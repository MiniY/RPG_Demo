using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class UIPageConfiguration
{
    public string Id;
    public string DisplayName;
    public UILayer Layer;
    public string Workspace;
    public UIBackPolicy BackPolicy;
    public bool BlockGameplay;
    public bool PauseWorld;
    public bool Cache = true;
    [Range(0, 1)] public float DimAlpha;
    public bool TemplateOnly;

    internal UIPageDefinition Bind(UIPage page) => new UIPageDefinition {
        Id = Id, Layer = Layer, Workspace = Workspace, BackPolicy = BackPolicy,
        BlockGameplay = BlockGameplay, PauseWorld = PauseWorld, Cache = Cache,
        DimAlpha = DimAlpha, SceneInstance = page.gameObject, DefaultFocus = page.DefaultFocus
    };
}

[CreateAssetMenu(menuName = "RPG/UI/Page Catalog", fileName = "UIPageCatalog")]
public sealed class UIPageCatalog : ScriptableObject
{
    [SerializeField] private List<UIPageConfiguration> pages = new List<UIPageConfiguration>();
    public IReadOnlyList<UIPageConfiguration> Pages => pages.AsReadOnly();
    public void Validate()
    {
        if (pages == null || pages.Count == 0) throw new InvalidOperationException("UI page catalog is empty");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (UIPageConfiguration page in pages)
        {
            if (page == null || string.IsNullOrWhiteSpace(page.Id) || !ids.Add(page.Id))
                throw new InvalidOperationException("Missing or duplicate UI page ID");
            if (!Enum.IsDefined(typeof(UILayer), page.Layer) || !Enum.IsDefined(typeof(UIBackPolicy), page.BackPolicy) ||
                float.IsNaN(page.DimAlpha) || page.DimAlpha < 0 || page.DimAlpha > 1)
                throw new InvalidOperationException("Invalid UI page rules: " + page.Id);
            if (page.TemplateOnly && (page.Layer != UILayer.Modal || page.Cache))
                throw new InvalidOperationException("Dialog templates require Modal and Cache=false: " + page.Id);
            if (!page.TemplateOnly && !page.Cache)
                throw new InvalidOperationException("Authored scene pages must remain cached; use a dialog template for disposable instances: " + page.Id);
        }
    }
    public UIPageConfiguration Find(string id)
    {
        foreach (var page in pages) if (page != null && page.Id == id) return page;
        throw new InvalidOperationException("Page is absent from the UI catalog: " + id);
    }
    public UIPageDefinition CreateDefinition(UIPage page)
    {
        UIPageConfiguration rule = Find(page.PageId);
        if (rule.TemplateOnly != page.IsTemplate) throw new InvalidOperationException("Page/template binding differs: " + page.PageId);
        return rule.Bind(page);
    }
}
