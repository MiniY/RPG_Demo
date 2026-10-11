using System;
using System.Collections.Generic;

/// <summary>
/// 在现有运行组件与 Tower Run 纯数据记录之间进行完整、隔离的捕获和应用。
/// </summary>
public static class TowerRunRuntimeStateBridge
{
    public static bool TryCapture(
        string runId,
        string playerName,
        TowerRunFloorRecord floor,
        TowerRunGenerationRecord generation,
        PlayerStatsRuntime stats,
        PlayerInventory inventory,
        IReadOnlyList<ShopTradeController> runtimeShops,
        out TowerRunCheckpointRecord record,
        out string message)
    {
        record = null;

        if (string.IsNullOrWhiteSpace(runId) || stats == null || inventory == null ||
            floor == null || generation == null)
        {
            message = "运行检查点缺少 Run、楼层、生成或玩家数据。";
            return false;
        }

        PlayerStatsSnapshot snapshot = stats.CaptureSnapshot();
        TowerRunCheckpointRecord candidate = new TowerRunCheckpointRecord
        {
            runId = runId,
            playerName = string.IsNullOrWhiteSpace(playerName) ? "玩家" : playerName.Trim(),
            floor = floor.DeepClone(),
            generation = generation.DeepClone(),
            player = new TowerRunPlayerRecord
            {
                stats = new TowerRunPlayerStatsRecord
                {
                    maxHealth = snapshot.maxHealth,
                    currentHealth = snapshot.currentHealth,
                    maxMana = snapshot.maxMana,
                    currentMana = snapshot.currentMana,
                    maxStamina = snapshot.maxStamina,
                    currentStamina = snapshot.currentStamina
                }
            }
        };

        if (!inventory.TryCaptureCheckpointInventory(candidate.player.inventory, out message))
            return false;

        Dictionary<string, TowerRunShopRecord> shopsByCatalog =
            new Dictionary<string, TowerRunShopRecord>(StringComparer.Ordinal);

        if (!TryCaptureCachedShops(shopsByCatalog, out message))
            return false;

        if (runtimeShops != null)
        {
            for (int i = 0; i < runtimeShops.Count; i++)
            {
                ShopTradeController shop = runtimeShops[i];

                if (shop == null)
                    continue;

                TowerRunShopRecord current = new TowerRunShopRecord();

                if (!shop.TryCaptureCheckpointShop(current, out message))
                    return false;

                if (shopsByCatalog.TryGetValue(current.catalogId, out TowerRunShopRecord cached) &&
                    ContainsUnknownCachedProduct(cached, current))
                {
                    message = $"商店 {current.catalogId} 的旧库存包含当前目录无法解析的 ProductId。";
                    return false;
                }

                shopsByCatalog[current.catalogId] = current;
            }
        }

        List<string> catalogIds = new List<string>(shopsByCatalog.Keys);
        catalogIds.Sort(StringComparer.Ordinal);

        for (int i = 0; i < catalogIds.Count; i++)
            candidate.player.shops.Add(shopsByCatalog[catalogIds[i]].DeepClone());

        record = candidate.DeepClone();
        message = string.Empty;
        return true;
    }

    public static bool TryApply(
        TowerRunCheckpointRecord record,
        PlayerStatsRuntime stats,
        PlayerInventory inventory,
        IReadOnlyList<ShopTradeController> runtimeShops,
        bool notify,
        out string message)
    {
        if (record?.player?.stats == null || stats == null || inventory == null)
        {
            message = "恢复记录缺少完整玩家数据或运行组件。";
            return false;
        }

        PlayerStatsSnapshot previousStats = stats.CaptureSnapshot();
        List<TowerRunInventoryEntry> previousInventory = new List<TowerRunInventoryEntry>();

        if (!inventory.TryCaptureCheckpointInventory(previousInventory, out message))
            return false;

        List<TowerRunShopRecord> previousShops = new List<TowerRunShopRecord>();
        Dictionary<string, TowerRunShopRecord> sourceShops = IndexShops(record.player.shops, out message);

        if (sourceShops == null)
            return false;

        if (runtimeShops != null)
        {
            for (int i = 0; i < runtimeShops.Count; i++)
            {
                ShopTradeController shop = runtimeShops[i];

                if (shop == null || string.IsNullOrWhiteSpace(shop.CatalogId))
                    continue;

                TowerRunShopRecord previous = new TowerRunShopRecord();

                if (!shop.TryCaptureCheckpointShop(previous, out message))
                    return false;

                previousShops.Add(previous);
            }
        }

        PlayerStatsSnapshot nextStats = new PlayerStatsSnapshot
        {
            maxHealth = record.player.stats.maxHealth,
            currentHealth = record.player.stats.currentHealth,
            maxMana = record.player.stats.maxMana,
            currentMana = record.player.stats.currentMana,
            maxStamina = record.player.stats.maxStamina,
            currentStamina = record.player.stats.currentStamina
        };

        stats.RestoreSnapshot(nextStats, false);

        if (!inventory.TryApplyCheckpointInventory(record.player.inventory, false, out message) ||
            !TryApplyRuntimeShops(runtimeShops, sourceShops, false, out message) ||
            !GameSaveService.TryReplaceRuntimeData(ToLegacyRuntimeData(record)))
        {
            stats.RestoreSnapshot(previousStats, false);
            inventory.TryApplyCheckpointInventory(previousInventory, false, out _);
            TryApplyRuntimeShops(runtimeShops, IndexShops(previousShops, out _), false, out _);
            return false;
        }

        if (notify)
        {
            stats.SendStatsChanged();
            inventory.NotifyInventoryChanged();
            NotifyRuntimeShops(runtimeShops);
        }

        message = string.Empty;
        return true;
    }

    private static bool TryCaptureCachedShops(
        Dictionary<string, TowerRunShopRecord> destination,
        out string message)
    {
        GameSaveData data = GameSaveService.Clone(GameSaveService.GetOrCreateData());

        if (data.shops != null)
        {
            for (int i = 0; i < data.shops.Count; i++)
            {
                GameSaveShopData shop = data.shops[i];

                if (shop == null || string.IsNullOrWhiteSpace(shop.catalogId) ||
                    destination.ContainsKey(shop.catalogId))
                {
                    message = "运行数据包含缺失或重复的 CatalogId。";
                    return false;
                }

                TowerRunShopRecord captured = new TowerRunShopRecord { catalogId = shop.catalogId };
                HashSet<string> productIds = new HashSet<string>(StringComparer.Ordinal);

                if (shop.productStocks != null)
                {
                    for (int j = 0; j < shop.productStocks.Count; j++)
                    {
                        GameSaveShopStockEntry stock = shop.productStocks[j];

                        if (stock == null || string.IsNullOrWhiteSpace(stock.productId) ||
                            !productIds.Add(stock.productId) || stock.quantity < 0L)
                        {
                            message = $"商店 {shop.catalogId} 包含无效库存条目。";
                            return false;
                        }

                        captured.productStocks.Add(new TowerRunShopStockRecord
                        {
                            productId = stock.productId,
                            quantity = stock.quantity
                        });
                    }
                }

                destination.Add(captured.catalogId, captured);
            }
        }

        message = string.Empty;
        return true;
    }

    private static bool ContainsUnknownCachedProduct(
        TowerRunShopRecord cached,
        TowerRunShopRecord current)
    {
        HashSet<string> currentIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < current.productStocks.Count; i++)
            currentIds.Add(current.productStocks[i].productId);

        for (int i = 0; i < cached.productStocks.Count; i++)
        {
            if (!currentIds.Contains(cached.productStocks[i].productId))
                return true;
        }

        return false;
    }

    private static Dictionary<string, TowerRunShopRecord> IndexShops(
        IReadOnlyList<TowerRunShopRecord> shops,
        out string message)
    {
        Dictionary<string, TowerRunShopRecord> result =
            new Dictionary<string, TowerRunShopRecord>(StringComparer.Ordinal);

        if (shops != null)
        {
            for (int i = 0; i < shops.Count; i++)
            {
                TowerRunShopRecord shop = shops[i];

                if (shop == null || string.IsNullOrWhiteSpace(shop.catalogId) ||
                    result.ContainsKey(shop.catalogId))
                {
                    message = "恢复记录包含缺失或重复的 CatalogId。";
                    return null;
                }

                result.Add(shop.catalogId, shop);
            }
        }

        message = string.Empty;
        return result;
    }

    private static bool TryApplyRuntimeShops(
        IReadOnlyList<ShopTradeController> runtimeShops,
        Dictionary<string, TowerRunShopRecord> sourceShops,
        bool notify,
        out string message)
    {
        if (runtimeShops != null)
        {
            for (int i = 0; i < runtimeShops.Count; i++)
            {
                ShopTradeController shop = runtimeShops[i];

                if (shop == null)
                    continue;

                bool applied = sourceShops.TryGetValue(
                    shop.CatalogId,
                    out TowerRunShopRecord source)
                    ? shop.TryApplyCheckpointShop(source, notify, out message)
                    : shop.TryInitializeCheckpointShopFromCatalog(notify, out message);

                if (!applied)
                    return false;
            }
        }

        message = string.Empty;
        return true;
    }

    private static void NotifyRuntimeShops(IReadOnlyList<ShopTradeController> runtimeShops)
    {
        if (runtimeShops == null)
            return;

        for (int i = 0; i < runtimeShops.Count; i++)
            runtimeShops[i]?.NotifyCheckpointApplied();
    }

    private static GameSaveData ToLegacyRuntimeData(TowerRunCheckpointRecord record)
    {
        GameSaveData data = GameSaveService.CreateNewData();
        data.playerName = record.playerName;
        data.playerStats = new PlayerStatsSaveData
        {
            maxHealth = record.player.stats.maxHealth,
            currentHealth = record.player.stats.currentHealth,
            maxMana = record.player.stats.maxMana,
            currentMana = record.player.stats.currentMana,
            maxStamina = record.player.stats.maxStamina,
            currentStamina = record.player.stats.currentStamina
        };

        for (int i = 0; i < record.player.inventory.Count; i++)
        {
            TowerRunInventoryEntry entry = record.player.inventory[i];
            data.inventory.Add(new GameSaveInventoryEntry
            {
                rewardId = entry.rewardId,
                amount = entry.amount
            });
        }

        for (int i = 0; i < record.player.shops.Count; i++)
        {
            TowerRunShopRecord source = record.player.shops[i];
            GameSaveShopData shop = new GameSaveShopData { catalogId = source.catalogId };

            for (int j = 0; j < source.productStocks.Count; j++)
            {
                TowerRunShopStockRecord stock = source.productStocks[j];
                shop.productStocks.Add(new GameSaveShopStockEntry
                {
                    productId = stock.productId,
                    quantity = stock.quantity
                });
            }

            data.shops.Add(shop);
        }

        return data;
    }
}
