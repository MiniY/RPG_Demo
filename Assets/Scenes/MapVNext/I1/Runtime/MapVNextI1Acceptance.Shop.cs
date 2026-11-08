#if UNITY_EDITOR || RPG_SHOP_INTEGRATION_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed partial class MapVNextI1Acceptance
{
    [Serializable] private sealed class ShopRow
    {
        public string status="RUNNING",buildId,scene,slotRoot,content,runId,worldSha,slotSha,inputScope;
        public List<string> checks=new List<string>();
        public List<ShopAmounts> purchases=new List<ShopAmounts>();
        public List<ShopMerchantRow> merchants=new List<ShopMerchantRow>();
    }
    [Serializable] private sealed class ShopAmounts {public int gold,meat,stock;}
    [Serializable] private sealed class ShopMerchantRow {public string name,controller,banner;public int spriteChanges;public bool actualMerchantReference;}
    private ShopRow shopEvidence;
    private bool shopFrames;
    private void ShopPersist() => File.WriteAllText(Path.Combine(output,"shop-integration.json"),JsonUtility.ToJson(shopEvidence,true));
    private static T PrivateField<T>(object owner,string name) => (T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
    private static void InvokePrivate(object owner,string name) => owner.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(owner,null);
    private IEnumerator ShopKey(Key key)
    {
        Feed(key);yield return new WaitForSecondsRealtime(.08f);Feed();yield return null;yield return null;
    }
    public IEnumerator RunShopEditorTest(string directory)
        => RunShopEditorTestCore(directory, false);
    public void CleanupShopEditorTest()
    {
        try { session?.ReturnToStartupChoice(); }
        finally { if (keyboard != null) CleanupInput(); }
    }
    private IEnumerator RunShopEditorTestCore(string directory, bool leavePlaying)
    {
        output=directory;Directory.CreateDirectory(output);session=GameSession.Instance.TowerRun;map=FindObjectOfType<MapGenerationController>(true);shopFrames=false;
        bool completed=false;
        try {yield return ProveShopIntegration();completed=true;}
        finally
        {
            try {if(!completed||!leavePlaying)session?.ReturnToStartupChoice();}
            finally {if(keyboard!=null)CleanupInput();}
        }
    }
    private IEnumerator ShopPicture(string label)
    {
        if(shopFrames)yield return CaptureFrame("Screenshots/"+label+".png","ShopIntegration");
    }
    private void CheckShopFonts()
    {
        int count=0;
        foreach(var text in FindObjectsOfType<TMP_Text>())
        {
            if(text.font==null||text.font.name!="MyTMP")continue;
            count++;text.ForceMeshUpdate();Require(text.fontSharedMaterial!=null&&text.fontSharedMaterial.mainTexture!=null,"ShopFontMaterialMissing:"+text.name);
            foreach(char c in Regex.Replace(text.text??string.Empty,"<[^>]*>",string.Empty))
                Require(char.IsWhiteSpace(c)||text.font.characterLookupTable.ContainsKey(c),"ShopMissingGlyph:"+text.name+":"+(int)c);
        }
        Require(count>0,"ShopNoUnifiedFontVisible");
    }
    private static ShopSlotUI MeatSlot() => FindObjectsOfType<ShopSlotUI>().Single(s=>s.CurrentProduct!=null && s.CurrentProduct.Reward.RewardId=="meat");
    private ShopAmounts ShopCount(ShopSlotUI slot,ShopTradeController trade,PlayerInventory inventory)
        => new ShopAmounts{gold=inventory.GetItemAmount(inventory.GoldReward),meat=inventory.GetItemAmount(slot.CurrentProduct.Reward),stock=trade.GetCurrentStock(slot.CurrentProduct)};
    private void RequireAmounts(ShopAmounts row,int gold,int meat,int stock)
    {Require(row.gold==gold&&row.meat==meat&&row.stock==stock,"ShopAmountsMismatch:"+JsonUtility.ToJson(row));shopEvidence.purchases.Add(row);ShopPersist();}
    private IEnumerator ProveShopIntegration()
    {
        Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path=="Assets/Scenes/SampleScene.unity","ShopMustUseFormalSampleScene");
        string protectedRoot=Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AppData/LocalLow/DefaultCompany/RPG_Demo"));
        Require(!Path.GetFullPath(TowerRunRuntimeEnvironment.SlotRoot).StartsWith(protectedRoot,StringComparison.OrdinalIgnoreCase),"ShopTestCannotUseProductionSave");
        shopFrames=!Application.isBatchMode;shopEvidence=new ShopRow{buildId=MapHostBuildStamp.BuildId,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
            slotRoot=TowerRunRuntimeEnvironment.SlotRoot,inputScope="Explicit diagnostic pointer isolation: enabled physical Mouse devices temporarily disabled inside this process, owned Mouse stays outside canvas, cleanup restores devices. Automated InputSystem keyboard -> existing GameInput -> real merchant range/event; EventSystem raycast -> existing slot click/trade. Player proximity is explicit fixture placement, not a human WASD claim."};ShopPersist();
        Require(session.State==TowerRunFlowState.StartupChoice,"ShopStartupChoiceMissing");
        var panel=FindObjectOfType<ShopPanelController>(true);var grid=FindObjectOfType<ShopGridUI>(true);
        Require(panel!=null&&grid!=null&&!panel.IsShopPanelVisible,"ShopInitiallyClosed");
        var startup=FindObjectOfType<TowerRunUiController>(true);
        PrivateField<UnityEngine.UI.Button>(startup,"primaryButton").onClick.Invoke();
        Require(session.State==TowerRunFlowState.Playing&&Context.IsReady,"ShopOrdinaryNewGameFailed:"+session.FailureMessage);
        Ready();BindPlayer();autoDefense=false;BindInput();BindShopPointer();
        var entry=Entrance();shopEvidence.content=entry.generation.ecology.contentVersion;shopEvidence.runId=entry.runId;
        string world=MapHostStateSnapshot.Capture(map);File.WriteAllText(Path.Combine(output,"Shop-entrance-world.txt"),world);shopEvidence.worldSha=MapHostCompatibility.Sha(System.Text.Encoding.UTF8.GetBytes(world));
        var initial=session.ReadSlot(1);Require(initial.state==TowerRunSlotState.Empty,"ShopFixtureMustHaveEmptySlot");
        var saved=session.TrySaveEntranceCandidate(1,entry,false,initial.confirmationToken);Require(saved.succeeded,"ShopEntranceSaveFailed:"+saved.message);
        string path=Path.Combine(TowerRunRuntimeEnvironment.SlotRoot,"tower-run-slot-1.json");byte[] checkpoint=File.ReadAllBytes(path);File.WriteAllBytes(Path.Combine(output,"Shop-Checkpoint.json"),checkpoint);shopEvidence.slotSha=MapHostCompatibility.Sha(checkpoint);
        Vector2 spawn=FootPosition;ShopPersist();yield return null;yield return null;
        Require(FindObjectsOfType<ShopTradeController>(true).Length==1&&FindObjectsOfType<EventSystem>().Length==1,"ShopDuplicateAuthorityOrEventSystem");
        var merchant=FindObjectsOfType<MerchantInteractionController>().First(m=>m.BoundPlayer==body.transform);
        yield return SetupFoot((Vector2)merchant.transform.position+Vector2.down*.85f,"SHOP_EXPLICIT_PROXIMITY_FIXTURE_NOT_WALK");
        yield return new WaitForFixedUpdate();yield return null;
        shopEvidence.checks.Add("Before F: range="+PrivateField<bool>(merchant,"isPlayerInRange")+" distance="+Vector2.Distance(body.transform.position,merchant.transform.position)+" input="+GameInput.Instance.IsGameplayInputEnabled+" gate="+TowerRunGateHub.IsGameplayBlocked+" state="+session.State+" update="+InputSystem.settings.updateMode);ShopPersist();
        Require(PrivateField<bool>(merchant,"isPlayerInRange")&&GameInput.Instance.IsGameplayInputEnabled,"ShopProximityFixtureNotReady");
        Require(!panel.IsShopPanelVisible,"ShopOpenedWithoutInteract");int interactEvents=0,merchantEvents=0;
        Action observeInput=()=>interactEvents++;Action<MerchantInteractionController> observeMerchant=m=>{if(m==merchant)merchantEvents++;};
        GameInput.Instance.OnInteractPressed+=observeInput;MerchantInteractionController.OnAnyMerchantInteracted+=observeMerchant;
        try {yield return ShopKey(Key.F);}
        finally {GameInput.Instance.OnInteractPressed-=observeInput;MerchantInteractionController.OnAnyMerchantInteracted-=observeMerchant;}
        shopEvidence.checks.Add("F observed GameInput="+interactEvents+" realMerchant="+merchantEvents+" open="+panel.IsShopPanelVisible);ShopPersist();
        Require(interactEvents==1&&merchantEvents==1,"ShopInteractEventCountMismatch");
        Require(panel.IsShopPanelVisible&&panel.CurrentMerchant==merchant,"ShopRealMerchantFChainFailed");
        var presenter=FindObjectOfType<ShopMerchantPresentationUI>(true);var details=FindObjectOfType<ShopDetailsUI>(true);var trade=FindObjectOfType<ShopTradeController>(true);var inventory=FindObjectOfType<PlayerInventory>();
        Require(presenter.SourceMerchant==merchant&&presenter.IsVisible,"ShopPortraitNotFromActualMerchant");
        var slot=MeatSlot();var meatReward=slot.CurrentProduct.Reward;RequireAmounts(ShopCount(slot,trade,inventory),100,0,3);
        var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};slot.OnPointerEnter(pointer);yield return null;
        Require(details.CurrentProduct==slot.CurrentProduct,"ShopHoverDetailsMissing");CheckShopFonts();yield return ShopPicture("01-Blue-Food-Details");slot.OnPointerExit(pointer);yield return null;Require(details.CurrentProduct==null,"ShopHoverDetailsNotCleared");
        foreach(string tab in new[]{"WeaponTab","SkillTab","FoodTab"})
        {
            var toggle=FindObjectsOfType<UnityEngine.UI.Toggle>().Single(t=>t.name==tab);toggle.isOn=true;yield return null;
            int expected=tab=="FoodTab"?merchant.ShopCatalog.GetProducts(ShopProductCategoryType.Food).Count:0;Require(FindObjectsOfType<ShopSlotUI>().Count(s=>s.CurrentProduct!=null)==expected,"ShopCategoryWrong:"+tab);Require(details.CurrentProduct==null,"ShopCategoryDetailsResidual");
        }
        slot=MeatSlot();
        for(int i=1;i<=4;i++)
        {
            yield return ClickShopProduct(slot);int bought=Mathf.Min(i,3);RequireAmounts(ShopCount(slot,trade,inventory),100-5*bought,bought,3-bought);
        }
        CheckShopFonts();yield return ShopPicture("02-SoldOut-ThreeSinglePurchases");
        yield return ShopKey(Key.Escape);Require(!panel.IsShopPanelVisible&&!TowerRunGateHub.IsGameplayBlocked,"ShopEscDidNotReleaseInput");
        Require(details.CurrentProduct==null&&!presenter.IsVisible,"ShopCloseResidual");
        yield return SetupFoot(spawn,"SHOP_RETURN_TO_SPAWN_FIXTURE_NOT_WALK");Vector2 beforeMove=body.position;Feed(Key.D);yield return new WaitForSeconds(.18f);Feed();yield return null;
        Require(Vector2.Distance(body.position,beforeMove)>.02f&&GameInput.Instance.MoveInput.sqrMagnitude<.001f,"ShopMovementNotRestored");
        yield return ShopKey(Key.Tab);var package=FindObjectOfType<PackagePanelController>(true);Require(package.IsPackagePanelVisible,"ShopBackpackInputFailed");
        Require(inventory.GetItemAmount(meatReward)==3,"ShopBackpackMeatMismatch");
        var meatSlot=FindObjectsOfType<PackageSlotUI>().Single(s=>s.CurrentReward==meatReward);meatSlot.OnPointerClick(pointer);yield return null;
        var packageGrid=FindObjectOfType<PackageGridUI>(true);Require(PrivateField<PackageSlotUI>(packageGrid,"currentSelectedSlot")==meatSlot&&PrivateField<PackageSlotUI>(packageGrid,"currentTooltipSlot")==meatSlot,"ShopBackpackSelectionOrTooltipMissing");
        CheckShopFonts();yield return ShopPicture("03-Package-AfterTrade");yield return ShopKey(Key.Tab);Require(!package.IsPackagePanelVisible,"ShopBackpackCloseFailed");
        int cameras=FindObjectsOfType<Camera>(true).Length;
        // Style actors share the known lawful interaction location. Disable only the
        // generated merchant's input component while an explicit fixture owns that spot.
        merchant.enabled=false;
        try {foreach(var style in presenter.Styles.ToArray())
        {
            var actor=Instantiate(style.sourcePrefab,merchant.transform.position,Quaternion.identity).GetComponent<MerchantInteractionController>();
            try
            {
                string reason;Require(actor.TryBindCanonicalPlayer(body.transform,out reason),reason);
                yield return SetupFoot((Vector2)actor.transform.position+Vector2.down*.85f,"SHOP_STYLE_FIXTURE_NOT_WALK");yield return new WaitForFixedUpdate();yield return null;
                yield return ShopKey(Key.F);Require(panel.IsShopPanelVisible&&panel.CurrentMerchant==actor&&presenter.SourceMerchant==actor,"ShopStyleMerchantSourceMismatch");
                Require(presenter.CurrentStyle.controller==style.controller,"ShopBannerStyleMismatch");var row=new ShopMerchantRow{name=style.sourcePrefab.name,controller=style.controller.name,banner=style.titleBanner==null?"PATCH":style.titleBanner.name,actualMerchantReference=true};
                var priorSprite=presenter.PreviewRenderer.sprite;
                for(int frame=0;frame<8;frame++)
                {
                    yield return new WaitForSecondsRealtime(.12f);var sprite=presenter.PreviewRenderer.sprite;if(sprite!=priorSprite)row.spriteChanges++;priorSprite=sprite;
                    if(shopFrames&&style.sourcePrefab.name.Contains("Purple"))yield return ShopPicture("Idle/Purple-"+frame.ToString("D2"));
                }
                Require(row.spriteChanges>=2,"ShopMerchantIdleDidNotAdvance:"+row.name);shopEvidence.merchants.Add(row);ShopPersist();
                slot=MeatSlot();slot.OnPointerEnter(pointer);yield return null;CheckShopFonts();yield return ShopPicture("Merchant-"+style.sourcePrefab.name);
                var close=FindObjectsOfType<UnityEngine.UI.Button>().Single(b=>b.name=="CloseButton");close.onClick.Invoke();yield return null;yield return null;
                Require(!panel.IsShopPanelVisible&&!presenter.IsVisible&&!TowerRunGateHub.IsGameplayBlocked&&details.CurrentProduct==null,"ShopCloseButtonResidual");
                Require(FindObjectsOfType<Camera>(true).Length==cameras,"ShopPreviewCameraLeak");
            }
            finally {Destroy(actor.gameObject);}
            yield return null;
        }} finally {merchant.enabled=true;}
        yield return ShopKey(Key.Tab);meatSlot=FindObjectsOfType<PackageSlotUI>().Single(s=>s.CurrentReward==meatReward);meatSlot.OnPointerClick(pointer);yield return null;
        Require(inventory.TryRemoveItem(meatReward,3),"ShopAdrFixtureRemoveFailed");yield return null;yield return null;
        Require(PrivateField<PackageSlotUI>(packageGrid,"currentSelectedSlot")==null&&PrivateField<PackageSlotUI>(packageGrid,"currentTooltipSlot")==null,"ShopAdrRemovedItemStateResidual");
        yield return ShopKey(Key.Tab);Require(!package.IsPackagePanelVisible&&!TowerRunGateHub.IsGameplayBlocked,"ShopFinalPackageCleanupFailed");
        Require(MapHostCompatibility.Sha(File.ReadAllBytes(path))==shopEvidence.slotSha,"ShopTradeMustNotRewriteEntranceSave");
        shopEvidence.checks.Add("Normal SampleScene new-game button; generated merchant F path; categories/hover; EventSystem card hit and exact three purchases; sold-out unchanged; Esc/close cleanup; movement/Tab; four prefab-driven Idle/banner styles; private entrance disk save unchanged by purchases.");
        shopEvidence.checks.Add("Spawned style actors and player proximity placement are isolated test fixtures, never serialized into SampleScene or claimed as human movement.");
        yield return ProveFoodProducts();
        Require(MapHostCompatibility.Sha(File.ReadAllBytes(path))==shopEvidence.slotSha,"FoodTradeMustNotRewriteEntranceSave");
        shopEvidence.status="AUTOMATED_SHOP_FLOW_VERIFIED_HUMAN_PENDING";ShopPersist();
    }
}
#endif
