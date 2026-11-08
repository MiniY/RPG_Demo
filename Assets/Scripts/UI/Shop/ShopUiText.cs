using System;
using TMPro;
using UnityEngine;

/// <summary>商店专用中文表现适配，不改变 RewardId、目录、价格或存档。</summary>
public static class ShopUiText
{
    public static void SetFittedName(TMP_Text text, string value)
    {
        if (text == null) return;
        if (!text.enableAutoSizing)
        {
            // Keep short names at their authored size; shrink only when necessary.
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = Mathf.Min(text.fontSizeMin, text.fontSizeMax);
            text.enableAutoSizing = true;
        }
        text.enableWordWrapping = false;
        text.text = value;
    }
    public static string Category(ShopProductCategoryType category)
    {
        switch (category)
        {
            case ShopProductCategoryType.Food: return "食物";
            case ShopProductCategoryType.Weapon: return "武器";
            case ShopProductCategoryType.Skill: return "技能";
            case ShopProductCategoryType.Material: return "材料";
            default: return "其他";
        }
    }

    public static string ShopName(ShopCatalogSO catalog) => ChineseOr(catalog == null ? null : catalog.ShopDisplayName, "商店");
    public static string Name(ShopCatalogProduct product)
    {
        if (product == null || product.Reward == null) return "未命名商品";
        // 当前真实目录已经提供中文；仅对已核实的稳定奖励 ID 提供兼容翻译。
        if (IsChinese(product.DisplayName)) return product.DisplayName;
        if (string.Equals(product.Reward.RewardId, "meat", StringComparison.Ordinal)) return "肉";
        return "未命名商品";
    }

    public static string Description(ShopCatalogProduct product) =>
        ChineseOr(product == null ? null : product.Description, "暂无说明");
    public static string Price(ShopCatalogProduct product) => product == null ? "价格：--" : $"价格：{product.UnitPrice} 金币";
    public static string Stock(ShopCatalogProduct product, ShopTradeController trade)
    {
        if (product == null) return "库存：0";
        if (product.InfiniteStock) return "库存：无限";
        int stock = trade == null ? product.InitialStock : trade.GetCurrentStock(product);
        return $"库存：{Math.Max(0, stock)}";
    }

    public static string ChineseOr(string value, string fallback) => IsChinese(value) ? value : fallback;
    public static bool IsChinese(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        bool hasChinese = false;
        foreach (char character in value)
        {
            if ((character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z')) return false;
            hasChinese |= character >= '\u3400' && character <= '\u9fff';
        }
        return hasChinese;
    }
}
