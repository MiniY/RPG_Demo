using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 一条背包物品存档记录，只保存 RewardId（奖励唯一 ID）和数量，不保存 Unity 对象引用。
/// </summary>
[Serializable]
public sealed class GameSaveInventoryEntry
{
    /// <summary>
    /// 奖励数据资产的稳定 ID。
    /// </summary>
    public string rewardId;

    /// <summary>
    /// 玩家拥有的奖励数量。
    /// </summary>
    public long amount;
}

/// <summary>
/// 一条商店商品库存存档记录。
/// </summary>
[Serializable]
public sealed class GameSaveShopStockEntry
{
    /// <summary>
    /// 商品配置的稳定 ID。
    /// </summary>
    public string productId;

    /// <summary>
    /// 商品当前剩余库存。
    /// </summary>
    public long quantity;
}

/// <summary>
/// 一个商店目录的库存存档。
/// </summary>
[Serializable]
public sealed class GameSaveShopData
{
    /// <summary>
    /// 商店目录的稳定 ID。
    /// </summary>
    public string catalogId;

    /// <summary>
    /// 当前商店的商品库存列表。
    /// </summary>
    public List<GameSaveShopStockEntry> productStocks = new List<GameSaveShopStockEntry>();
}

/// <summary>
/// 玩家生命、魔法和体力的存档数据。
/// </summary>
[Serializable]
public sealed class PlayerStatsSaveData
{
    /// <summary>
    /// 最大生命值。
    /// </summary>
    public int maxHealth = 100;

    /// <summary>
    /// 当前生命值。
    /// </summary>
    public float currentHealth = 100f;

    /// <summary>
    /// 最大魔法值。
    /// </summary>
    public int maxMana = 100;

    /// <summary>
    /// 当前魔法值。
    /// </summary>
    public float currentMana = 100f;

    /// <summary>
    /// 最大体力值。
    /// </summary>
    public int maxStamina = 100;

    /// <summary>
    /// 当前体力值。
    /// </summary>
    public float currentStamina = 100f;
}

/// <summary>
/// 游戏完整存档，统一保存玩家档案、背包和所有商店库存。
/// </summary>
[Serializable]
public sealed class GameSaveData
{
    /// <summary>
    /// 当前存档格式版本。
    /// </summary>
    public int version = GameSaveService.CurrentVersion;

    /// <summary>
    /// 玩家名称。
    /// </summary>
    public string playerName = "玩家";

    /// <summary>
    /// 玩家属性存档数据。
    /// </summary>
    public PlayerStatsSaveData playerStats = new PlayerStatsSaveData();

    /// <summary>
    /// 玩家背包物品列表。
    /// </summary>
    public List<GameSaveInventoryEntry> inventory = new List<GameSaveInventoryEntry>();

    /// <summary>
    /// 所有商人的库存列表。
    /// </summary>
    public List<GameSaveShopData> shops = new List<GameSaveShopData>();
}

/// <summary>
/// 统一游戏存档服务，负责加载、校验和原子保存 JSON（结构化文本）存档。
/// </summary>
public static class GameSaveService
{
    /// <summary>
    /// 当前支持的存档格式版本。
    /// </summary>
    public const int CurrentVersion = 2;

    /// <summary>
    /// 新游戏和存档数据允许保存的最大数量。
    /// </summary>
    public const int MaxQuantity = int.MaxValue;

    /// <summary>
    /// 正式存档文件名。
    /// </summary>
    private const string SaveFileName = "save.json";

    /// <summary>
    /// 正式存档的备份文件名。
    /// </summary>
    private const string BackupFileName = "save.backup.json";

    /// <summary>
    /// 保存过程中使用的临时文件名。
    /// </summary>
    private const string TemporaryFileName = "save.tmp";

    /// <summary>
    /// 当前进程已经缓存的存档数据。
    /// </summary>
    private static GameSaveData cachedData;

    /// <summary>
    /// 当前进程是否已经完成过一次存档加载。
    /// </summary>
    private static bool hasLoaded;

    /// <summary>
    /// 当前存档是否来自一个有效的本地文件。
    /// false 表示需要创建新游戏数据。
    /// </summary>
    public static bool HasLoadedExistingSave { get; private set; }

    /// <summary>
    /// 正式存档的绝对路径。
    /// </summary>
    public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    /// <summary>
    /// 备份存档的绝对路径。
    /// </summary>
    public static string BackupPath => Path.Combine(Application.persistentDataPath, BackupFileName);

    /// <summary>
    /// 临时存档的绝对路径。
    /// </summary>
    public static string TemporaryPath => Path.Combine(Application.persistentDataPath, TemporaryFileName);

    /// <summary>
    /// 在进入新的播放会话时清除静态缓存，兼容关闭 Domain Reload（域重载）的编辑器设置。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        cachedData = null;
        hasLoaded = false;
        HasLoadedExistingSave = false;
    }

    /// <summary>
    /// 获取当前存档；第一次调用时按正式存档、备份存档、新游戏的顺序加载。
    /// </summary>
    /// <returns>当前进程使用的完整存档数据。</returns>
    public static GameSaveData GetOrCreateData()
    {
        EnsureLoaded();
        return cachedData;
    }

    /// <summary>
    /// 将候选数据以原子方式写入正式存档。
    /// </summary>
    /// <param name="data">准备写入的完整存档数据。</param>
    /// <returns>保存成功返回 true，失败返回 false。</returns>
    public static bool TrySave(GameSaveData data)
    {
        if (data == null)
            return false;

        Normalize(data);

        string json;

        try
        {
            data.version = CurrentVersion;
            json = JsonUtility.ToJson(data, true);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"生成游戏存档文本失败：{exception.Message}");
            return false;
        }

        if (!TryWriteAtomically(json))
            return false;

        cachedData = Clone(data);
        hasLoaded = true;
        HasLoadedExistingSave = true;
        return true;
    }

    /// <summary>
    /// 创建一个空的、格式正确的新游戏存档。
    /// </summary>
    /// <returns>空的新游戏存档。</returns>
    public static GameSaveData CreateNewData()
    {
        return new GameSaveData
        {
            version = CurrentVersion,
            playerName = "玩家",
            playerStats = new PlayerStatsSaveData(),
            inventory = new List<GameSaveInventoryEntry>(),
            shops = new List<GameSaveShopData>()
        };
    }

    /// <summary>
    /// 深复制存档数据，用于交易前构建候选状态，避免失败时污染缓存。
    /// </summary>
    /// <param name="source">要复制的存档。</param>
    /// <returns>独立的存档副本。</returns>
    public static GameSaveData Clone(GameSaveData source)
    {
        GameSaveData clone = CreateNewData();

        if (source == null)
            return clone;

        clone.version = source.version;
        clone.playerName = source.playerName;

        if (source.playerStats != null)
        {
            clone.playerStats = new PlayerStatsSaveData
            {
                maxHealth = source.playerStats.maxHealth,
                currentHealth = source.playerStats.currentHealth,
                maxMana = source.playerStats.maxMana,
                currentMana = source.playerStats.currentMana,
                maxStamina = source.playerStats.maxStamina,
                currentStamina = source.playerStats.currentStamina
            };
        }

        if (source.inventory != null)
        {
            for (int i = 0; i < source.inventory.Count; i++)
            {
                GameSaveInventoryEntry entry = source.inventory[i];

                if (entry == null)
                    continue;

                clone.inventory.Add(new GameSaveInventoryEntry
                {
                    rewardId = entry.rewardId,
                    amount = entry.amount
                });
            }
        }

        if (source.shops != null)
        {
            for (int i = 0; i < source.shops.Count; i++)
            {
                GameSaveShopData shop = source.shops[i];

                if (shop == null)
                    continue;

                GameSaveShopData shopClone = new GameSaveShopData
                {
                    catalogId = shop.catalogId
                };

                if (shop.productStocks != null)
                {
                    for (int j = 0; j < shop.productStocks.Count; j++)
                    {
                        GameSaveShopStockEntry stock = shop.productStocks[j];

                        if (stock == null)
                            continue;

                        shopClone.productStocks.Add(new GameSaveShopStockEntry
                        {
                            productId = stock.productId,
                            quantity = stock.quantity
                        });
                    }
                }

                clone.shops.Add(shopClone);
            }
        }

        return clone;
    }

    /// <summary>
    /// 查找指定商店的存档记录。
    /// </summary>
    /// <param name="data">要查询的完整存档。</param>
    /// <param name="catalogId">商店目录 ID。</param>
    /// <returns>找到时返回商店存档，找不到时返回 null。</returns>
    public static GameSaveShopData FindShop(GameSaveData data, string catalogId)
    {
        if (data == null || data.shops == null || string.IsNullOrWhiteSpace(catalogId))
            return null;

        for (int i = 0; i < data.shops.Count; i++)
        {
            GameSaveShopData shop = data.shops[i];

            if (shop != null && shop.catalogId == catalogId)
                return shop;
        }

        return null;
    }

    /// <summary>
    /// 查找或创建指定商店的存档记录。
    /// </summary>
    /// <param name="data">要修改的完整存档。</param>
    /// <param name="catalogId">商店目录 ID。</param>
    /// <returns>对应的商店存档，参数无效时返回 null。</returns>
    public static GameSaveShopData GetOrCreateShop(GameSaveData data, string catalogId)
    {
        if (data == null || string.IsNullOrWhiteSpace(catalogId))
            return null;

        if (data.shops == null)
            data.shops = new List<GameSaveShopData>();

        GameSaveShopData existingShop = FindShop(data, catalogId);

        if (existingShop != null)
        {
            if (existingShop.productStocks == null)
                existingShop.productStocks = new List<GameSaveShopStockEntry>();

            return existingShop;
        }

        GameSaveShopData newShop = new GameSaveShopData
        {
            catalogId = catalogId,
            productStocks = new List<GameSaveShopStockEntry>()
        };

        data.shops.Add(newShop);
        return newShop;
    }

    /// <summary>
    /// 确保静态服务已经读取过一次本地数据。
    /// </summary>
    private static void EnsureLoaded()
    {
        if (hasLoaded)
            return;

        hasLoaded = true;
        HasLoadedExistingSave = false;

        if (TryRead(SavePath, out GameSaveData formalData, out bool formalDataMigrated))
        {
            cachedData = formalData;
            HasLoadedExistingSave = true;

            if (formalDataMigrated)
                TrySave(cachedData);

            return;
        }

        if (TryRead(BackupPath, out GameSaveData backupData, out bool backupDataMigrated))
        {
            cachedData = backupData;
            HasLoadedExistingSave = true;
            Debug.LogWarning("正式游戏存档不可用，已从备份存档恢复。");

            if (backupDataMigrated)
                TrySave(cachedData);

            return;
        }

        cachedData = CreateNewData();
    }

    /// <summary>
    /// 从指定路径读取并校验一个存档文件。
    /// </summary>
    /// <param name="path">存档文件路径。</param>
    /// <param name="data">读取到的存档数据。</param>
    /// <returns>读取并校验成功返回 true。</returns>
    private static bool TryRead(string path, out GameSaveData data, out bool migrated)
    {
        data = null;
        migrated = false;

        if (!File.Exists(path))
            return false;

        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            GameSaveData parsedData = JsonUtility.FromJson<GameSaveData>(json);

            if (!IsValid(parsedData))
            {
                Debug.LogWarning($"游戏存档格式无效：{path}");
                return false;
            }

            migrated = parsedData.version < CurrentVersion;
            Migrate(parsedData);
            Normalize(parsedData);
            data = parsedData;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"读取游戏存档失败：{path}\n{exception.Message}");
            return false;
        }
    }

    /// <summary>
    /// 判断反序列化后的存档是否满足基本结构要求。
    /// </summary>
    /// <param name="data">待检查的存档。</param>
    /// <returns>结构有效返回 true。</returns>
    private static bool IsValid(GameSaveData data)
    {
        return data != null &&
               data.version > 0 &&
               data.version <= CurrentVersion;
    }

    /// <summary>
    /// 清理重复、负数、空 ID 和超出上限的存档记录。
    /// </summary>
    /// <param name="data">要规范化的存档。</param>
    private static void Normalize(GameSaveData data)
    {
        if (data == null)
            return;

        data.version = CurrentVersion;
        NormalizePlayerProfile(data);
        NormalizePlayerStats(data);
        NormalizeInventory(data);
        NormalizeShops(data);
    }

    /// <summary>
    /// 把旧版本存档补充为当前版本需要的字段。
    /// </summary>
    /// <param name="data">需要迁移的存档。</param>
    private static void Migrate(GameSaveData data)
    {
        if (data == null)
            return;

        if (data.version < 2)
            data.playerStats = new PlayerStatsSaveData();

        if (string.IsNullOrWhiteSpace(data.playerName))
            data.playerName = "玩家";
    }

    /// <summary>
    /// 规范化玩家档案字段。
    /// </summary>
    /// <param name="data">需要规范化的存档。</param>
    private static void NormalizePlayerProfile(GameSaveData data)
    {
        if (string.IsNullOrWhiteSpace(data.playerName))
            data.playerName = "玩家";

        data.playerName = data.playerName.Trim();
    }

    /// <summary>
    /// 规范化生命、魔法和体力，确保当前值不会超出最大值。
    /// </summary>
    /// <param name="data">需要规范化的存档。</param>
    private static void NormalizePlayerStats(GameSaveData data)
    {
        if (data.playerStats == null)
            data.playerStats = new PlayerStatsSaveData();

        data.playerStats.maxHealth = Math.Max(10, Math.Min(999, data.playerStats.maxHealth));
        data.playerStats.maxMana = Math.Max(10, Math.Min(999, data.playerStats.maxMana));
        data.playerStats.maxStamina = Math.Max(10, Math.Min(999, data.playerStats.maxStamina));
        data.playerStats.currentHealth = Mathf.Clamp(data.playerStats.currentHealth, 0f, data.playerStats.maxHealth);
        data.playerStats.currentMana = Mathf.Clamp(data.playerStats.currentMana, 0f, data.playerStats.maxMana);
        data.playerStats.currentStamina = Mathf.Clamp(data.playerStats.currentStamina, 0f, data.playerStats.maxStamina);
    }

    /// <summary>
    /// 规范化背包存档，并合并相同奖励的重复堆。
    /// </summary>
    /// <param name="data">要规范化的完整存档。</param>
    private static void NormalizeInventory(GameSaveData data)
    {
        Dictionary<string, long> amounts = new Dictionary<string, long>(StringComparer.Ordinal);

        if (data.inventory != null)
        {
            for (int i = 0; i < data.inventory.Count; i++)
            {
                GameSaveInventoryEntry entry = data.inventory[i];

                if (entry == null || string.IsNullOrWhiteSpace(entry.rewardId) || entry.amount <= 0)
                    continue;

                long safeAmount = ClampQuantity(entry.amount);

                if (amounts.TryGetValue(entry.rewardId, out long currentAmount))
                    amounts[entry.rewardId] = SaturatingAdd(currentAmount, safeAmount);
                else
                    amounts.Add(entry.rewardId, safeAmount);
            }
        }

        List<string> rewardIds = new List<string>(amounts.Keys);
        rewardIds.Sort(StringComparer.Ordinal);
        data.inventory = new List<GameSaveInventoryEntry>(rewardIds.Count);

        for (int i = 0; i < rewardIds.Count; i++)
        {
            string rewardId = rewardIds[i];
            data.inventory.Add(new GameSaveInventoryEntry
            {
                rewardId = rewardId,
                amount = amounts[rewardId]
            });
        }
    }

    /// <summary>
    /// 规范化商店存档，并防止重复库存记录造成库存凭空增加。
    /// </summary>
    /// <param name="data">要规范化的完整存档。</param>
    private static void NormalizeShops(GameSaveData data)
    {
        Dictionary<string, GameSaveShopData> uniqueShops =
            new Dictionary<string, GameSaveShopData>(StringComparer.Ordinal);

        if (data.shops != null)
        {
            for (int i = 0; i < data.shops.Count; i++)
            {
                GameSaveShopData shop = data.shops[i];

                if (shop == null || string.IsNullOrWhiteSpace(shop.catalogId))
                    continue;

                if (!uniqueShops.ContainsKey(shop.catalogId))
                {
                    uniqueShops.Add(shop.catalogId, shop);
                    NormalizeShopStocks(shop);
                }
            }
        }

        List<string> catalogIds = new List<string>(uniqueShops.Keys);
        catalogIds.Sort(StringComparer.Ordinal);
        data.shops = new List<GameSaveShopData>(catalogIds.Count);

        for (int i = 0; i < catalogIds.Count; i++)
            data.shops.Add(uniqueShops[catalogIds[i]]);
    }

    /// <summary>
    /// 规范化一个商店的商品库存记录。
    /// </summary>
    /// <param name="shop">要规范化的商店存档。</param>
    private static void NormalizeShopStocks(GameSaveShopData shop)
    {
        Dictionary<string, long> quantities = new Dictionary<string, long>(StringComparer.Ordinal);

        if (shop.productStocks != null)
        {
            for (int i = 0; i < shop.productStocks.Count; i++)
            {
                GameSaveShopStockEntry entry = shop.productStocks[i];

                if (entry == null || string.IsNullOrWhiteSpace(entry.productId) || entry.quantity < 0)
                    continue;

                long safeQuantity = ClampQuantity(entry.quantity);

                if (quantities.TryGetValue(entry.productId, out long currentQuantity))
                    quantities[entry.productId] = Math.Min(currentQuantity, safeQuantity);
                else
                    quantities.Add(entry.productId, safeQuantity);
            }
        }

        List<string> productIds = new List<string>(quantities.Keys);
        productIds.Sort(StringComparer.Ordinal);
        shop.productStocks = new List<GameSaveShopStockEntry>(productIds.Count);

        for (int i = 0; i < productIds.Count; i++)
        {
            string productId = productIds[i];
            shop.productStocks.Add(new GameSaveShopStockEntry
            {
                productId = productId,
                quantity = quantities[productId]
            });
        }
    }

    /// <summary>
    /// 将数量限制在 0 到 int.MaxValue 范围内，避免溢出和负数。
    /// </summary>
    /// <param name="value">待限制的数量。</param>
    /// <returns>安全数量。</returns>
    private static long ClampQuantity(long value)
    {
        return Math.Max(0L, Math.Min((long)MaxQuantity, value));
    }

    /// <summary>
    /// 执行不会溢出的长整数加法。
    /// </summary>
    /// <param name="left">左侧数量。</param>
    /// <param name="right">右侧数量。</param>
    /// <returns>限制在最大数量以内的结果。</returns>
    private static long SaturatingAdd(long left, long right)
    {
        if (left >= MaxQuantity || right >= MaxQuantity - left)
            return MaxQuantity;

        return left + right;
    }

    /// <summary>
    /// 将 JSON 文本先写入临时文件，再替换正式文件，尽量避免中途断电产生半份存档。
    /// </summary>
    /// <param name="json">要写入的 JSON 文本。</param>
    /// <returns>写入成功返回 true。</returns>
    private static bool TryWriteAtomically(string json)
    {
        string savePath = SavePath;
        string backupPath = BackupPath;
        string temporaryPath = TemporaryPath;

        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);

            byte[] bytes = new UTF8Encoding(false).GetBytes(json);

            using (FileStream stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            if (File.Exists(savePath))
            {
                try
                {
                    File.Replace(temporaryPath, savePath, backupPath, true);
                }
                catch (PlatformNotSupportedException)
                {
                    ReplaceWithoutFileReplace(temporaryPath, savePath, backupPath);
                }
                catch (IOException)
                {
                    ReplaceWithoutFileReplace(temporaryPath, savePath, backupPath);
                }
            }
            else
            {
                File.Move(temporaryPath, savePath);
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"保存游戏存档失败：{savePath}\n{exception.Message}");
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (Exception cleanupException)
            {
                Debug.LogWarning($"清理临时存档失败：{temporaryPath}\n{cleanupException.Message}");
            }
        }
    }

    /// <summary>
    /// 在平台不支持 File.Replace（文件替换）时执行带备份的兼容替换。
    /// </summary>
    /// <param name="temporaryPath">临时文件路径。</param>
    /// <param name="savePath">正式文件路径。</param>
    /// <param name="backupPath">备份文件路径。</param>
    private static void ReplaceWithoutFileReplace(string temporaryPath, string savePath, string backupPath)
    {
        if (File.Exists(backupPath))
            File.Delete(backupPath);

        File.Copy(savePath, backupPath, true);
        File.Delete(savePath);
        File.Move(temporaryPath, savePath);
    }
}
