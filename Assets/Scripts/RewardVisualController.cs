using UnityEngine;

// 控制奖励掉落物的视觉动画，不负责拾取逻辑。
[RequireComponent(typeof(Animator))]
public class RewardVisualController : MonoBehaviour
{
    [SerializeField] private Animator animator; // 奖励视觉物体上的动画控制器。
    [SerializeField] private string spawnStateName = "Spawn"; // 掉落物出现时播放的动画状态名。
    [SerializeField] private string isCollectedParameterName = "IsCollected"; // 是否已经被玩家拾取的 Animator 参数名。
    [SerializeField, Min(0)] private int animationLayer = 0; // 播放动画使用的 Animator 图层。
    [SerializeField] private bool showMissingStateWarning = true; // 找不到动画状态时是否显示提示。

    // 初始化动画控制器引用。
    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    // 每次对象启用或从对象池取出时，重新播放掉落动画。
    private void OnEnable()
    {
        ResetForSpawn();
    }

    // 重置为未拾取状态，并从第一帧播放掉落动画。
    public void ResetForSpawn()
    {
        if (animator == null || string.IsNullOrWhiteSpace(spawnStateName))
            return;

        SetCollected(false);

        if (!TryPlaySpawnState())
            return;

        animator.Update(0f);
    }

    // 标记奖励已经被拾取，实际回收由 RewardPickup 处理。
    public void MarkCollected()
    {
        if (animator == null)
            return;

        SetCollected(true);
    }

    // 设置 Animator 里的拾取状态参数。
    private void SetCollected(bool isCollected)
    {
        if (animator == null || string.IsNullOrWhiteSpace(isCollectedParameterName))
            return;

        animator.SetBool(isCollectedParameterName, isCollected);
    }

    // 尝试从第一帧播放 Spawn 状态，状态不存在时避免 Unity 报错。
    private bool TryPlaySpawnState()
    {
        int shortNameHash = Animator.StringToHash(spawnStateName);

        if (!animator.HasState(animationLayer, shortNameHash))
        {
            if (showMissingStateWarning)
                Debug.LogWarning($"奖励动画状态不存在：{spawnStateName}。请检查 Animator 状态名或 RewardVisualController 配置。", this);

            return false;
        }

        animator.Play(shortNameHash, animationLayer, 0f);
        return true;
    }
}
