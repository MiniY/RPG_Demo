using System.Collections.Generic;
using NUnit.Framework;

public class TowerRunCheckpointRecordTests
{
    [Test]
    public void DeepClone_SeparatesEveryMutablePlayerCollection()
    {
        TowerRunCheckpointRecord source = new TowerRunCheckpointRecord
        {
            runId = "run-a",
            playerName = "玩家",
            floor = new TowerRunFloorRecord
            {
                floorNumber = 1,
                displayName = "第 1 层"
            },
            player = new TowerRunPlayerRecord
            {
                inventory = new List<TowerRunInventoryEntry>
                {
                    new TowerRunInventoryEntry { rewardId = "gold", amount = 12 },
                    new TowerRunInventoryEntry { rewardId = "missing-item", amount = 2 }
                },
                shops = new List<TowerRunShopRecord>
                {
                    new TowerRunShopRecord
                    {
                        catalogId = "merchant-a",
                        productStocks = new List<TowerRunShopStockRecord>
                        {
                            new TowerRunShopStockRecord { productId = "potion", quantity = 3 }
                        }
                    }
                }
            }
        };

        TowerRunCheckpointRecord clone = source.DeepClone();

        clone.floor.displayName = "changed";
        clone.player.inventory[0].amount = 99;
        clone.player.inventory.Add(new TowerRunInventoryEntry { rewardId = "new", amount = 1 });
        clone.player.shops[0].productStocks[0].quantity = 0;

        Assert.That(source.floor.displayName, Is.EqualTo("第 1 层"));
        Assert.That(source.player.inventory, Has.Count.EqualTo(2));
        Assert.That(source.player.inventory[0].amount, Is.EqualTo(12));
        Assert.That(source.player.shops[0].productStocks[0].quantity, Is.EqualTo(3));
    }
}
