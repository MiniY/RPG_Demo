using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 保存一种奖励在背包中的数量。
/// </summary>
[Serializable]
public class InventoryItemStack
{
    /// <summary>
    /// 奖励的静态数据，例如名称、图标、说明。
    /// </summary>
    [SerializeField] private RewardSO reward; // 奖励数据。

    /// <summary>
    /// 玩家当前拥有这种奖励的数量。
    /// </summary>
    [SerializeField] private int amount; // 奖励数量。

    /// <summary>
    /// 对外提供奖励数据，只允许读取。
    /// </summary>
    public RewardSO Reward => reward;

    /// <summary>
    /// 对外提供奖励数量，只允许读取。
    /// </summary>
    public int Amount => amount;

    /// <summary>
    /// Unity（团结引擎）反序列化需要的无参构造函数。
    /// </summary>
    public InventoryItemStack()
    {
    }

    /// <summary>
    /// 创建一个新的奖励数量记录。
    /// </summary>
    /// <param name="rewardData">要记录的奖励数据。</param>
    /// <param name="rewardAmount">要记录的奖励数量。</param>
    public InventoryItemStack(RewardSO rewardData, int rewardAmount)
    {
        reward = rewardData;
        amount = Mathf.Clamp(rewardAmount, 0, GameSaveService.MaxQuantity);
    }

    /// <summary>
    /// 修改数量，只允许背包系统内部调用。
    /// </summary>
    /// <param name="nextAmount">新的奖励数量。</param>
    internal void SetAmount(int nextAmount)
    {
        amount = Mathf.Clamp(nextAmount, 0, GameSaveService.MaxQuantity);
    }
}

/// <summary>
/// 玩家背包交易前的内存快照，用于交易失败时完整回滚。
/// </summary>
public sealed class PlayerInventorySnapshot
{
    /// <summary>
    /// 快照中的奖励数量记录。
    /// </summary>
    internal readonly List<InventoryItemStack> ItemStacks = new List<InventoryItemStack>();
}

/// <summary>
/// 管理玩家背包中的奖励数量，不负责背包界面显示。
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    /// <summary>
    /// 背包内容发生变化时通知界面刷新。
    /// </summary>
    public event Action OnInventoryChanged;

    /// <summary>
    /// 玩家当前拥有的奖励列表，保留在 Inspector（检视面板）中方便调试查看。
    /// </summary>
    [SerializeField] private List<InventoryItemStack> itemStacks = new List<InventoryItemStack>(); // 背包物品列表。

    /// <summary>
    /// 奖励注册表，用于把存档中的 RewardId（奖励唯一 ID）解析成奖励资产。
    /// </summary>
    [SerializeField] private RewardRegistrySO rewardRegistry; // 奖励注册表。

    /// <summary>
    /// Gold（金币）奖励数据。
    /// </summary>
    [SerializeField] private RewardSO goldReward; // 金币奖励。

    /// <summary>
    /// 没有有效存档时新游戏给予的初始金币。
    /// </summary>
    [SerializeField, Min(0)] private int newGameStartingGold = 100; // 新游戏初始金币。

    /// <summary>
    /// 当前对象是否已经完成初始化。
    /// </summary>
    private bool isInitialized; // 是否已经初始化。

    /// <summary>
    /// 当前版本暂时无法解析的存档条目，保存时原样保留，避免资源缺失导致数据丢失。
    /// </summary>
    private readonly List<GameSaveInventoryEntry> unresolvedSaveEntries =
        new List<GameSaveInventoryEntry>(); // 未解析存档条目。

    /// <summary>
    /// 提供给 UI（用户界面）读取的背包内容，只允许读取，不允许外部直接修改。
    /// </summary>
    public IReadOnlyList<InventoryItemStack> ItemStacks => itemStacks;

    /// <summary>
    /// 当前背包使用的金币奖励数据。
    /// </summary>
    public RewardSO GoldReward
    {
        get
        {
            ResolveGoldReward();
            return goldReward;
        }
    }

    /// <summary>
    /// 当前背包是否已经完成存档加载。
    /// </summary>
    public bool IsInitialized => isInitialized;

    /// <summary>
    /// 初始化背包并加载统一游戏存档。
    /// </summary>
    private void Awake()
    {
        InitializeFromSave();
    }

    /// <summary>
    /// 脚本启用时开始监听奖励拾取事件。
    /// </summary>
    private void OnEnable()
    {
        RewardPickup.OnRewardPickupRequested += HandleRewardPickupRequested;
    }

    /// <summary>
    /// 脚本禁用时停止监听奖励拾取事件，避免对象销毁后还收到通知。
    /// </summary>
    private void OnDisable()
    {
        RewardPickup.OnRewardPickupRequested -= HandleRewardPickupRequested;
    }

    /// <summary>
    /// 应用暂停时保存当前背包，减少切后台造成的数据丢失。
    /// </summary>
    /// <param name="pauseStatus">应用是否进入暂停状态。</param>
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            SaveCurrentState();
    }

    /// <summary>
    /// 应用退出时保存当前背包。
    /// </summary>
    private void OnApplicationQuit()
    {
        SaveCurrentState();
    }

    /// <summary>
    /// 处理地面奖励拾取请求，只有保存成功才确认接收。
    /// </summary>
    /// <param name="reward">被拾取的奖励数据。</param>
    /// <param name="amount">被拾取的奖励数量。</param>
    /// <returns>奖励成功加入并保存返回 true。</returns>
    private bool HandleRewardPickupRequested(RewardSO reward, int amount)
    {
        return TryAddItem(reward, amount);
    }

    /// <summary>
    /// 往背包中增加指定数量的奖励，并立即保存。
    /// </summary>
    /// <param name="reward">要增加的奖励数据。</param>
    /// <param name="amount">要增加的奖励数量。</param>
    public void AddItem(RewardSO reward, int amount)
    {
        TryAddItem(reward, amount);
    }

    /// <summary>
    /// 往背包中增加指定数量的奖励，并在保存失败时恢复原状态。
    /// </summary>
    /// <param name="reward">要增加的奖励数据。</param>
    /// <param name="amount">要增加的奖励数量，可以使用 long（64 位整数）进行安全计算。</param>
    /// <returns>增加并保存成功返回 true，否则返回 false。</returns>
    public bool TryAddItem(RewardSO reward, long amount)
    {
        EnsureInitialized();

        PlayerInventorySnapshot snapshot = CaptureSnapshot(); // 修改前快照。

        if (!TryAddItemWithoutPersistence(reward, amount))
            return false;

        if (!SaveCurrentState())
        {
            RestoreSnapshot(snapshot, false);
            return false;
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 从背包中扣除指定数量的奖励，数量不足或保存失败时不会留下修改。
    /// </summary>
    /// <param name="reward">要扣除的奖励数据。</param>
    /// <param name="amount">要扣除的奖励数量。</param>
    /// <returns>扣除并保存成功返回 true，否则返回 false。</returns>
    public bool RemoveItem(RewardSO reward, int amount)
    {
        return TryRemoveItem(reward, amount);
    }

    /// <summary>
    /// 从背包中扣除指定数量的奖励，并在保存失败时恢复原状态。
    /// </summary>
    /// <param name="reward">要扣除的奖励数据。</param>
    /// <param name="amount">要扣除的奖励数量。</param>
    /// <returns>扣除并保存成功返回 true，否则返回 false。</returns>
    public bool TryRemoveItem(RewardSO reward, long amount)
    {
        EnsureInitialized();

        PlayerInventorySnapshot snapshot = CaptureSnapshot(); // 修改前快照。

        if (!TryRemoveItemWithoutPersistence(reward, amount))
            return false;

        if (!SaveCurrentState())
        {
            RestoreSnapshot(snapshot, false);
            return false;
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 判断背包中是否拥有足够数量的某种奖励。
    /// </summary>
    /// <param name="reward">要检查的奖励数据。</param>
    /// <param name="amount">需要的奖励数量。</param>
    /// <returns>数量足够返回 true，否则返回 false。</returns>
    public bool HasItem(RewardSO reward, int amount)
    {
        return HasItem(reward, (long)amount);
    }

    /// <summary>
    /// 判断背包中是否拥有足够数量的某种奖励。
    /// </summary>
    /// <param name="reward">要检查的奖励数据。</param>
    /// <param name="amount">需要的奖励数量。</param>
    /// <returns>数量足够返回 true，否则返回 false。</returns>
    public bool HasItem(RewardSO reward, long amount)
    {
        if (reward == null || amount <= 0)
            return false;

        return GetItemAmountLong(reward) >= amount;
    }

    /// <summary>
    /// 获取背包中某种奖励的当前数量，结果限制在 int（32 位整数）范围内。
    /// </summary>
    /// <param name="reward">要查询的奖励数据。</param>
    /// <returns>当前拥有数量，没有该奖励时返回 0。</returns>
    public int GetItemAmount(RewardSO reward)
    {
        return ClampToInt(GetItemAmountLong(reward));
    }

    /// <summary>
    /// 获取背包中某种奖励的长整数数量。
    /// </summary>
    /// <param name="reward">要查询的奖励数据。</param>
    /// <returns>当前拥有数量。</returns>
    public long GetItemAmountLong(RewardSO reward)
    {
        InventoryItemStack existingStack = FindStack(reward); // 要查询的奖励记录。
        return existingStack == null ? 0L : existingStack.Amount;
    }

    /// <summary>
    /// 创建当前背包的内存快照。
    /// </summary>
    /// <returns>可用于回滚的背包快照。</returns>
    public PlayerInventorySnapshot CaptureSnapshot()
    {
        PlayerInventorySnapshot snapshot = new PlayerInventorySnapshot(); // 新快照。

        if (itemStacks == null)
            return snapshot;

        for (int i = 0; i < itemStacks.Count; i++)
        {
            InventoryItemStack stack = itemStacks[i];

            if (stack == null)
                continue;

            snapshot.ItemStacks.Add(new InventoryItemStack(stack.Reward, stack.Amount));
        }

        return snapshot;
    }

    /// <summary>
    /// 用快照恢复背包内存状态。
    /// </summary>
    /// <param name="snapshot">要恢复的快照。</param>
    /// <param name="notify">恢复后是否通知 UI（用户界面）刷新。</param>
    public void RestoreSnapshot(PlayerInventorySnapshot snapshot, bool notify)
    {
        if (snapshot == null)
            return;

        if (itemStacks == null)
            itemStacks = new List<InventoryItemStack>();

        itemStacks.Clear();

        for (int i = 0; i < snapshot.ItemStacks.Count; i++)
        {
            InventoryItemStack stack = snapshot.ItemStacks[i];

            if (stack == null || stack.Reward == null || stack.Amount <= 0)
                continue;

            itemStacks.Add(new InventoryItemStack(stack.Reward, stack.Amount));
        }

        if (notify)
            OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// 交易成功后通知背包界面和 HUD（抬头显示）刷新。
    /// </summary>
    internal void NotifyInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// 供商店等外部系统确保玩家背包已经完成存档加载。
    /// </summary>
    internal void EnsureInitializedForRuntime()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// 在不触发保存和事件的情况下增加奖励，供原子交易使用。
    /// </summary>
    /// <param name="reward">要增加的奖励。</param>
    /// <param name="amount">要增加的数量。</param>
    /// <returns>参数有效并完成修改返回 true。</returns>
    internal bool TryAddItemWithoutPersistence(RewardSO reward, long amount)
    {
        if (reward == null || amount <= 0)
            return false;

        int safeAmount = ClampToInt(amount);

        if (safeAmount <= 0)
            return false;

        InventoryItemStack existingStack = FindStack(reward); // 已经存在的奖励记录。

        if (existingStack == null)
        {
            itemStacks.Add(new InventoryItemStack(reward, safeAmount));
            return true;
        }

        long nextAmount = (long)existingStack.Amount + safeAmount;

        if (nextAmount > GameSaveService.MaxQuantity)
            return false;

        existingStack.SetAmount((int)nextAmount);
        return true;
    }

    /// <summary>
    /// 在不触发保存和事件的情况下扣除奖励，供原子交易使用。
    /// </summary>
    /// <param name="reward">要扣除的奖励。</param>
    /// <param name="amount">要扣除的数量。</param>
    /// <returns>数量足够并完成修改返回 true。</returns>
    internal bool TryRemoveItemWithoutPersistence(RewardSO reward, long amount)
    {
        if (reward == null || amount <= 0)
            return false;

        InventoryItemStack existingStack = FindStack(reward); // 要扣除的奖励记录。

        if (existingStack == null || existingStack.Amount < amount)
            return false;

        int nextAmount = ClampToInt(existingStack.Amount - amount);
        existingStack.SetAmount(nextAmount);

        if (existingStack.Amount <= 0)
            itemStacks.Remove(existingStack);

        return true;
    }

    /// <summary>
    /// 将当前背包内容写入候选存档，不执行磁盘写入。
    /// </summary>
    /// <param name="data">要写入的候选存档。</param>
    internal void WriteToSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        if (data.inventory == null)
            data.inventory = new List<GameSaveInventoryEntry>();

        data.inventory.Clear();

        for (int i = 0; i < unresolvedSaveEntries.Count; i++)
        {
            GameSaveInventoryEntry unresolvedEntry = unresolvedSaveEntries[i];

            if (unresolvedEntry == null ||
                string.IsNullOrWhiteSpace(unresolvedEntry.rewardId) ||
                unresolvedEntry.amount <= 0)
            {
                continue;
            }

            data.inventory.Add(new GameSaveInventoryEntry
            {
                rewardId = unresolvedEntry.rewardId,
                amount = unresolvedEntry.amount
            });
        }

        if (itemStacks == null)
            return;

        for (int i = 0; i < itemStacks.Count; i++)
        {
            InventoryItemStack stack = itemStacks[i];

            if (stack == null || stack.Reward == null || stack.Amount <= 0)
                continue;

            stack.Reward.EnsureRewardId();

            if (string.IsNullOrWhiteSpace(stack.Reward.RewardId))
                continue;

            data.inventory.Add(new GameSaveInventoryEntry
            {
                rewardId = stack.Reward.RewardId,
                amount = stack.Amount
            });
        }
    }

    /// <summary>
    /// 初始化背包引用并从统一存档恢复数量。
    /// </summary>
    private void InitializeFromSave()
    {
        if (isInitialized)
            return;

        if (itemStacks == null)
            itemStacks = new List<InventoryItemStack>();

        ResolveGoldReward();
        unresolvedSaveEntries.Clear();

        GameSaveData loadedData = GameSaveService.GetOrCreateData(); // 统一存档数据。

        if (GameSaveService.HasLoadedExistingSave && rewardRegistry != null)
        {
            LoadFromSaveData(loadedData);
        }
        else
        {
            NormalizeRuntimeStacks();

            if (!GameSaveService.HasLoadedExistingSave &&
                goldReward != null &&
                GetItemAmountLong(goldReward) <= 0 &&
                newGameStartingGold > 0)
            {
                TryAddItemWithoutPersistence(goldReward, newGameStartingGold);
            }

            GameSaveData newCandidate = GameSaveService.Clone(loadedData);
            WriteToSaveData(newCandidate);

            if (!GameSaveService.HasLoadedExistingSave && !GameSaveService.TrySave(newCandidate))
                Debug.LogWarning("新游戏初始数据保存失败，本次运行仍会继续使用内存数据。", this);
        }

        isInitialized = true;
    }

    /// <summary>
    /// 从存档中的 RewardId（奖励唯一 ID）恢复玩家背包。
    /// </summary>
    /// <param name="data">已加载的完整存档。</param>
    private void LoadFromSaveData(GameSaveData data)
    {
        itemStacks.Clear();
        unresolvedSaveEntries.Clear();

        if (data == null || data.inventory == null)
            return;

        for (int i = 0; i < data.inventory.Count; i++)
        {
            GameSaveInventoryEntry entry = data.inventory[i];

            if (entry == null || entry.amount <= 0)
                continue;

            RewardSO reward = rewardRegistry.FindById(entry.rewardId); // 存档 ID 对应的奖励资产。

            if (reward == null)
            {
                Debug.LogWarning($"存档中的奖励无法在 RewardRegistrySO（奖励注册表）中找到：{entry.rewardId}", this);
                unresolvedSaveEntries.Add(new GameSaveInventoryEntry
                {
                    rewardId = entry.rewardId,
                    amount = Math.Max(0L, Math.Min((long)GameSaveService.MaxQuantity, entry.amount))
                });
                continue;
            }

            TryAddItemWithoutPersistence(reward, entry.amount);
        }

        NormalizeRuntimeStacks();
    }

    /// <summary>
    /// 规范化 Inspector（检视面板）或旧数据中的背包堆。
    /// </summary>
    private void NormalizeRuntimeStacks()
    {
        if (itemStacks == null)
            itemStacks = new List<InventoryItemStack>();

        Dictionary<RewardSO, InventoryItemStack> uniqueStacks =
            new Dictionary<RewardSO, InventoryItemStack>();

        for (int i = itemStacks.Count - 1; i >= 0; i--)
        {
            InventoryItemStack stack = itemStacks[i];

            if (stack == null || stack.Reward == null || stack.Amount <= 0)
            {
                itemStacks.RemoveAt(i);
                continue;
            }

            stack.SetAmount(ClampToInt(stack.Amount));

            if (!uniqueStacks.TryGetValue(stack.Reward, out InventoryItemStack existingStack))
            {
                uniqueStacks.Add(stack.Reward, stack);
                continue;
            }

            long mergedAmount = (long)existingStack.Amount + stack.Amount;
            existingStack.SetAmount(ClampToInt(mergedAmount));
            itemStacks.RemoveAt(i);
        }
    }

    /// <summary>
    /// 保存当前背包和已存在的商店数据。
    /// </summary>
    /// <returns>保存成功返回 true。</returns>
    private bool SaveCurrentState()
    {
        EnsureInitializedWithoutRecursion();

        GameSaveData candidate = GameSaveService.Clone(GameSaveService.GetOrCreateData()); // 候选存档。
        WriteToSaveData(candidate);

        if (GameSaveService.TrySave(candidate))
            return true;

        Debug.LogWarning("玩家背包保存失败，已由调用方恢复内存快照。", this);
        return false;
    }

    /// <summary>
    /// 确保背包完成初始化，避免外部脚本在 Awake（唤醒）顺序变化时读取空数据。
    /// </summary>
    private void EnsureInitialized()
    {
        if (!isInitialized)
            InitializeFromSave();
    }

    /// <summary>
    /// 保存时避免递归调用 InitializeFromSave（从存档初始化）。
    /// </summary>
    private void EnsureInitializedWithoutRecursion()
    {
        if (itemStacks == null)
            itemStacks = new List<InventoryItemStack>();
    }

    /// <summary>
    /// 如果没有手动绑定金币，尝试从注册表中寻找 gold（金币）奖励。
    /// </summary>
    private void ResolveGoldReward()
    {
        if (goldReward != null || rewardRegistry == null)
            return;

        goldReward = rewardRegistry.FindById("gold");
    }

    /// <summary>
    /// 在背包列表中查找指定奖励的数量记录。
    /// </summary>
    /// <param name="reward">要查找的奖励。</param>
    /// <returns>找到时返回数量记录，否则返回 null。</returns>
    private InventoryItemStack FindStack(RewardSO reward)
    {
        if (reward == null || itemStacks == null)
            return null;

        for (int i = 0; i < itemStacks.Count; i++)
        {
            InventoryItemStack currentStack = itemStacks[i]; // 当前正在检查的奖励记录。

            if (currentStack != null && currentStack.Reward == reward)
                return currentStack;
        }

        return null;
    }

    /// <summary>
    /// 将长整数数量安全限制为 int（32 位整数）。
    /// </summary>
    /// <param name="amount">待转换数量。</param>
    /// <returns>安全的非负整数数量。</returns>
    private static int ClampToInt(long amount)
    {
        return (int)Math.Max(0L, Math.Min((long)GameSaveService.MaxQuantity, amount));
    }

    /// <summary>
    /// 在 Inspector（检视面板）修改初始金币时保证数值合法。
    /// </summary>
    private void OnValidate()
    {
        newGameStartingGold = Mathf.Clamp(newGameStartingGold, 0, GameSaveService.MaxQuantity);
    }
}
