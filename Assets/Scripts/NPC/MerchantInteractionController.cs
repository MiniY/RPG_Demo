using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 商人交互控制器，负责检测玩家距离、更新商人标识动画状态，并响应交互输入。
/// </summary>
public class MerchantInteractionController : MonoBehaviour, IMerchantPlacementTarget
{
    /// <summary>
    /// 任意商人成功接收到玩家交互时发出的全局事件。
    /// </summary>
    public static event Action<MerchantInteractionController> OnAnyMerchantInteracted;

    /// <summary>
    /// 商人标识 Animator（动画控制器）里的玩家范围参数名称。
    /// </summary>
    private const string PLAYER_IN_RANGE_PARAMETER_NAME = "PlayerInRange";

    /// <summary>
    /// PlayerInRange（玩家在范围内）参数的哈希值，用于减少 Animator 字符串查找成本。
    /// </summary>
    private static readonly int PlayerInRangeHash = Animator.StringToHash(PLAYER_IN_RANGE_PARAMETER_NAME);

    /// <summary>
    /// 玩家 Transform（变换组件），用于计算玩家和商人的距离。
    /// </summary>
    [SerializeField] private Transform playerTransform;

    /// <summary>
    /// 当前商人使用的 ShopCatalogSO（商店目录资产）。
    /// </summary>
    [SerializeField] private ShopCatalogSO shopCatalog; // 商人商店目录。

    /// <summary>
    /// 商人标识 Animator（动画控制器），用于控制 MerchantSign（商人标识）进入 Idle（待机）或 Floating（漂浮）状态。
    /// </summary>
    [SerializeField] private Animator merchantSignAnimator;

    /// <summary>
    /// 玩家可以按 F 交互的距离，数值越小交互越精准。
    /// </summary>
    [SerializeField, Min(0f)] private float interactDistance = 1.1f;

    /// <summary>
    /// 是否输出商人交互调试日志。
    /// </summary>
    [SerializeField] private bool showDebugLog = true;

    /// <summary>
    /// 商人被交互时触发的 UnityEvent（Unity 事件），可在 Inspector（检视面板）中扩展音效、对话等附加响应。
    /// </summary>
    [SerializeField] private UnityEvent onMerchantInteracted;

    /// <summary>
    /// 当前玩家是否处于商人的交互范围内。
    /// </summary>
    private bool isPlayerInRange;

    /// <summary>
    /// 当前已经绑定的 GameInput（游戏输入）实例。
    /// </summary>
    private GameInput boundGameInput;

    /// <summary>
    /// 对外提供当前商人的商店目录，只允许读取。
    /// </summary>
    public ShopCatalogSO ShopCatalog => shopCatalog;

    /// <summary>由 RandomGenerated Merchant placement 使用的场景实例 Transform。</summary>
    public Transform PlacementTransform => transform;

    /// <summary>当前交互逻辑实际使用的玩家引用。</summary>
    public Transform BoundPlayer => playerTransform;

    /// <summary>显式绑定 Q4 Canonical Player；不改变原有商店、UI 或交互所有权。</summary>
    public bool TryBindCanonicalPlayer(Transform canonicalPlayer, out string reason)
    {
        if (canonicalPlayer == null)
        {
            reason = "Canonical Player is missing.";
            return false;
        }

        playerTransform = canonicalPlayer;
        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// 进入播放模式时清空静态事件，避免关闭 Domain Reload（域重载）后残留旧订阅。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticEvents()
    {
        OnAnyMerchantInteracted = null;
    }

    /// <summary>
    /// 初始化商人标识动画引用，并设置初始范围状态。
    /// </summary>
    private void Awake()
    {
        AutoAssignMerchantSignAnimator();
        SetPlayerInRange(false);
    }

    /// <summary>
    /// 对象启用时尝试绑定交互输入事件。
    /// </summary>
    private void OnEnable()
    {
        TryBindGameInput();
    }

    /// <summary>
    /// 对象禁用时解绑交互输入事件，避免重复订阅。
    /// </summary>
    private void OnDisable()
    {
        UnbindGameInput();
    }

    /// <summary>
    /// 每帧刷新玩家引用、输入绑定和玩家范围状态。
    /// </summary>
    private void Update()
    {
        TryFindPlayerTransform();
        TryBindGameInput();
        RefreshPlayerRangeState();
    }

    /// <summary>
    /// 尝试自动查找 MerchantSign（商人标识）子物体上的 Animator（动画控制器）。
    /// </summary>
    private void AutoAssignMerchantSignAnimator()
    {
        if (merchantSignAnimator != null)
            return;

        Transform merchantSignTransform = transform.Find("MerchantSign");

        if (merchantSignTransform != null)
            merchantSignAnimator = merchantSignTransform.GetComponent<Animator>();
    }

    /// <summary>
    /// 尝试自动查找玩家 Transform（变换组件）。
    /// </summary>
    private void TryFindPlayerTransform()
    {
        if (playerTransform != null)
            return;

        PlayerAction playerAction = FindObjectOfType<PlayerAction>();

        if (playerAction != null)
            playerTransform = playerAction.transform;
    }

    /// <summary>
    /// 尝试绑定 GameInput（游戏输入）里的交互事件。
    /// </summary>
    private void TryBindGameInput()
    {
        if (boundGameInput != null)
            return;

        if (GameInput.Instance == null)
            return;

        boundGameInput = GameInput.Instance;
        boundGameInput.OnInteractPressed += HandleInteractPressed;
    }

    /// <summary>
    /// 解绑 GameInput（游戏输入）里的交互事件。
    /// </summary>
    private void UnbindGameInput()
    {
        if (boundGameInput == null)
            return;

        boundGameInput.OnInteractPressed -= HandleInteractPressed;
        boundGameInput = null;
    }

    /// <summary>
    /// 根据玩家和商人的距离刷新 PlayerInRange（玩家在范围内）状态。
    /// </summary>
    private void RefreshPlayerRangeState()
    {
        if (playerTransform == null)
        {
            SetPlayerInRange(false);
            return;
        }

        Vector2 offsetToPlayer = playerTransform.position - transform.position;
        float interactDistanceSqr = interactDistance * interactDistance;
        bool nextPlayerInRange = offsetToPlayer.sqrMagnitude <= interactDistanceSqr;

        SetPlayerInRange(nextPlayerInRange);
    }

    /// <summary>
    /// 设置玩家范围状态，并同步 MerchantSign（商人标识）的 Animator（动画控制器）参数。
    /// </summary>
    /// <param name="nextPlayerInRange">玩家是否在交互范围内。</param>
    private void SetPlayerInRange(bool nextPlayerInRange)
    {
        if (isPlayerInRange == nextPlayerInRange)
            return;

        isPlayerInRange = nextPlayerInRange;

        if (merchantSignAnimator != null)
            merchantSignAnimator.SetBool(PlayerInRangeHash, isPlayerInRange);
    }

    /// <summary>
    /// 处理 F 交互输入；只有玩家在范围内时才触发商人交互事件。
    /// </summary>
    private void HandleInteractPressed()
    {
        if (!isPlayerInRange)
            return;

        if (showDebugLog)
            Debug.Log("Merchant interaction triggered.", this);

        OnAnyMerchantInteracted?.Invoke(this);
        onMerchantInteracted?.Invoke();
    }

    /// <summary>
    /// 在 Scene（场景）视图中显示商人的交互范围，方便调试精准距离。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }

    /// <summary>
    /// 在 Inspector（检查器）中修改数值时保证交互距离不为负数。
    /// </summary>
    private void OnValidate()
    {
        interactDistance = Mathf.Max(0f, interactDistance);
    }
}
