using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class MapVNextI1IntegrationTests
{
    [UnityTest] public IEnumerator CheckpointFoodStockVersionsPreserveLegacyAndRejectCurrentGaps()
    {
        yield return LoadFormalScene("FoodStockCompatibility");
        Call("BeginNewRunWithSeed", 21193); Ready(true);
        var trade = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true).Single(c => c.GetType().Name == "ShopTradeController");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var captureMethod = trade.GetType().GetMethod("TryCaptureCheckpointShop", flags);
        var applyMethod = trade.GetType().GetMethod("TryApplyCheckpointShop", flags);
        Func<TowerRunShopRecord> capture = () => {
            var record = new TowerRunShopRecord(); object[] args = {record, null};
            Assert.That((bool)captureMethod.Invoke(trade, args), Is.True, args[1] as string); return record;
        };
        Func<TowerRunShopRecord, bool> apply = record => (bool)applyMethod.Invoke(trade, new object[] {record, false, null});
        const string meat = "762f41302f8b484f87042277651b8692";
        var initial = capture(); Assert.That(initial.stockVersion, Is.EqualTo(1)); Assert.That(initial.productStocks.Count, Is.EqualTo(3));
        Assert.That(initial.DeepClone().stockVersion, Is.EqualTo(1));
        var legacy = initial.DeepClone(); legacy.stockVersion = 0;
        legacy.productStocks = legacy.productStocks.Where(s => s.productId == meat).ToList();
        legacy.productStocks.Single().quantity = 1;
        string legacyBefore = JsonUtility.ToJson(legacy);
        Assert.That(apply(legacy), Is.True, "Old meat-only stock must remain compatible with additive Food products");
        Assert.That(JsonUtility.ToJson(legacy), Is.EqualTo(legacyBefore), "Reading an old record must not mutate it");
        var current = capture();
        Assert.That(current.stockVersion, Is.EqualTo(1));
        Assert.That(current.productStocks.Single(s => s.productId == meat).quantity, Is.EqualTo(1));
        Assert.That(current.productStocks.Where(s => s.productId != meat).All(s => s.quantity == 3), Is.True);
        var bad = new List<TowerRunShopRecord>();
        var missingCurrent = current.DeepClone(); missingCurrent.productStocks.RemoveAt(0); bad.Add(missingCurrent);
        var missingOld = legacy.DeepClone(); missingOld.productStocks.Clear(); bad.Add(missingOld);
        var duplicate = current.DeepClone(); duplicate.productStocks.Add(duplicate.productStocks[0].DeepClone()); bad.Add(duplicate);
        var unknown = current.DeepClone(); unknown.productStocks[0].productId = "unknown-food-sku"; bad.Add(unknown);
        var nullLegacyId = legacy.DeepClone(); nullLegacyId.productStocks[0].productId = null; bad.Add(nullLegacyId);
        var nullCurrentId = current.DeepClone(); nullCurrentId.productStocks[0].productId = null; bad.Add(nullCurrentId);
        var blankLegacyId = legacy.DeepClone(); blankLegacyId.productStocks[0].productId = " "; bad.Add(blankLegacyId);
        var negative = current.DeepClone(); negative.productStocks[0].quantity = -1; bad.Add(negative);
        var overStock = current.DeepClone(); overStock.productStocks[0].quantity = 4; bad.Add(overStock);
        var future = current.DeepClone(); future.stockVersion = 2; bad.Add(future);
        var invalidVersion = current.DeepClone(); invalidVersion.stockVersion = -1; bad.Add(invalidVersion);
        foreach (var record in bad)
        {
            string before = JsonUtility.ToJson(capture()); string sourceBefore = JsonUtility.ToJson(record);
            var sourceIdsBefore = record.productStocks.Select(s => s.productId).ToArray();
            Assert.That(apply(record), Is.False, "Invalid current stock must not use the old-version fallback");
            Assert.That(JsonUtility.ToJson(capture()), Is.EqualTo(before), "Rejected stock must be atomic");
            Assert.That(JsonUtility.ToJson(record), Is.EqualTo(sourceBefore));
            CollectionAssert.AreEqual(sourceIdsBefore, record.productStocks.Select(s => s.productId).ToArray(),
                "Rejected records must preserve null and blank IDs exactly");
        }
        var service = trade.GetType().Assembly.GetType("GameSaveService", true);
        object data = service.GetMethod("CreateNewData").Invoke(null, null);
        object shop = Activator.CreateInstance(trade.GetType().Assembly.GetType("GameSaveShopData", true));
        shop.GetType().GetField("catalogId").SetValue(shop, current.catalogId);
        shop.GetType().GetField("stockVersion").SetValue(shop, 1);
        ((IList)data.GetType().GetField("shops").GetValue(data)).Add(shop);
        object cloned = service.GetMethod("Clone").Invoke(null, new[] {data});
        object clonedShop = ((IList)cloned.GetType().GetField("shops").GetValue(cloned))[0];
        Assert.That(clonedShop.GetType().GetField("stockVersion").GetValue(clonedShop), Is.EqualTo(1));
        object originalMemory = service.GetMethod("Clone").Invoke(null, new[] {service.GetMethod("GetOrCreateData").Invoke(null, null)});
        var entrance = Entrance();
        var catalog = trade.GetType().GetField("shopCatalog", flags).GetValue(trade);
        object foodCategory = Enum.Parse(trade.GetType().Assembly.GetType("ShopProductCategoryType", true), "Food");
        object product = ((IEnumerable)catalog.GetType().GetMethod("GetProducts").Invoke(catalog, new[] {foodCategory})).Cast<object>().First();
        Assert.That(trade.GetType().GetMethod("CanPurchase").Invoke(trade, new object[] {product, null}), Is.EqualTo(true));
        try
        {
            object stockEntry = Activator.CreateInstance(trade.GetType().Assembly.GetType("GameSaveShopStockEntry", true));
            stockEntry.GetType().GetField("productId").SetValue(stockEntry, meat);
            stockEntry.GetType().GetField("quantity").SetValue(stockEntry, 1L);
            ((IList)shop.GetType().GetField("productStocks").GetValue(shop)).Add(stockEntry);
            Assert.That(service.GetMethod("TryReplaceRuntimeData").Invoke(null, new[] {data}), Is.EqualTo(true));
            string cachedBefore = JsonUtility.ToJson(service.GetMethod("GetOrCreateData").Invoke(null, null));
            var bridge = trade.GetType().Assembly.GetType("TowerRunRuntimeStateBridge", true);
            object[] bridgeArgs = {entrance.runId, entrance.playerName, entrance.floor, entrance.generation,
                coordinator.GetType().GetField("stats", flags).GetValue(coordinator),
                coordinator.GetType().GetField("inventory", flags).GetValue(coordinator),
                coordinator.GetType().GetField("shops", flags).GetValue(coordinator), null, null};
            Assert.That((bool)bridge.GetMethod("TryCapture").Invoke(null, bridgeArgs), Is.False,
                "An already initialized shop must not hide a current-version cached gap during capture");
            string liveBefore = JsonUtility.ToJson(capture());
            object inventory = coordinator.GetType().GetField("inventory", flags).GetValue(coordinator);
            object gold = trade.GetType().GetField("goldReward", flags).GetValue(trade);
            object goldBefore = inventory.GetType().GetMethod("GetItemAmountLong").Invoke(inventory, new[] {gold});
            Assert.That(trade.GetType().GetMethod("CanPurchase").Invoke(trade, new object[] {product, null}), Is.EqualTo(false));
            object purchase = trade.GetType().GetMethod("TryPurchase").Invoke(trade, new[] {product});
            Assert.That(purchase.GetType().GetField("succeeded").GetValue(purchase), Is.EqualTo(false));
            Assert.That(trade.GetType().GetMethod("SaveRuntimeStocks").Invoke(trade, null), Is.EqualTo(false));
            Assert.That(inventory.GetType().GetMethod("GetItemAmountLong").Invoke(inventory, new[] {gold}), Is.EqualTo(goldBefore));
            Assert.That(JsonUtility.ToJson(capture()), Is.EqualTo(liveBefore));
            Assert.That(JsonUtility.ToJson(service.GetMethod("GetOrCreateData").Invoke(null, null)), Is.EqualTo(cachedBefore));
            trade.GetType().GetField("hasInitializedRuntimeStocks", flags).SetValue(trade, false);
            var stocks = (IDictionary)trade.GetType().GetField("runtimeStocks", flags).GetValue(trade); stocks.Clear();
            object[] failedCapture = {new TowerRunShopRecord(), null};
            Assert.That((bool)captureMethod.Invoke(trade, failedCapture), Is.False,
                "Current-version cached gaps must fail initialization and capture");
            Assert.That(trade.GetType().GetMethod("GetCurrentStock").Invoke(trade, new[] {product}), Is.EqualTo(0));
            Assert.That(trade.GetType().GetMethod("CanPurchase").Invoke(trade, new object[] {product, null}), Is.EqualTo(false));
            Assert.That(trade.GetType().GetMethod("SaveRuntimeStocks").Invoke(trade, null), Is.EqualTo(false));
            Assert.That(stocks.Count, Is.Zero, "Invalid initialization cannot be patched by EnsureProductStock");
            Assert.That(JsonUtility.ToJson(service.GetMethod("GetOrCreateData").Invoke(null, null)), Is.EqualTo(cachedBefore));
            shop.GetType().GetField("stockVersion").SetValue(shop, 0);
            Assert.That(service.GetMethod("TryReplaceRuntimeData").Invoke(null, new[] {data}), Is.EqualTo(true));
            trade.GetType().GetField("hasInitializedRuntimeStocks", flags).SetValue(trade, false); stocks.Clear();
            var fromOldCache = capture();
            Assert.That(fromOldCache.stockVersion, Is.EqualTo(1));
            Assert.That(fromOldCache.productStocks.Single(s => s.productId == meat).quantity, Is.EqualTo(1));
            Assert.That(fromOldCache.productStocks.Where(s => s.productId != meat).All(s => s.quantity == 3), Is.True);
        }
        finally
        {
            service.GetMethod("TryReplaceRuntimeData").Invoke(null, new[] {originalMemory});
            Assert.That(apply(current), Is.True);
        }
        File.WriteAllText(Path.Combine(Output, "FoodStockCompatibility.txt"),
            "Legacy stockVersion0 meat1 preserved; new food3+3, direct and cached initialization. Current stockVersion1 captured/cloned. Eleven invalid records, including legacy/current null and blank IDs, rejected atomically; current cached gaps fail capture, init, purchase and save; sources unchanged.");
    }
}
