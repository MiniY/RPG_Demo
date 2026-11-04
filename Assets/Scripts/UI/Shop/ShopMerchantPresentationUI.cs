using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>从已被商店接受的真实商人克隆 Visual，仅承担动画展示，不复制交互或交易状态。</summary>
public sealed class ShopMerchantPresentationUI : MonoBehaviour
{
    [Serializable] public sealed class MerchantStyle
    {
        public GameObject sourcePrefab;
        public RuntimeAnimatorController controller;
        public Sprite titleBanner;
        public bool segmentedBanner;
        public Color bodyColor=Color.white;
        public Vector2 visualCenter, visualSize;
    }
    [SerializeField] private ShopPanelController shopPanel;
    [SerializeField] private RawImage portrait;
    [SerializeField] private Image titleBanner;
    [SerializeField] private List<MerchantStyle> styles=new List<MerchantStyle>();
    [SerializeField] private ShopPatchGraphic purpleBannerPatch;
    private MerchantInteractionController sourceMerchant;
    private GameObject previewRoot, visual;
    private Camera previewCamera;
    private RenderTexture target;
    private Texture2D pointTexture;
    public IReadOnlyList<MerchantStyle> Styles=>styles;
    public Animator PreviewAnimator {get;private set;}
    public SpriteRenderer PreviewRenderer {get;private set;}
    public MerchantStyle CurrentStyle {get;private set;}
    public MerchantInteractionController SourceMerchant=>sourceMerchant;
    public bool IsVisible=>portrait!=null&&portrait.isActiveAndEnabled&&visual!=null&&visual.activeSelf;
    private void OnEnable()
    {
        if(shopPanel!=null)shopPanel.OnShopPanelVisibilityChanged+=OnVisibility;
    }
    private void OnDisable()
    {
        if(shopPanel!=null)shopPanel.OnShopPanelVisibilityChanged-=OnVisibility;
        Hide();
    }
    private void OnVisibility(bool visible)
    {
        if(!visible){Hide();return;}
        // The saved inactive nested Canvas reported overrideSorting=false; apply the modal layer after activation.
        var modal=portrait.GetComponentInParent<Canvas>();
        modal.overrideSorting=true;modal.sortingOrder=120;
        Bind(shopPanel.CurrentMerchant);
    }
    private void LateUpdate()
    {
        if(shopPanel==null||!shopPanel.IsShopPanelVisible)return;
        if(sourceMerchant!=shopPanel.CurrentMerchant)Bind(shopPanel.CurrentMerchant);
    }
    private void Bind(MerchantInteractionController merchant)
    {
        if(merchant==null){Hide();return;}
        var source=merchant.transform.Find("Visual");
        var animator=source==null?null:source.GetComponent<Animator>();
        if(animator==null)throw new InvalidOperationException("Merchant Visual/Animator missing");
        CurrentStyle=styles.Find(x=>x.controller==animator.runtimeAnimatorController);
        if(CurrentStyle==null)throw new InvalidOperationException("Unregistered merchant controller: "+animator.runtimeAnimatorController);
        sourceMerchant=merchant;
        if(previewRoot==null)
        {
            previewRoot=new GameObject("ShopMerchantPreview (owned visual only)");
            previewRoot.transform.position=new Vector3(100000,100000,0);
            previewCamera=new GameObject("PortraitCamera").AddComponent<Camera>();
            previewCamera.transform.SetParent(previewRoot.transform,false);previewCamera.transform.localPosition=new Vector3(0,0,-10);
            previewCamera.orthographic=true;previewCamera.clearFlags=CameraClearFlags.SolidColor;previewCamera.backgroundColor=Color.clear;
            previewCamera.cullingMask=1<<31;previewCamera.nearClipPlane=.1f;previewCamera.farClipPlane=20;previewCamera.allowHDR=false;previewCamera.allowMSAA=false;
            target=new RenderTexture(512,512,16,RenderTextureFormat.ARGB32){name="ShopMerchantPortrait",filterMode=FilterMode.Point,antiAliasing=1};target.Create();
            previewCamera.targetTexture=target;portrait.texture=target;
        }
        if(visual!=null){visual.SetActive(false);Destroy(visual);}
        if(pointTexture!=null)Destroy(pointTexture);
        visual=Instantiate(source.gameObject,previewRoot.transform,false);visual.name="Visual (from "+merchant.name+")";
        visual.transform.localPosition=source.localPosition-(Vector3)CurrentStyle.visualCenter;
        foreach(var child in visual.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
        // The audited Visual contains no merchant interaction or state scripts.
        foreach(var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))behaviour.enabled=false;
        PreviewAnimator=visual.GetComponent<Animator>();PreviewRenderer=visual.GetComponent<SpriteRenderer>();
        PreviewAnimator.updateMode=AnimatorUpdateMode.UnscaledTime;PreviewAnimator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        PreviewAnimator.Rebind();PreviewAnimator.Play("MerchantIdle",0,0);PreviewAnimator.Update(0);
        // Object.Instantiate on this non-readable runtime texture yielded an empty GPU copy.
        // Read back the original atlas through a temporary render target before uploading a private Point copy.
        var originalTexture=PreviewRenderer.sprite.texture;
        var staging=RenderTexture.GetTemporary(originalTexture.width,originalTexture.height,0,RenderTextureFormat.ARGB32);
        var previousTarget=RenderTexture.active;
        try
        {
            Graphics.Blit(originalTexture,staging);RenderTexture.active=staging;
            pointTexture=new Texture2D(originalTexture.width,originalTexture.height,TextureFormat.RGBA32,false){name="Shop-only Point texture",filterMode=FilterMode.Point};
            pointTexture.ReadPixels(new Rect(0,0,originalTexture.width,originalTexture.height),0,0);
            pointTexture.Apply(false,true);
        }
        finally{RenderTexture.active=previousTarget;RenderTexture.ReleaseTemporary(staging);}
        var properties=new MaterialPropertyBlock();PreviewRenderer.GetPropertyBlock(properties);properties.SetTexture("_MainTex",pointTexture);PreviewRenderer.SetPropertyBlock(properties);
        previewCamera.orthographicSize=Mathf.Max(CurrentStyle.visualSize.x,CurrentStyle.visualSize.y)*.58f;
        previewCamera.enabled=true;portrait.enabled=true;visual.SetActive(true);titleBanner.sprite=CurrentStyle.titleBanner;
        titleBanner.enabled=!CurrentStyle.segmentedBanner;
        if(purpleBannerPatch!=null)
        {
            purpleBannerPatch.enabled=CurrentStyle.segmentedBanner;
            if(CurrentStyle.segmentedBanner)purpleBannerPatch.Configure(CurrentStyle.titleBanner.texture,
                new[]{new Rect(0,448,128,128),new Rect(192,448,64,128),new Rect(320,448,128,128)},new Vector2(98,72),.75f,true);
        }
    }
    private void Hide()
    {
        if(visual!=null)visual.SetActive(false);
        if(previewCamera!=null)previewCamera.enabled=false;
        if(portrait!=null)portrait.enabled=false;
    }
    private void OnDestroy()
    {
        if(previewRoot!=null)Destroy(previewRoot);
        if(pointTexture!=null)Destroy(pointTexture);
        if(target!=null){target.Release();Destroy(target);}
    }
}
