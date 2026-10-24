using System;

/// <summary>One resident's observed departure episode. Shared navigation never owns these clocks or latches.</summary>
public sealed class EcologyExcursionState
{
    public EcologyExcursionState(int maximumHomeSteps, double maximumOutsideSeconds)
    {
        if (maximumHomeSteps < 1 || double.IsNaN(maximumOutsideSeconds) || double.IsInfinity(maximumOutsideSeconds) || maximumOutsideSeconds <= 0d)
            throw new ArgumentOutOfRangeException(nameof(maximumHomeSteps));
        MaximumHomeSteps = maximumHomeSteps; MaximumOutsideSeconds = maximumOutsideSeconds;
    }
    private double lastWorldTime = double.NegativeInfinity;
    public int MaximumHomeSteps { get; }
    public double MaximumOutsideSeconds { get; }
    public bool HasPermit { get; private set; }
    public bool ReturnLatched { get; private set; }
    public bool NavigationFault { get; private set; }
    public long Episode { get; private set; }
    public double DepartedAt { get; private set; }
    public double Elapsed { get; private set; }
    public double LastCompletedElapsed { get; private set; }
    public bool CounterattackReserved { get; private set; }
    public double CounterattackDeadline { get; private set; }
    private bool counterattackRunning;
    public bool MayChase => !ReturnLatched && !NavigationFault;
    public void RequestReturn() { ReturnLatched = true; }
    public bool CounterattackActive(double worldNow) => HasPermit && ReturnLatched && !NavigationFault && counterattackRunning && worldNow < CounterattackDeadline;
    public bool TryReserveCounterattack(double worldNow, double originalCycleDuration)
    {
        if (!HasPermit || !ReturnLatched || NavigationFault || CounterattackReserved || originalCycleDuration <= 0d ||
            double.IsNaN(originalCycleDuration) || double.IsInfinity(originalCycleDuration)) return false;
        CounterattackReserved = true; counterattackRunning = true;
        CounterattackDeadline = worldNow + Math.Min(2d, originalCycleDuration);
        return true;
    }
    public void EndCounterattack() { counterattackRunning = false; }
    public void ObserveActualPose(bool completeHome, bool stopped, int homeSteps, double scaledWorldNow)
    {
        if (double.IsNaN(scaledWorldNow) || double.IsInfinity(scaledWorldNow) || scaledWorldNow < lastWorldTime)
            throw new ArgumentOutOfRangeException(nameof(scaledWorldNow), "A resident lease needs one monotonic scaled world clock.");
        lastWorldTime = scaledWorldNow;
        if (!completeHome && !HasPermit)
        {
            HasPermit = true; Episode = checked(Episode + 1); DepartedAt = scaledWorldNow;
            CounterattackReserved = false; counterattackRunning = false;
        }
        Elapsed = HasPermit ? scaledWorldNow - DepartedAt : 0d;
        if (homeSteps < 0 || completeHome && homeSteps != 0 || !completeHome && homeSteps == 0)
        {
            NavigationFault = true; ReturnLatched = true;
        }
        if (HasPermit && (homeSteps > MaximumHomeSteps || Elapsed >= MaximumOutsideSeconds)) ReturnLatched = true;
        if (completeHome && stopped && !NavigationFault)
        {
            if (HasPermit) LastCompletedElapsed = Elapsed;
            HasPermit = false; ReturnLatched = false; Elapsed = 0d;
            CounterattackReserved = false; counterattackRunning = false;
        }
    }
}
