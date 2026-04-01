using TaskbarUnhideZoner.Detection;

namespace TaskbarUnhideZoner.Tests;

public sealed class ZoneStateMachineTests
{
    [Fact]
    public void TriggerRequiresDwellTime()
    {
        var machine = new ZoneStateMachine(() => 300, () => 500);

        Assert.False(machine.Update(inZone: true, nowMs: 0));
        Assert.False(machine.Update(inZone: true, nowMs: 299));
        Assert.True(machine.Update(inZone: true, nowMs: 300));
    }

    [Fact]
    public void CooldownRequiresLeaveAndReenter()
    {
        var machine = new ZoneStateMachine(() => 100, () => 200);

        Assert.False(machine.Update(inZone: true, nowMs: 0));
        Assert.True(machine.Update(inZone: true, nowMs: 100));

        Assert.False(machine.Update(inZone: true, nowMs: 500));
        Assert.False(machine.Update(inZone: false, nowMs: 501));
        Assert.False(machine.Update(inZone: true, nowMs: 502));
        Assert.True(machine.Update(inZone: true, nowMs: 602));
    }
}
