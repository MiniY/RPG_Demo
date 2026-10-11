using UnityEngine;

/// <summary>
/// 为所有玩法模态、死亡恢复和换层操作提供按所有权释放的统一门禁。
/// </summary>
public static class TowerRunGateHub
{
    private static readonly TowerRunGate gameplay = new TowerRunGate();
    private static readonly TowerRunGate worldPause = new TowerRunGate();
    private static GameInput boundInput;
    private static float timeScaleBeforePause = 1f;

    public static bool IsGameplayBlocked => gameplay.IsBlocked;
    public static bool IsWorldPaused => worldPause.IsBlocked;

    static TowerRunGateHub()
    {
        gameplay.Changed += HandleGameplayGateChanged;
        worldPause.Changed += HandleWorldPauseChanged;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        gameplay.Clear();
        worldPause.Clear();
        boundInput = null;
        timeScaleBeforePause = 1f;
    }

    public static void BindInput(GameInput input)
    {
        boundInput = input;
        boundInput?.SetGameplayInputEnabled(!gameplay.IsBlocked);
    }

    public static TowerRunGateLease AcquireGameplay(string owner) => gameplay.Acquire(owner);
    public static TowerRunGateLease AcquireWorldPause(string owner) => worldPause.Acquire(owner);
    public static bool IsGameplayBlockedByOtherThan(string owner) => gameplay.IsBlockedByOtherThan(owner);

    private static void HandleGameplayGateChanged(bool blocked)
    {
        boundInput?.SetGameplayInputEnabled(!blocked);
    }

    private static void HandleWorldPauseChanged(bool paused)
    {
        if (paused)
        {
            if (!Mathf.Approximately(Time.timeScale, 0f))
                timeScaleBeforePause = Time.timeScale;

            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = timeScaleBeforePause <= 0f ? 1f : timeScaleBeforePause;
        }
    }
}
