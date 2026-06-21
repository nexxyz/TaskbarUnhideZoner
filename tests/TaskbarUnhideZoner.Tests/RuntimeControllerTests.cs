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
            _ => engine,
            _ => { },
            startupEnabled: false);

        Assert.True(taskbarState.AutoHideEnabled);
        Assert.False(config.PendingAutohideRestore);
        Assert.False(runtime.IsAutohideOffSuspended);
        Assert.Equal(new[] { true }, taskbarState.SetAutoHideEnabledCalls);
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
            _ => engine,
            _ => { },
            startupEnabled: false);

        Assert.False(taskbarState.AutoHideEnabled);
        Assert.True(runtime.IsAutohideOffSuspended);
        Assert.Empty(taskbarState.SetAutoHideEnabledCalls);
        Assert.Equal(0, engine.StartCalls);
    }

    [Fact]
    public void TriggerAndLeaveManageRecoveryFlagLifecycle()
    {
        var config = new AppConfig
        {
            Enabled = true,
            PendingAutohideRestore = false
        };
        var taskbarState = new FakeTaskbarStateService(autoHideEnabled: true);
        var engine = new FakeZoneEngine();

        using var runtime = new RuntimeController(
            config,
            taskbarState,
            _ => engine,
            _ => { },
            startupEnabled: false);

        runtime.OnZoneTriggered(new Point(10, 10));

        Assert.False(taskbarState.AutoHideEnabled);
        Assert.True(config.PendingAutohideRestore);

        runtime.OnZoneLeft();

        Assert.True(taskbarState.AutoHideEnabled);
        Assert.False(config.PendingAutohideRestore);
        Assert.Equal(new[] { false, true }, taskbarState.SetAutoHideEnabledCalls);
    }

    private sealed class FakeTaskbarStateService(bool autoHideEnabled) : ITaskbarStateService
    {
        public bool AutoHideEnabled { get; private set; } = autoHideEnabled;

        public List<bool> SetAutoHideEnabledCalls { get; } = new();

        public uint GetStateFlags() => AutoHideEnabled ? Interop.NativeMethods.AbsAutoHide : 0;

        public bool IsAutoHideEnabled() => AutoHideEnabled;

        public bool SetAutoHideEnabled(bool enabled)
        {
            SetAutoHideEnabledCalls.Add(enabled);
            AutoHideEnabled = enabled;
            return true;
        }

        public bool SetStateFlags(uint stateFlags)
        {
            AutoHideEnabled = (stateFlags & Interop.NativeMethods.AbsAutoHide) != 0;
            return true;
        }
    }

    private sealed class FakeZoneEngine : IZoneEngineController
    {
        public int StartCalls { get; private set; }

        public void Start()
        {
            StartCalls++;
        }

        public void Stop()
        {
        }

        public void Reinitialize()
        {
        }

        public void Dispose()
        {
        }
    }
}
