using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 商店购买失败原因，用于区分售罄、金币不足和保存失败。
/// </summary>
public enum ShopPurchaseFailReason
{
    /// <summary>
    /// None（无）：没有失败。
    /// </summary>
    None,

    /// <summary>
    /// InvalidProduct（无效商品）：商品配置缺失或没有奖励数据。
    /// </summary>
    InvalidProduct,

    /// <summary>
    /// SoldOut（售罄）：商品当前库存为 0。
    /// </summary>
    SoldOut,

    /// <summary>
    /// MissingInventory（缺少背包）：没有绑定玩家背包。
    /// </summary>
    MissingInventory,

    /// <summary>
    /// MissingGoldReward（缺少金币数据）：没有绑定 Gold（金币）奖励数据。
    /// </summary>
    MissingGoldReward,

    /// <summary>
    /// NotEnoughGold（金币不足）：玩家金币数量不足。
    /// </summary>
    NotEnoughGold,

    /// <summary>
    /// InventoryFull（背包数量达到上限）：无法安全加入购买的商品。
    /// </summary>
    InventoryFull,

    /// <summary>
    /// SaveFailed（保存失败）：交易无法安全写入本地存档。
    /// </summary>
    SaveFailed
}

/// <summary>
/// 商店购买结果，保存一次购买是否成功、失败原因和提示文本。
/// </summary>
public class ShopPurchaseResult
{
    /// <summary>
    /// 本次购买是否成功。
    /// </summary>
    public bool succeeded; // 是否购买成功。

    /// <summary>
    /// 本次购买失败原因。
    /// </summary>
    public ShopPurchaseFailReason failReason; // 失败原因。

    /// <summary>
    /// 本次购买对应的商品。
    /// </summary>
    public ShopCatalogProduct product; // 商品数据。

    /// <summary>
    /// 本次购买后商品剩余库存。
    /// </summary>
    public int currentStock; // 当前库存。

    /// <summary>
    /// 给 UI（用户界面）显示的提示文本。
    /// </summary>
    public string message; // 提示文本。

    /// <summary>
    /// 创建购买结果。
    /// </summary>
    /// <param name="isSucceeded">是否购买成功。</param>
    /// <param name="reason">失败原因。</param>
    /// <param name="sourceProduct">商品数据。</param>
    /// <param name="stock">当前库存。</param>
    /// <param name="text">提示文本。</param>
    public ShopPurchaseResult(
        bool isSucceeded,
        ShopPurchaseFailReason reason,
        ShopCatalogProduct sourceProduct,
        int stock,
        string text)
    {
        succeeded = isSucceeded;
        failReason = reason;
        product = sourceProduct;
        currentStock = stock;
        message = text;
    }
}

/// <summary>
/// 商店交易控制器，负责金币购买、库存减少和原子交易保存。
/// </summary>
public class ShopTradeController : MonoBehaviour
{
    /// <summary>
    /// 购买成功时发出的事件。
    /// </summary>
    public event Action<ShopPurchaseResult> OnPurchaseSucceeded;

    /// <summary>
    /// 购买失败时发出的事件。
    /// </summary>
    public event Action<ShopPurchaseResult> OnPurchaseFailed;

    /// <summary>
    /// 购买相关状态变化时发出的事件，例如金币或库存变化。
    /// </summary>
    public event Action OnTradeStateChanged;

    /// <summary>
    /// 当前商店使用的商品目录，用于初始化运行时库存。
    /// </summary>
    [SerializeField] private ShopCatalogSO shopCatalog; // 商店目录数据。

    /// <summary>
    /// 玩家运行时背包，用来扣除金币和添加商品。
    /// </summary>
    [SerializeField] private PlayerInventory playerInventory; // 玩家背包。

    /// <summary>
    /// Gold（金币）奖励数据，所有商品目前都用它支付。
    /// </summary>
    [SerializeField] private RewardSO goldReward; // 金币奖励数据。

    /// <summary>
    /// 是否在缺少玩家背包引用时自动查找场景中的运行时背包。
    /// </summary>
    [SerializeField] private bool autoFindPlayerInventory = true; // 是否自动查找玩家背包。

    /// <summary>
    /// 兼容旧 Inspector（检视面板）字段；推荐保持关闭，库存只在显式重置时恢复初始值。
    /// </summary>
    [SerializeField] private bool resetStockOnEnable = false; // 启用时是否重置库存。

    /// <summary>
    /// 商品运行时库存，键是商品配置，值是当前剩余库存。
    /// </summary>
    private readonly Dictionary<ShopCatalogProduct, int> runtimeStocks =
        new Dictionary<ShopCatalogProduct, int>(); // 运行时库存表。

    /// <summary>
    /// 当前运行时库存是否已经初始化。
    /// </summary>
    private bool hasInitializedRuntimeStocks; // 是否已经初始化库存。

    /// <summary>
    /// 当前已经订阅库存变化事件的玩家背包。
    /// </summary>
    private PlayerInventory subscribedInventory; // 已订阅的玩家背包。

    /// <summary>
    /// 对外提供金币奖励数据，只读。
    /// </summary>
    public RewardSO GoldReward => goldReward;

    /// <summary>
    /// 启用时查找背包、订阅背包变化，并加载持久化库存。
    /// </summary>
    private void OnEnable()
    {
        ResolvePlayerInventory();
        SubscribeInventory();

        if (resetStockOnEnable)
            ResetRuntimeStocks();
        else
            InitializeRuntimeStocksIfNeeded();
    }

    /// <summary>
    /// 禁用时保存库存并取消订阅玩家背包变化事件。
    /// </summary>
    private void OnDisable()
    {
        SaveRuntimeStocks();
        UnsubscribeInventory();
    }

    /// <summary>
    /// 应用暂停时保存商品库存。
    /// </summary>
    /// <param name="pauseStatus">应用是否进入暂停状态。</param>
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            SaveRuntimeStocks();
    }

    /// <summary>
    /// 应用退出时保存商品库存。
    /// </summary>
    private void OnApplicationQuit()
    {
        SaveRuntimeStocks();
    }

    /// <summary>
    /// 设置当前商店目录，并重建运行时库存。
    /// </summary>
    /// <param name="catalog">新的商店目录。</param>
    public void SetShopCatalog(ShopCatalogSO catalog)
    {
        if (shopCatalog == catalog && hasInitializedRuntimeStocks)
            return;

        if (hasInitializedRuntimeStocks && shopCatalog != null)
            SaveRuntimeStocks();

        shopCatalog = catalog;
        hasInitializedRuntimeStocks = false;
        runtimeStocks.Clear();
        InitializeRuntimeStocksIfNeeded();
        OnTradeStateChanged?.Invoke();
    }

    /// <summary>
    /// 设置玩家背包引用，适合玩家对象动态生成后主动绑定。
    /// </summary>
    /// <param name="inventory">新的玩家背包。</param>
    public void SetPlayerInventory(PlayerInventory inventory)
    {
        UnsubscribeInventory();
        playerInventory = IsRuntimeInventory(inventory) ? inventory : null;
        SyncGoldRewardFromInventory();
        SubscribeInventory();
        OnTradeStateChanged?.Invoke();
    }

    /// <summary>
    /// 设置 Gold（金币）奖励数据。
    /// </summary>
    /// <param name="reward">金币奖励数据。</param>
    public void SetGoldReward(RewardSO reward)
    {
        // 运行时背包存在时，货币必须以背包配置为准，避免 UI（用户界面）和商店引用不同金币资产。
        if (IsRuntimeInventory(playerInventory))
            SyncGoldRewardFromInventory();
        else
            goldReward = reward;

        OnTradeStateChanged?.Invoke();
    }

    /// <summary>
    /// 获取商品当前剩余库存。
    /// </summary>
    /// <param name="product">目标商品。</param>
    /// <returns>无限库存返回 int.MaxValue，否则返回剩余库存。</returns>
    public int GetCurrentStock(ShopCatalogProduct product)
    {
        if (product == null)
            return 0;

        if (product.InfiniteStock)
            return int.MaxValue;

        EnsureProductStock(product);
        return runtimeStocks.TryGetValue(product, out int stock) ? stock : 0;
    }

    /// <summary>
    /// 获取商品库存显示文本。
    /// </summary>
    /// <param name="product">目标商品。</param>
    /// <returns>库存文本。</returns>
    public string GetStockText(ShopCatalogProduct product)
    {
        if (product == null)
            return "剩余 0 个";

        if (product.InfiniteStock)
            return "剩余 ∞ 个";

        return "剩余 " + GetCurrentStock(product) + " 个";
    }

    /// <summary>
    /// 判断指定商品当前是否可以购买。
    /// </summary>
    /// <param name="product">要检查的商品。</param>
    /// <param name="message">不可购买时输出提示。</param>
    /// <returns>可以购买返回 true，否则返回 false。</returns>
    public bool CanPurchase(ShopCatalogProduct product, out string message)
    {
        ResolvePlayerInventory();
        SubscribeInventory();

        if (playerInventory != null)
            playerInventory.EnsureInitializedForRuntime();

        ShopPurchaseResult result = EvaluatePurchase(product); // 购买检查结果。
        message = result.message;
        return result.succeeded;
    }

    /// <summary>
    /// 尝试购买指定商品；金币、商品和商店库存使用一次原子提交。
    /// </summary>
    /// <param name="product">要购买的商品。</param>
    /// <returns>购买结果。</returns>
    public ShopPurchaseResult TryPurchase(ShopCatalogProduct product)
    {
        ResolvePlayerInventory();
        SubscribeInventory();
        InitializeRuntimeStocksIfNeeded();

        if (playerInventory != null)
            playerInventory.EnsureInitializedForRuntime();

        ShopPurchaseResult checkResult = EvaluatePurchase(product); // 购买前检查结果。

        if (!checkResult.succeeded)
        {
            OnPurchaseFailed?.Invoke(checkResult);
            OnTradeStateChanged?.Invoke();
            return checkResult;
        }

        PlayerInventorySnapshot inventorySnapshot = playerInventory.CaptureSnapshot(); // 背包快照。
        int previousStock = GetCurrentStock(product); // 交易前库存。

        if (product.UnitPrice > 0 &&
            !playerInventory.TryRemoveItemWithoutPersistence(goldReward, product.UnitPrice))
        {
            ShopPurchaseResult failResult = CreateFailure(
                product,
                ShopPurchaseFailReason.NotEnoughGold,
                "金币数量不足");
            OnPurchaseFailed?.Invoke(failResult);
            OnTradeStateChanged?.Invoke();
            return failResult;
        }

        if (!playerInventory.TryAddItemWithoutPersistence(product.Reward, product.PurchaseAmount))
        {
            playerInventory.RestoreSnapshot(inventorySnapshot, false);

            ShopPurchaseResult failResult = CreateFailure(
                product,
                ShopPurchaseFailReason.InventoryFull,
                "背包数量已达上限");
            OnPurchaseFailed?.Invoke(failResult);
            OnTradeStateChanged?.Invoke();
            return failResult;
        }

        DecreaseStock(product);

        GameSaveData candidate = GameSaveService.Clone(GameSaveService.GetOrCreateData()); // 候选存档。
        playerInventory.WriteToSaveData(candidate);
        WriteRuntimeStocksToSaveData(candidate);

        if (!GameSaveService.TrySave(candidate))
        {
            playerInventory.RestoreSnapshot(inventorySnapshot, false);

            if (!product.InfiniteStock)
                runtimeStocks[product] = previousStock;

            ShopPurchaseResult failResult = CreateFailure(
                product,
                ShopPurchaseFailReason.SaveFailed,
                "交易保存失败，请重试");
            OnPurchaseFailed?.Invoke(failResult);
            OnTradeStateChanged?.Invoke();
            return failResult;
        }

        playerInventory.NotifyInventoryChanged();

        ShopPurchaseResult successResult = new ShopPurchaseResult(
            true,
            ShopPurchaseFailReason.None,
            product,
            GetCurrentStock(product),
            "购买成功");
        OnPurchaseSucceeded?.Invoke(successResult);
        OnTradeStateChanged?.Invoke();
        return successResult;
    }

    /// <summary>
    /// 重新初始化所有商品的运行时库存，并保存新库存。
    /// </summary>
    public void ResetRuntimeStocks()
    {
        InitializeRuntimeStocksIfNeeded();

        Dictionary<ShopCatalogProduct, int> previousStocks =
            new Dictionary<ShopCatalogProduct, int>(runtimeStocks); // 重置前库存。

        runtimeStocks.Clear();
        InitializeRuntimeStocksFromCatalog();
        hasInitializedRuntimeStocks = true;

        if (SaveRuntimeStocks())
            return;

        runtimeStocks.Clear();

        foreach (KeyValuePair<ShopCatalogProduct, int> pair in previousStocks)
            runtimeStocks[pair.Key] = pair.Value;
    }

    /// <summary>
    /// 保存当前商店库存和当前玩家背包到统一存档。
    /// </summary>
    /// <returns>保存成功时返回 true，否则返回 false。</returns>
    public bool SaveRuntimeStocks()
    {
        ResolvePlayerInventory();

        if (shopCatalog == null)
            return false;

        InitializeRuntimeStocksIfNeeded();
        shopCatalog.EnsurePersistentIds();

        GameSaveData candidate = GameSaveService.Clone(GameSaveService.GetOrCreateData()); // 候选存档。

        if (playerInventory != null)
        {
            playerInventory.EnsureInitializedForRuntime();
            SyncGoldRewardFromInventory();
            playerInventory.WriteToSaveData(candidate);
        }

        WriteRuntimeStocksToSaveData(candidate);
        return GameSaveService.TrySave(candidate);
    }

    /// <summary>
    /// 检查一次购买是否满足商品、库存、背包和金币条件。
    /// </summary>
    /// <param name="product">要检查的商品。</param>
    /// <returns>购买检查结果。</returns>
    private ShopPurchaseResult EvaluatePurchase(ShopCatalogProduct product)
    {
        if (product == null || !product.IsValid || product.PurchaseAmount <= 0)
            return CreateFailure(product, ShopPurchaseFailReason.InvalidProduct, "商品配置无效");

        if (!product.InfiniteStock && GetCurrentStock(product) <= 0)
            return CreateFailure(product, ShopPurchaseFailReason.SoldOut, "已售罄");

        if (playerInventory == null)
            return CreateFailure(product, ShopPurchaseFailReason.MissingInventory, "缺少玩家背包引用");

        if (goldReward == null)
            return CreateFailure(product, ShopPurchaseFailReason.MissingGoldReward, "缺少金币数据");

        if (product.UnitPrice > 0 &&
            playerInventory.GetItemAmountLong(goldReward) < product.UnitPrice)
        {
            return CreateFailure(product, ShopPurchaseFailReason.NotEnoughGold, "金币数量不足");
        }

        return new ShopPurchaseResult(
            true,
            ShopPurchaseFailReason.None,
            product,
            GetCurrentStock(product),
            string.Empty);
    }

    /// <summary>
    /// 创建购买失败结果。
    /// </summary>
    /// <param name="product">购买失败的商品。</param>
    /// <param name="reason">失败原因。</param>
    /// <param name="message">提示文本。</param>
    /// <returns>购买失败结果。</returns>
    private ShopPurchaseResult CreateFailure(
        ShopCatalogProduct product,
        ShopPurchaseFailReason reason,
        string message)
    {
        return new ShopPurchaseResult(
            false,
            reason,
            product,
            GetCurrentStock(product),
            message);
    }

    /// <summary>
    /// 减少指定商品的一件库存；购买数量由商品配置单独决定。
    /// </summary>
    /// <param name="product">刚刚被购买的商品。</param>
    private void DecreaseStock(ShopCatalogProduct product)
    {
        if (product == null || product.InfiniteStock)
            return;

        EnsureProductStock(product);
        runtimeStocks[product] = Mathf.Max(0, runtimeStocks[product] - 1);
    }

    /// <summary>
    /// 如果运行时库存没有记录该商品，则写入初始库存。
    /// </summary>
    /// <param name="product">目标商品。</param>
    private void EnsureProductStock(ShopCatalogProduct product)
    {
        InitializeRuntimeStocksIfNeeded();

        if (product == null || product.InfiniteStock || runtimeStocks.ContainsKey(product))
            return;

        runtimeStocks[product] = Mathf.Max(0, product.InitialStock);
    }

    /// <summary>
    /// 初始化商店目录下所有商品的运行时库存，并读取统一存档。
    /// </summary>
    private void InitializeRuntimeStocksIfNeeded()
    {
        if (hasInitializedRuntimeStocks)
            return;

        runtimeStocks.Clear();

        if (shopCatalog == null)
        {
            hasInitializedRuntimeStocks = true;
            return;
        }

        shopCatalog.EnsurePersistentIds();
        InitializeRuntimeStocksFromCatalog();

        GameSaveData data = GameSaveService.GetOrCreateData(); // 统一存档。
        GameSaveShopData savedShop = GameSaveService.FindShop(data, shopCatalog.CatalogId); // 当前商店存档。

        if (savedShop != null)
            ApplySavedStocks(savedShop);

        hasInitializedRuntimeStocks = true;
    }

    /// <summary>
    /// 按商店目录中的初始库存创建运行时库存。
    /// </summary>
    private void InitializeRuntimeStocksFromCatalog()
    {
        if (shopCatalog == null)
            return;

        IReadOnlyList<ShopCatalogCategory> categories = shopCatalog.Categories; // 当前分类列表。

        for (int i = 0; i < categories.Count; i++)
        {
            ShopCatalogCategory category = categories[i]; // 当前分类。

            if (category == null)
                continue;

            IReadOnlyList<ShopCatalogProduct> products = category.Products; // 当前分类商品。

            for (int j = 0; j < products.Count; j++)
            {
                ShopCatalogProduct product = products[j]; // 当前商品。

                if (product != null && !product.InfiniteStock)
                    runtimeStocks[product] = Mathf.Max(0, product.InitialStock);
            }
        }
    }

    /// <summary>
    /// 将统一存档中的商店库存应用到当前目录。
    /// </summary>
    /// <param name="savedShop">当前商店的存档。</param>
    private void ApplySavedStocks(GameSaveShopData savedShop)
    {
        if (savedShop == null || savedShop.productStocks == null || shopCatalog == null)
            return;

        Dictionary<string, long> savedQuantities =
            new Dictionary<string, long>(StringComparer.Ordinal); // 商品 ID 到库存数量。

        for (int i = 0; i < savedShop.productStocks.Count; i++)
        {
            GameSaveShopStockEntry entry = savedShop.productStocks[i];

            if (entry == null || string.IsNullOrWhiteSpace(entry.productId))
                continue;

            savedQuantities[entry.productId] = entry.quantity;
        }

        IReadOnlyList<ShopCatalogCategory> categories = shopCatalog.Categories; // 当前分类列表。

        for (int i = 0; i < categories.Count; i++)
        {
            ShopCatalogCategory category = categories[i]; // 当前分类。

            if (category == null)
                continue;

            IReadOnlyList<ShopCatalogProduct> products = category.Products; // 当前商品列表。

            for (int j = 0; j < products.Count; j++)
            {
                ShopCatalogProduct product = products[j]; // 当前商品。

                if (product == null || product.InfiniteStock)
                    continue;

                if (savedQuantities.TryGetValue(product.ProductId, out long savedQuantity))
                {
                    int safeQuantity = (int)Math.Max(
                        0L,
                        Math.Min((long)product.InitialStock, savedQuantity));
                    runtimeStocks[product] = safeQuantity;
                }
            }
        }
    }

    /// <summary>
    /// 将当前运行时库存写入候选存档，不执行磁盘写入。
    /// </summary>
    /// <param name="data">要修改的候选存档。</param>
    private void WriteRuntimeStocksToSaveData(GameSaveData data)
    {
        if (data == null || shopCatalog == null)
            return;

        shopCatalog.EnsurePersistentIds();
        GameSaveShopData savedShop = GameSaveService.GetOrCreateShop(data, shopCatalog.CatalogId);

        if (savedShop == null)
            return;

        savedShop.productStocks.Clear();

        IReadOnlyList<ShopCatalogCategory> categories = shopCatalog.Categories; // 当前分类列表。

        for (int i = 0; i < categories.Count; i++)
        {
            ShopCatalogCategory category = categories[i]; // 当前分类。

            if (category == null)
                continue;

            IReadOnlyList<ShopCatalogProduct> products = category.Products; // 当前商品列表。

            for (int j = 0; j < products.Count; j++)
            {
                ShopCatalogProduct product = products[j]; // 当前商品。

                if (product == null || product.InfiniteStock)
                    continue;

                EnsureProductStock(product);
                savedShop.productStocks.Add(new GameSaveShopStockEntry
                {
                    productId = product.ProductId,
                    quantity = GetCurrentStock(product)
                });
            }
        }
    }

    /// <summary>
    /// 尝试自动查找场景中的运行时玩家背包。
    /// </summary>
    private void ResolvePlayerInventory()
    {
        if (IsRuntimeInventory(playerInventory))
        {
            SyncGoldRewardFromInventory();
            return;
        }

        // Prefab（预制体）资产不是运行时玩家，不能作为交易数据源。
        playerInventory = null;

        if (!autoFindPlayerInventory)
            return;

#pragma warning disable CS0618
        playerInventory = FindObjectOfType<PlayerInventory>();
#pragma warning restore CS0618

        SyncGoldRewardFromInventory();
    }

    /// <summary>
    /// 判断背包是否是当前场景中的运行时实例，而不是 Project（项目）窗口中的 prefab（预制体）资产。
    /// </summary>
    /// <param name="inventory">要检查的背包对象。</param>
    /// <returns>属于有效场景实例时返回 true。</returns>
    private static bool IsRuntimeInventory(PlayerInventory inventory)
    {
        return inventory != null &&
               inventory.gameObject != null &&
               inventory.gameObject.scene.IsValid();
    }

    /// <summary>
    /// 让商店使用运行时背包声明的唯一金币奖励资产。
    /// </summary>
    private void SyncGoldRewardFromInventory()
    {
        if (IsRuntimeInventory(playerInventory))
            goldReward = playerInventory.GoldReward;
    }

    /// <summary>
    /// 订阅玩家背包变化事件。
    /// </summary>
    private void SubscribeInventory()
    {
        if (playerInventory == null || subscribedInventory == playerInventory)
            return;

        UnsubscribeInventory();
        subscribedInventory = playerInventory;
        subscribedInventory.OnInventoryChanged += HandleInventoryChanged;
    }

    /// <summary>
    /// 取消订阅玩家背包变化事件。
    /// </summary>
    private void UnsubscribeInventory()
    {
        if (subscribedInventory == null)
            return;

        subscribedInventory.OnInventoryChanged -= HandleInventoryChanged;
        subscribedInventory = null;
    }

    /// <summary>
    /// 处理背包变化，并通知商店 UI（用户界面）刷新购买状态。
    /// </summary>
    private void HandleInventoryChanged()
    {
        OnTradeStateChanged?.Invoke();
    }
}
