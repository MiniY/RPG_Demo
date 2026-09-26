using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 商店商品分类类型，用于 ModeTabs（模式标签）按商品用途切换列表。
/// </summary>
public enum ShopProductCategoryType
{
    /// <summary>
    /// Food（食物）：恢复类或可食用商品。
    /// </summary>
    Food,

    /// <summary>
    /// Weapon（武器）：武器类商品。
    /// </summary>
    Weapon,

    /// <summary>
    /// Skill（技能）：技能或能力类商品。
    /// </summary>
    Skill,

    /// <summary>
    /// Material（材料）：制作、强化或任务材料。
    /// </summary>
    Material,

    /// <summary>
    /// Other（其他）：暂时无法归类的商品。
    /// </summary>
    Other
}

/// <summary>
/// 商店商品配置，保存一个商品的奖励数据、金币单价和初始库存。
/// </summary>
[Serializable]
public class ShopCatalogProduct
{
    /// <summary>
    /// 商品持久化 ID，用作本地库存存档中的稳定键。
    /// </summary>
    [SerializeField, HideInInspector] private string productId; // 商品持久化 ID。

    /// <summary>
    /// 玩家购买后获得的奖励数据。
    /// </summary>
    [SerializeField] private RewardSO reward; // 商品奖励数据。

    /// <summary>
    /// 单个商品需要消耗的金币数量。
    /// </summary>
    [SerializeField, Min(0)] private int unitPrice = 1; // 商品单价。

    /// <summary>
    /// 商人初始拥有的商品数量。
    /// </summary>
    [SerializeField, Min(0)] private int initialStock = 1; // 初始库存。

    /// <summary>
    /// 每次点击购买时实际加入玩家背包的数量，与拾取数量倍率相互独立。
    /// </summary>
    [SerializeField, Min(1)] private int purchaseAmount = 1; // 单次购买数量。

    /// <summary>
    /// 是否无限库存，无限库存不会因为购买而减少。
    /// </summary>
    [SerializeField] private bool infiniteStock; // 是否无限库存。

    /// <summary>
    /// 商店中显示的专用说明，留空时使用 RewardSO（奖励数据）的说明。
    /// </summary>
    [SerializeField, TextArea(2, 5)] private string descriptionOverride; // 商店专用说明。

    /// <summary>
    /// 对外提供商品持久化 ID，只读。
    /// </summary>
    public string ProductId => productId;

    /// <summary>
    /// 对外提供商品奖励数据，只读。
    /// </summary>
    public RewardSO Reward => reward;

    /// <summary>
    /// 对外提供商品单价，只读。
    /// </summary>
    public int UnitPrice => unitPrice;

    /// <summary>
    /// 对外提供初始库存，只读。
    /// </summary>
    public int InitialStock => initialStock;

    /// <summary>
    /// 对外提供单次购买数量，只读。
    /// </summary>
    public int PurchaseAmount => Mathf.Max(1, purchaseAmount);

    /// <summary>
    /// 对外提供是否无限库存，只读。
    /// </summary>
    public bool InfiniteStock => infiniteStock;

    /// <summary>
    /// 获取商品显示名称。
    /// </summary>
    public string DisplayName => reward == null ? "Empty Product" : reward.rewardName;

    /// <summary>
    /// 获取商品显示图标。
    /// </summary>
    public Sprite Icon => reward == null ? null : reward.itemIcon;

    /// <summary>
    /// 获取商品说明文本。
    /// </summary>
    public string Description => string.IsNullOrWhiteSpace(descriptionOverride) ? GetRewardDescription() : descriptionOverride;

    /// <summary>
    /// 判断商品配置是否可用。
    /// </summary>
    public bool IsValid => reward != null;

    /// <summary>
    /// 在 Inspector 修改数据时保证数值合法。
    /// </summary>
    public void Validate()
    {
        EnsurePersistentId();
        unitPrice = Mathf.Max(0, unitPrice);
        initialStock = Mathf.Max(0, initialStock);
        purchaseAmount = Mathf.Max(1, purchaseAmount);
    }

    /// <summary>
    /// 在商品没有持久化 ID 时生成一个稳定 ID。
    /// </summary>
    internal void EnsurePersistentId()
    {
        if (string.IsNullOrWhiteSpace(productId))
            RegeneratePersistentId();
    }

    /// <summary>
    /// 重新生成商品持久化 ID，用于修复重复 ID。
    /// </summary>
    internal void RegeneratePersistentId()
    {
        productId = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// 获取奖励数据中的默认说明。
    /// </summary>
    /// <returns>奖励说明文本。</returns>
    private string GetRewardDescription()
    {
        if (reward == null || string.IsNullOrWhiteSpace(reward.description))
            return "No description.";

        return reward.description;
    }
}

/// <summary>
/// 商店商品分类，保存一个分类标签下的所有商品。
/// </summary>
[Serializable]
public class ShopCatalogCategory
{
    /// <summary>
    /// 分类类型，用于代码切换商品列表。
    /// </summary>
    [SerializeField] private ShopProductCategoryType categoryType = ShopProductCategoryType.Food; // 分类类型。

    /// <summary>
    /// 分类在 UI 中显示的名称。
    /// </summary>
    [SerializeField] private string displayName = "Food"; // 分类显示名称。

    /// <summary>
    /// 分类图标，当前可选，后续可以用于标签按钮显示。
    /// </summary>
    [SerializeField] private Sprite icon; // 分类图标。

    /// <summary>
    /// 当前分类下的商品列表。
    /// </summary>
    [SerializeField] private List<ShopCatalogProduct> products = new List<ShopCatalogProduct>(); // 商品列表。

    /// <summary>
    /// 对外提供分类类型，只读。
    /// </summary>
    public ShopProductCategoryType CategoryType => categoryType;

    /// <summary>
    /// 对外提供分类显示名称，只读。
    /// </summary>
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? categoryType.ToString() : displayName;

    /// <summary>
    /// 对外提供分类图标，只读。
    /// </summary>
    public Sprite Icon => icon;

    /// <summary>
    /// 对外提供商品列表，只读。
    /// </summary>
    public IReadOnlyList<ShopCatalogProduct> Products => products;

    /// <summary>
    /// 在 Inspector 修改数据时保证分类和商品数据合法。
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = categoryType.ToString();

        for (int i = 0; i < products.Count; i++)
        {
            ShopCatalogProduct product = products[i]; // 当前校验的商品。

            if (product != null)
                product.Validate();
        }
    }
}

/// <summary>
/// 商店目录数据，保存商店名称和按分类组织的商品配置。
/// </summary>
[CreateAssetMenu(fileName = "ShopCatalog_", menuName = "RPG/Shop/Shop Catalog")]
public class ShopCatalogSO : ScriptableObject
{
    /// <summary>
    /// 商店目录持久化 ID，用于区分不同商人的本地库存文件。
    /// </summary>
    [SerializeField, HideInInspector] private string catalogId; // 商店目录持久化 ID。

    /// <summary>
    /// 当前商店的显示名称。
    /// </summary>
    [SerializeField] private string shopDisplayName = "Merchant Shop"; // 商店显示名称。

    /// <summary>
    /// 当前商店拥有的商品分类列表。
    /// </summary>
    [SerializeField] private List<ShopCatalogCategory> categories = new List<ShopCatalogCategory>(); // 商品分类列表。

    /// <summary>
    /// 对外提供商店目录持久化 ID，只读。
    /// </summary>
    public string CatalogId => catalogId;

    /// <summary>
    /// 对外提供商店显示名称，只读。
    /// </summary>
    public string ShopDisplayName => shopDisplayName;

    /// <summary>
    /// 对外提供所有商品分类，只读。
    /// </summary>
    public IReadOnlyList<ShopCatalogCategory> Categories => categories;

    /// <summary>
    /// 根据分类类型获取商品列表。
    /// </summary>
    /// <param name="categoryType">目标商品分类。</param>
    /// <returns>该分类下的商品列表。</returns>
    public IReadOnlyList<ShopCatalogProduct> GetProducts(ShopProductCategoryType categoryType)
    {
        ShopCatalogCategory category = GetCategory(categoryType); // 目标分类。
        return category == null ? Array.Empty<ShopCatalogProduct>() : category.Products;
    }

    /// <summary>
    /// 根据分类类型获取分类显示名称。
    /// </summary>
    /// <param name="categoryType">目标商品分类。</param>
    /// <returns>分类显示名称。</returns>
    public string GetCategoryDisplayName(ShopProductCategoryType categoryType)
    {
        ShopCatalogCategory category = GetCategory(categoryType); // 目标分类。
        return category == null ? categoryType.ToString() : category.DisplayName;
    }

    /// <summary>
    /// 根据分类类型查找商品分类。
    /// </summary>
    /// <param name="categoryType">目标商品分类。</param>
    /// <returns>找到时返回分类数据，找不到时返回 null。</returns>
    public ShopCatalogCategory GetCategory(ShopProductCategoryType categoryType)
    {
        for (int i = 0; i < categories.Count; i++)
        {
            ShopCatalogCategory category = categories[i]; // 当前检查的分类。

            if (category != null && category.CategoryType == categoryType)
                return category;
        }

        return null;
    }

    /// <summary>
    /// 在 Inspector 修改数据时校验商品分类和商品配置。
    /// </summary>
    private void OnValidate()
    {
        EnsurePersistentId();

        if (string.IsNullOrWhiteSpace(shopDisplayName))
            shopDisplayName = "Merchant Shop";

        EnsureProductPersistentIds();
    }

    /// <summary>
    /// 确保商店目录和其中所有商品都拥有稳定的持久化 ID。
    /// </summary>
    internal void EnsurePersistentIds()
    {
        EnsurePersistentId();
        EnsureProductPersistentIds();
    }

    /// <summary>
    /// 确保当前目录中的商品 ID 存在且互不重复。
    /// </summary>
    private void EnsureProductPersistentIds()
    {
        HashSet<string> productIds = new HashSet<string>(); // 当前目录中已经使用的商品 ID。

        for (int i = 0; i < categories.Count; i++)
        {
            ShopCatalogCategory category = categories[i]; // 当前校验的分类。

            if (category == null)
                continue;

            category.Validate();

            IReadOnlyList<ShopCatalogProduct> products = category.Products; // 当前分类商品列表。

            for (int j = 0; j < products.Count; j++)
            {
                ShopCatalogProduct product = products[j]; // 当前校验的商品。

                if (product == null)
                    continue;

                product.EnsurePersistentId();

                if (!productIds.Add(product.ProductId))
                {
                    product.RegeneratePersistentId();
                    productIds.Add(product.ProductId);
                }
            }
        }
    }

    /// <summary>
    /// 在商店目录没有持久化 ID 时生成一个稳定 ID。
    /// </summary>
    private void EnsurePersistentId()
    {
        if (string.IsNullOrWhiteSpace(catalogId))
            catalogId = Guid.NewGuid().ToString("N");
    }
}
