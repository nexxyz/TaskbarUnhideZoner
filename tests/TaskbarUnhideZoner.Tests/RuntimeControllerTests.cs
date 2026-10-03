using System.Drawing;
using TaskbarUnhideZoner.Models;
using TaskbarUnhideZoner.Runtime;
using TaskbarUnhideZoner.Services;

namespace TaskbarUnhideZoner.Tests;

public sealed class RuntimeControllerTests
{
    [Fact]
    public void StartupRecoversPendingAutohideRestore()
    {
        var config = new AppConfig
        {
            Enabled = true,
            PendingAutohideRestore = true
        };
        var taskbarState = new FakeTaskbarStateService(autoHideEnabled: false);
        var engine = new FakeZoneEngine();

        using var runtime = new RuntimeController(
            config,
            taskbarState,
            new FakeRevealService(),
            _ => engine,
            _ => { },
            startupEnabled: false);

        Assert.True(taskbarState.AutoHideEnabled);
        Assert.False(config.PendingAutohideRestore);
        Assert.False(runtime.IsAutohideOffSuspended);
        Assert.Equal(1, taskbarState.EnableAutoHideCalls);
        Assert.Equal(1, engine.StartCalls);
    }

    [Fact]
    public void StartupSuspendsWhenAutohideIsOffWithoutRecoveryFlag()
    {
        var config = new AppConfig
        {
            Enabled = true,
            PendingAutohideRestore = false
        };
        var taskbarState = new FakeTaskbarStateService(autoHideEnabled: false);
        var engine = new FakeZoneEngine();

        using var runtime = new RuntimeController(
            config,
            taskbarState,
            new FakeRevealService(),
            _ => engine,
            _ => { },
            startupEnabled: false);

        Assert.False(taskbarState.AutoHideEnabled);
        Assert.True(runtime.IsAutohideOffSuspended);
        Assert.Equal(0, taskbarState.EnableAutoHideCalls);
        Assert.Equal(0, engine.StartCalls);
    }

    [Fact]
    public void FailedPendingAutohideRestoreAtStartupRemainsPendingAndRefreshRetries()
    {
        var config = new AppConfig
        {
            Enabled = true,
            PendingAutohideRestore = true
        };
        var taskbarState = new FakeTaskbarStateService(autoHideEnabled: false);
        taskbarState.EnableAutoHideResults.Enqueue(false);
        taskbarState.EnableAutoHideResults.Enqueue(true);
        var engine = new FakeZoneEngine();

        using var runtime = new RuntimeController(
            config,
            taskbarState,
            new FakeRevealService(),
            _ => engine,
            _ => { },
            startupEnabled: false);

        Assert.False(taskbarState.AutoHideEnabled);
        Assert.True(config.PendingAutohideRestore);
        Assert.True(runtime.IsAutohideOffSuspended);
        Assert.Equal(1, taskbarState.EnableAutoHideCalls);
        Assert.Equal(0, engine.StartCalls);

        runtime.RefreshAutohideState();

        Assert.True(taskbarState.AutoHideEnabled);
        Assert.False(config.PendingAutohideRestore);
        Assert.False(runtime.IsAutohideOffSuspended);
        Assert.Equal(2, taskbarState.EnableAutoHideCalls);
        Assert.Equal(1, engine.StartCalls);
    }

    private const int FastKeepAliveMs = 20;

    [Fact]
    public void RevealKeepsAutohideAndStopsKeepAliveOnLeave()
    {
        var config = new AppConfig { Enabled = true };
        var taskbarState = new FakeTaskbarStateService(autoHideEnabled: true);
        var reveal = new FakeRevealService();
        using var runtime = CreateRuntime(config, taskbarState, reveal, new FakeZoneEngine());

        runtime.OnZoneTriggered(new Point(10, 10));

        Assert.True(SpinWait.SpinUntil(() => reveal.RevealCalls >= 3, TimeSpan.FromSeconds(5)), $"expected keep-alive reveals, got {reveal.RevealCalls}");
        Assert.True(taskbarState.AutoHideEnabled);
        Assert.Equal(0, taskbarState.EnableAutoHideCalls);
        Assert.False(config.PendingAutohideRestore);

        runtime.OnZoneLeft();
        AssertKeepAliveStopped(reveal);
    }

    [Fact]
    public void RevealDoesNotRetriggerWhileActive()
    {
        var reveal = new FakeRevealService();
        using var runtime = CreateRuntime(
            new AppConfig { Enabled = true },
            new FakeTaskbarStateService(autoHideEnabled: true),
            reveal,
            new FakeZoneEngine(),
            revealKeepAliveMs: Timeout.Infinite);

        runtime.OnZoneTriggered(new Point(10, 10));
        runtime.OnZoneTriggered(new Point(10, 10));

        Assert.Equal(1, reveal.RevealCalls);
    }

    [Fact]
    public void DisablingStopsKeepAlive()
    {
        var reveal = new FakeRevealService();
        using var runtime = CreateRuntime(new AppConfig { Enabled = true }, new FakeTaskbarStateService(autoHideEnabled: true), reveal, new FakeZoneEngine());

        runtime.OnZoneTriggered(new Point(10, 10));
        runtime.SetEnabled(false);

        AssertKeepAliveStopped(reveal);
    }

    [Fact]
    public void AutohideTurnedOffExternallyStopsKeepAlive()
    {
        var taskbarState = new FakeTaskbarStateService(autoHideEnabled: true);
        var reveal = new FakeRevealService();
        using var runtime = CreateRuntime(new AppConfig { Enabled = true }, taskbarState, reveal, new FakeZoneEngine());

        runtime.OnZoneTriggered(new Point(10, 10));
        taskbarState.AutoHideEnabled = false;
        runtime.RefreshAutohideState();

        Assert.True(runtime.IsAutohideOffSuspended);
        AssertKeepAliveStopped(reveal);
    }

    [Fact]
    public void ReinitializeDetectionStopsKeepAlive()
    {
        var reveal = new FakeRevealService();
        var engine = new FakeZoneEngine();
        using var runtime = CreateRuntime(new AppConfig { Enabled = true }, new FakeTaskbarStateService(autoHideEnabled: true), reveal, engine);

        runtime.OnZoneTriggered(new Point(10, 10));
        runtime.ReinitializeDetection();

        Assert.Equal(1, engine.ReinitializeCalls);
        AssertKeepAliveStopped(reveal);
    }

    [Fact]
    public void TriggerAfterDisposeIsIgnored()
    {
        var reveal = new FakeRevealService();
        var runtime = CreateRuntime(new AppConfig { Enabled = true }, new FakeTaskbarStateService(autoHideEnabled: true), reveal, new FakeZoneEngine());

        runtime.Dispose();
        runtime.OnZoneTriggered(new Point(10, 10));
        runtime.OnZoneLeft();
        runtime.RefreshAutohideState();

        Assert.Equal(0, reveal.RevealCalls);
    }

    private static RuntimeController CreateRuntime(
        AppConfig config,
        FakeTaskbarStateService taskbarState,
        FakeRevealService reveal,
        FakeZoneEngine engine,
        int revealKeepAliveMs = FastKeepAliveMs)
    {
        return new RuntimeController(
            config,
            taskbarState,
            reveal,
            _ => engine,
            _ => { },
            startupEnabled: false,
            revealKeepAliveMs);
    }

    // Stop is lock-protected and re-checked by the timer callback, so after a short settle the count must freeze.
    private static void AssertKeepAliveStopped(FakeRevealService reveal)
    {
        Thread.Sleep(FastKeepAliveMs * 3);
        var callsAfterStop = reveal.RevealCalls;
        Thread.Sleep(FastKeepAliveMs * 10);
        Assert.Equal(callsAfterStop, reveal.RevealCalls);
    }

    private sealed class FakeRevealService : ITaskbarRevealService
    {
        private int _revealCalls;

        public int RevealCalls => Volatile.Read(ref _revealCalls);

        public bool Reveal()
        {
            Interlocked.Increment(ref _revealCalls);
            return true;
        }

        public bool IsAnyTaskbarShown() => true;
    }

    private sealed class FakeTaskbarStateService(bool autoHideEnabled) : ITaskbarStateService
    {
        public bool AutoHideEnabled { get; set; } = autoHideEnabled;

        public int EnableAutoHideCalls { get; private set; }

        public Queue<bool> EnableAutoHideResults { get; } = new();

        public bool IsAutoHideEnabled() => AutoHideEnabled;

        public bool EnableAutoHide()
        {
            EnableAutoHideCalls++;
            if (EnableAutoHideResults.Count > 0 && !EnableAutoHideResults.Dequeue())
            {
                return false;
            }

            AutoHideEnabled = true;
            return true;
        }
    }

    private sealed class FakeZoneEngine : IZoneEngineController
    {
        public int StartCalls { get; private set; }

        public int ReinitializeCalls { get; private set; }

        public void Start()
        {
            StartCalls++;
        }

        public void Stop()
        {
        }

        public void Reinitialize()
        {
            ReinitializeCalls++;
        }

        public void Dispose()
        {
        }
    }
}
