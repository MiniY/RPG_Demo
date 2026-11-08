#if UNITY_EDITOR || RPG_SHOP_INTEGRATION_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public sealed partial class MapVNextI1Acceptance
{
    private readonly List<Mouse> shopPhysicalMice = new List<Mouse>();
    private Mouse shopPointer;
    private void BindShopPointer()
    {
        if (shopPointer != null) return;
        foreach (var device in InputSystem.devices.OfType<Mouse>().Where(m => m.enabled).ToArray())
        {
            shopPhysicalMice.Add(device);
            InputSystem.DisableDevice(device);
        }
        shopPointer = InputSystem.AddDevice<Mouse>("ShopAcceptancePointer");
        InputSystem.QueueStateEvent(shopPointer, new MouseState { position = new Vector2(-1000, -1000) });
    }
    private void CleanupShopPointer()
    {
        try { if (shopPointer != null && shopPointer.added) InputSystem.RemoveDevice(shopPointer); }
        finally
        {
            shopPointer = null;
            foreach (var device in shopPhysicalMice)
                if (device.added) InputSystem.EnableDevice(device);
            shopPhysicalMice.Clear();
        }
    }
    [Serializable] private sealed class FoodProductProof
    {
        public string rewardId, productId, name, sprite;
        public int price, initialStock, beforeStock, afterStock, beforeAmount, afterAmount;
    }
    [Serializable] private sealed class FoodShopProof
    {
        public string status = "RUNNING", buildId, sourceFingerprint, slotRoot;
        public int width, height, beforeGold, afterGold;
        public List<FoodProductProof> products = new List<FoodProductProof>();
        public List<string> checks = new List<string>();
    }
    private IEnumerator ClickShopProduct(ShopSlotUI slot, string capture = null)
    {
        Canvas.ForceUpdateCanvases();
        var scroll = slot.GetComponentInParent<ScrollRect>();
        if (scroll != null && scroll.content != null)
        {
            RectTransform viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            var contentCorners = new Vector3[4]; scroll.content.GetWorldCorners(contentCorners);
            var bounds = new Bounds(viewport.InverseTransformPoint(contentCorners[0]), Vector3.zero);
            for (int i = 1; i < contentCorners.Length; i++) bounds.Encapsulate(viewport.InverseTransformPoint(contentCorners[i]));
            Vector2 centre = viewport.InverseTransformPoint(slot.RectTransform.TransformPoint(slot.RectTransform.rect.center));
            Vector2 offset = viewport.rect.center - centre;
            scroll.StopMovement();
            if (scroll.vertical && bounds.size.y > viewport.rect.height)
                scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition - offset.y / (bounds.size.y - viewport.rect.height));
            if (scroll.horizontal && bounds.size.x > viewport.rect.width)
                scroll.horizontalNormalizedPosition = Mathf.Clamp01(scroll.horizontalNormalizedPosition - offset.x / (bounds.size.x - viewport.rect.width));
            yield return null; yield return null; Canvas.ForceUpdateCanvases();
            var cardCorners = new Vector3[4]; slot.RectTransform.GetWorldCorners(cardCorners);
            Require(cardCorners.All(c => {
                float y = viewport.InverseTransformPoint(c).y;
                return y >= viewport.rect.yMin - .01f && y <= viewport.rect.yMax + .01f;
            }), "FoodCaptureMustShowWholeCard:" + slot.CurrentProduct.Reward.RewardId);
        }
        if (capture != null) yield return ShopPicture(capture);
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, slot.RectTransform.TransformPoint(slot.RectTransform.rect.center)) };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0, "ShopNoHitAtCard");
        var clicked = ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
        Require(clicked != null && (clicked == slot.gameObject || clicked.transform.IsChildOf(slot.transform)),
            "ShopCardBlockedByOtherUI:reward=" + slot.CurrentProduct.Reward.RewardId + ":point=" + pointer.position +
            ":firstHit=" + hits[0].gameObject.name + ":clicked=" + (clicked == null ? "null" : clicked.name));
        yield return null;
    }
    private IEnumerator ProveFoodProducts()
    {
        var report = new FoodShopProof { buildId = MapHostBuildStamp.BuildId,
            sourceFingerprint = Environment.GetEnvironmentVariable("RPG_I1_SOURCE_FINGERPRINT"),
            slotRoot = TowerRunRuntimeEnvironment.SlotRoot, width = Screen.width, height = Screen.height };
        Action persist = () => File.WriteAllText(Path.Combine(output, "food-shop.json"), JsonUtility.ToJson(report, true));
        persist();
        var panel = FindObjectOfType<ShopPanelController>(true);
        var trade = FindObjectOfType<ShopTradeController>(true);
        var inventory = FindObjectOfType<PlayerInventory>();
        var merchant = FindObjectsOfType<MerchantInteractionController>().Single(m => m.BoundPlayer == body.transform);
        var products = merchant.ShopCatalog.GetProducts(ShopProductCategoryType.Food);
        Require(products.Count == 3 && products.Select(p => p.Reward.RewardId).OrderBy(id => id).SequenceEqual(
            new[] { "meat", "plant_cream_big", "plant_cream_small" }), "FoodCatalogMustContainExactlyThreeExistingRewards");
        Require(products.Select(p => p.ProductId).Distinct().Count() == 3, "FoodProductIdsMustBeUnique");
        yield return SetupFoot((Vector2)merchant.transform.position + Vector2.down * .85f, "FOOD_EXPLICIT_PROXIMITY_FIXTURE_NOT_WALK");
        yield return new WaitForFixedUpdate(); yield return ShopKey(Key.F);
        Require(panel.IsShopPanelVisible && panel.CurrentMerchant == merchant, "FoodOriginalMerchantFChainFailed");
        FindObjectsOfType<Toggle>().Single(t => t.name == "FoodTab").isOn = true;
        yield return null; yield return null; Canvas.ForceUpdateCanvases();
        foreach (var product in products)
        {
            var slot = FindObjectsOfType<ShopSlotUI>().Single(s => s.CurrentProduct == product);
            var image = PrivateField<Image>(slot, "itemIcon");
            var title = PrivateField<TMP_Text>(slot, "itemNameText"); title.ForceMeshUpdate();
            Require(PrivateField<TMP_Text>(slot, "priceText").text == ShopUiText.Price(product),
                "FoodCardPriceLabelMismatch:" + product.Reward.RewardId);
            var scroll = slot.GetComponentInParent<ScrollRect>();
            var corners = new Vector3[4]; slot.RectTransform.GetWorldCorners(corners);
            Require(scroll != null && corners.All(c => {
                float x = scroll.viewport.InverseTransformPoint(c).x;
                return x >= scroll.viewport.rect.xMin - .01f && x <= scroll.viewport.rect.xMax + .01f;
            }), "FoodCardOutsideListWidth:" + product.Reward.RewardId);
            var detailsRect = (RectTransform)FindObjectOfType<ShopDetailsUI>(true).transform;
            var detailsCorners = new Vector3[4]; detailsRect.GetWorldCorners(detailsCorners);
            float detailsLeft = detailsCorners.Min(c => scroll.viewport.InverseTransformPoint(c).x);
            Require(corners.All(c => scroll.viewport.InverseTransformPoint(c).x <= detailsLeft + .01f),
                "FoodCardOverlapsDetailDock:" + product.Reward.RewardId);
            Require(image != null && image.enabled && image.sprite != null && image.sprite == product.Icon,
                "FoodIconMustUseItsCatalogPresentation:" + product.Reward.RewardId);
            Require(title.text == ShopUiText.Name(product) && !title.isTextOverflowing &&
                title.textBounds.size.x <= title.rectTransform.rect.width + 2f, "FoodTitleDoesNotFitAuthoredRibbon:" + product.Reward.RewardId);
            if (product.Reward.RewardId != "meat")
            {
                Require(product.CardIconSize == 220 && image.sprite != product.Reward.itemIcon &&
                    image.sprite.texture.filterMode == FilterMode.Point, "FoodNeedsItsBoundedPixelIcon:" + product.Reward.RewardId);
                var iconParent = (RectTransform)image.transform.parent;
                var iconCorners = new Vector3[4]; image.rectTransform.GetWorldCorners(iconCorners);
                Require(iconCorners.All(c => iconParent.rect.Contains(iconParent.InverseTransformPoint(c))),
                    "FoodIconOutsideAuthoredProductView:" + product.Reward.RewardId);
            }
            int price = product.Reward.RewardId == "plant_cream_small" ? 3 : product.Reward.RewardId == "plant_cream_big" ? 8 : 5;
            Require(product.UnitPrice == price && product.InitialStock == 3 && product.PurchaseAmount == 1 && !product.InfiniteStock,
                "FoodProductConfigurationMismatch:" + product.Reward.RewardId);
            report.products.Add(new FoodProductProof { rewardId = product.Reward.RewardId, productId = product.ProductId,
                name = title.text, sprite = image.sprite.name, price = price, initialStock = 3,
                beforeStock = trade.GetCurrentStock(product), beforeAmount = inventory.GetItemAmount(product.Reward) });
        }
        report.beforeGold = inventory.GetItemAmount(inventory.GoldReward); persist();
        CheckShopFonts(); yield return ShopPicture("04-Food-ThreeProducts");
        foreach (string id in new[] { "plant_cream_small", "plant_cream_big" })
        {
            var product = products.Single(p => p.Reward.RewardId == id);
            var beforeStocks = products.ToDictionary(p => p.ProductId, p => trade.GetCurrentStock(p));
            int gold = inventory.GetItemAmount(inventory.GoldReward), amount = inventory.GetItemAmount(product.Reward);
            Require(beforeStocks[product.ProductId] == 3, "NewFoodStockWasResetOrShared:" + id);
            yield return ClickShopProduct(FindObjectsOfType<ShopSlotUI>().Single(s => s.CurrentProduct == product), "Food-" + id + "-BeforePurchase");
            var detailTitle = PrivateField<TMP_Text>(FindObjectOfType<ShopDetailsUI>(true), "titleText");
            detailTitle.ForceMeshUpdate();
            Require(detailTitle.text == ShopUiText.Name(product) && detailTitle.textBounds.size.x <= detailTitle.rectTransform.rect.width + 2f,
                "FoodDetailTitleDoesNotFit:" + id);
            Require(inventory.GetItemAmount(inventory.GoldReward) == gold - product.UnitPrice &&
                inventory.GetItemAmount(product.Reward) == amount + 1 && trade.GetCurrentStock(product) == 2,
                "FoodPurchaseMustDebitExactPriceAndAddOne:" + id);
            foreach (var other in products.Where(p => p != product))
                Require(trade.GetCurrentStock(other) == beforeStocks[other.ProductId], "FoodStocksMustBeIndependent:" + id);
        }
        CheckShopFonts(); yield return ShopPicture("05-Food-AfterEssencePurchase");
        yield return ShopKey(Key.Escape); Require(!panel.IsShopPanelVisible, "FoodShopDidNotClose");
        yield return ShopKey(Key.F); Require(panel.IsShopPanelVisible, "FoodShopDidNotReopen");
        foreach (var product in products)
        {
            var row = report.products.Single(r => r.productId == product.ProductId);
            row.afterStock = trade.GetCurrentStock(product); row.afterAmount = inventory.GetItemAmount(product.Reward);
            Require(row.afterStock == (row.rewardId == "meat" ? row.beforeStock : 2), "FoodStockNotRetainedAfterReopen");
        }
        report.afterGold = inventory.GetItemAmount(inventory.GoldReward);
        Require(report.afterGold == report.beforeGold - 11, "FoodCombinedPriceMismatch");
        yield return ShopKey(Key.Escape); yield return ShopKey(Key.Tab);
        Require(FindObjectOfType<PackagePanelController>(true).IsPackagePanelVisible, "FoodBackpackDidNotOpen");
        foreach (var product in products.Where(p => p.Reward.RewardId != "meat"))
            Require(FindObjectsOfType<PackageSlotUI>().Any(s => s.CurrentReward == product.Reward), "PurchasedFoodMissingFromBackpack");
        CheckShopFonts(); yield return ShopPicture("06-Package-NewFood");
        yield return ShopKey(Key.Tab);
        Require(!TowerRunGateHub.IsGameplayBlocked, "FoodFlowLeftGameplayBlocked");
        report.checks.Add("Three Food entries retain original rewards; essence uses byte-identical local UI pixel copies with Point import and bounded icon layout. Card/detail titles fit; 5/3/8 prices, stock3, purchase1.");
        report.checks.Add("Original F, actual EventSystem first-hit card click, exact two prices/quantities, independent stocks, close/reopen retention and backpack presentation verified.");
        report.status = "AUTOMATED_FOOD_PRODUCTS_VERIFIED_HUMAN_PENDING"; persist();
    }
}
#endif
