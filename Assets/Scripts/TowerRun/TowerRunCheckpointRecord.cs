using System;
using System.Collections.Generic;

[Serializable]
public sealed class TowerRunInventoryEntry
{
    public string rewardId;
    public long amount;

    public TowerRunInventoryEntry DeepClone()
    {
        return new TowerRunInventoryEntry
        {
            rewardId = rewardId,
            amount = amount
        };
    }
}

[Serializable]
public sealed class TowerRunShopStockRecord
{
    public string productId;
    public long quantity;

    public TowerRunShopStockRecord DeepClone()
    {
        return new TowerRunShopStockRecord
        {
            productId = productId,
            quantity = quantity
        };
    }
}

[Serializable]
public sealed class TowerRunShopRecord
{
    public string catalogId;
    public List<TowerRunShopStockRecord> productStocks = new List<TowerRunShopStockRecord>();

    public TowerRunShopRecord DeepClone()
    {
        TowerRunShopRecord clone = new TowerRunShopRecord
        {
            catalogId = catalogId
        };

        if (productStocks == null)
            return clone;

        for (int i = 0; i < productStocks.Count; i++)
        {
            TowerRunShopStockRecord stock = productStocks[i];

            if (stock != null)
                clone.productStocks.Add(stock.DeepClone());
        }

        return clone;
    }
}

[Serializable]
public sealed class TowerRunPlayerRecord
{
    public TowerRunPlayerStatsRecord stats = new TowerRunPlayerStatsRecord();
    public List<TowerRunInventoryEntry> inventory = new List<TowerRunInventoryEntry>();
    public List<TowerRunShopRecord> shops = new List<TowerRunShopRecord>();

    public TowerRunPlayerRecord DeepClone()
    {
        TowerRunPlayerRecord clone = new TowerRunPlayerRecord
        {
            stats = stats?.DeepClone() ?? new TowerRunPlayerStatsRecord()
        };

        if (inventory != null)
        {
            for (int i = 0; i < inventory.Count; i++)
            {
                TowerRunInventoryEntry entry = inventory[i];

                if (entry != null)
                    clone.inventory.Add(entry.DeepClone());
            }
        }

        if (shops != null)
        {
            for (int i = 0; i < shops.Count; i++)
            {
                TowerRunShopRecord shop = shops[i];

                if (shop != null)
                    clone.shops.Add(shop.DeepClone());
            }
        }

        return clone;
    }
}

[Serializable]
public sealed class TowerRunPlayerStatsRecord
{
    public int maxHealth = 100;
    public float currentHealth = 100f;
    public int maxMana = 100;
    public float currentMana = 100f;
    public int maxStamina = 100;
    public float currentStamina = 100f;

    public TowerRunPlayerStatsRecord DeepClone()
    {
        return new TowerRunPlayerStatsRecord
        {
            maxHealth = maxHealth,
            currentHealth = currentHealth,
            maxMana = maxMana,
            currentMana = currentMana,
            maxStamina = maxStamina,
            currentStamina = currentStamina
        };
    }
}

[Serializable]
public sealed class TowerRunGenerationRecord
{
    public int generatorVersion;
    public string configurationIdentity;
    public string profileId;
    public int requestedSeed;
    public int attemptIndex;
    public string staticBaselineSignature;

    public TowerRunGenerationRecord DeepClone()
    {
        return new TowerRunGenerationRecord
        {
            generatorVersion = generatorVersion,
            configurationIdentity = configurationIdentity,
            profileId = profileId,
            requestedSeed = requestedSeed,
            attemptIndex = attemptIndex,
            staticBaselineSignature = staticBaselineSignature
        };
    }
}

[Serializable]
public sealed class TowerRunFloorRecord
{
    public int floorNumber;
    public string displayName;

    public TowerRunFloorRecord DeepClone()
    {
        return new TowerRunFloorRecord
        {
            floorNumber = floorNumber,
            displayName = displayName
        };
    }
}

[Serializable]
public sealed class TowerRunCheckpointRecord
{
    public const int CurrentSchemaVersion = 1;

    public int schemaVersion = CurrentSchemaVersion;
    public string runId;
    public string playerName = "玩家";
    public string savedAtUtc;
    public TowerRunFloorRecord floor = new TowerRunFloorRecord();
    public TowerRunGenerationRecord generation = new TowerRunGenerationRecord();
    public TowerRunPlayerRecord player = new TowerRunPlayerRecord();

    public TowerRunCheckpointRecord DeepClone()
    {
        return new TowerRunCheckpointRecord
        {
            schemaVersion = schemaVersion,
            runId = runId,
            playerName = playerName,
            savedAtUtc = savedAtUtc,
            floor = floor?.DeepClone() ?? new TowerRunFloorRecord(),
            generation = generation?.DeepClone() ?? new TowerRunGenerationRecord(),
            player = player?.DeepClone() ?? new TowerRunPlayerRecord()
        };
    }
}
