namespace TaskbarUnhideZoner.Detection;

internal enum ZoneState
{
    OutsideZone,
    InsideZoneCounting,
    TriggeredCooldown
}

internal sealed class ZoneStateMachine
{
    private readonly Func<int> _triggerDelayMs;
    private readonly Func<int> _cooldownMs;

    private ZoneState _state = ZoneState.OutsideZone;
    private long _enteredAtMs;
    private long _cooldownUntilMs;

    public ZoneStateMachine(Func<int> triggerDelayMs, Func<int> cooldownMs)
    {
        _triggerDelayMs = triggerDelayMs;
        _cooldownMs = cooldownMs;
    }

    public ZoneState State => _state;

    public bool Update(bool inZone, long nowMs)
    {
        switch (_state)
        {
            case ZoneState.OutsideZone:
                if (inZone)
                {
                    _enteredAtMs = nowMs;
                    _state = ZoneState.InsideZoneCounting;
                }

                return false;

            case ZoneState.InsideZoneCounting:
                if (!inZone)
                {
                    _state = ZoneState.OutsideZone;
                    return false;
                }

                if (nowMs - _enteredAtMs < _triggerDelayMs())
                {
                    return false;
                }

                _cooldownUntilMs = nowMs + _cooldownMs();
                _state = ZoneState.TriggeredCooldown;
                return true;

            case ZoneState.TriggeredCooldown:
                if (!inZone && nowMs >= _cooldownUntilMs)
                {
                    _state = ZoneState.OutsideZone;
                }

                return false;

            default:
                return false;
        }
    }

    public void ForceOutside()
    {
        _state = ZoneState.OutsideZone;
        _enteredAtMs = 0;
        _cooldownUntilMs = 0;
    }
}
