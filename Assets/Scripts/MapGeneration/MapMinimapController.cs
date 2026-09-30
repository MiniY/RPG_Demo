using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 根据 MapData（地图数据）显示数据驱动的小地图，并更新玩家位置标记。
/// </summary>
[DisallowMultipleComponent]
public sealed class MapMinimapController : MonoBehaviour
{
    /// <summary>
    /// 提供地图生成结果的控制器。
    /// </summary>
    [Header("Data Sources（数据来源）")]
    [SerializeField] private MapGenerationController mapController;

    /// <summary>
    /// 把玩家世界坐标转换为地图网格坐标的渲染器。
    /// </summary>
    [SerializeField] private MapTilemapRenderer tilemapRenderer;

    /// <summary>
    /// 需要在小地图上显示位置的玩家。
    /// </summary>
    [SerializeField] private Transform player;

    /// <summary>
    /// 显示小地图纹理的 RawImage（原始图像）组件。
    /// </summary>
    [Header("View（视图）")]
    [SerializeField] private RawImage minimapImage;

    /// <summary>
    /// 表示玩家当前位置的 UI 标记。
    /// </summary>
    [SerializeField] private RectTransform playerMarker;

    /// <summary>
    /// 覆盖在小地图上的战争迷雾 RawImage（原始图像）组件。
    /// </summary>
    [Header("Fog of War（战争迷雾）")]
    [SerializeField] private RawImage fogOverlay;

    /// <summary>
    /// 是否启用战争迷雾。
    /// </summary>
    [SerializeField] private bool enableFogOfWar = true;

    /// <summary>
    /// 玩家每次探索的圆形半径，单位是地图网格数量。
    /// </summary>
    [SerializeField, Min(0)] private int explorationRadius = 5;

    /// <summary>
    /// 未探索区域使用的遮罩颜色和透明度。
    /// </summary>
    [SerializeField] private Color fogColor = new Color(0f, 0f, 0f, 0.92f);

    /// <summary>
    /// 表示玩家出生点位置的 UI 标记。
    /// </summary>
    [Header("Map Markers（地图标记）")]
    [SerializeField] private RectTransform spawnMarker;

    /// <summary>
    /// 表示道路出口位置的 UI 标记。
    /// </summary>
    [SerializeField] private RectTransform exitMarker;

    /// <summary>
    /// 是否显示出生点标记。
    /// </summary>
    [SerializeField] private bool showSpawnMarker = true;

    /// <summary>
    /// 是否显示出口标记。
    /// </summary>
    [SerializeField] private bool showExitMarker = true;

    /// <summary>
    /// 没有手动配置 UI 时，是否在运行时自动创建小地图界面。
    /// </summary>
    [SerializeField] private bool createRuntimeView = true;

    /// <summary>
    /// 小地图外框的屏幕尺寸。
    /// </summary>
    [SerializeField, Min(96f)] private float minimapSize = 192f;

    /// <summary>
    /// 小地图与屏幕右上角的间距。
    /// </summary>
    [SerializeField, Min(0f)] private float screenMargin = 16f;

    /// <summary>
    /// 小地图底图与外框之间的内边距。
    /// </summary>
    [SerializeField, Min(0f)] private float contentPadding = 6f;

    /// <summary>
    /// 玩家标记的屏幕尺寸。
    /// </summary>
    [SerializeField, Min(2f)] private float markerSize = 10f;

    /// <summary>
    /// 小地图中草地使用的颜色。
    /// </summary>
    [Header("Palette（颜色）")]
    [SerializeField] private Color32 grassColor = new Color32(76, 148, 78, 255);

    /// <summary>
    /// 小地图中水域使用的颜色。
    /// </summary>
    [SerializeField] private Color32 waterColor = new Color32(47, 112, 166, 255);

    /// <summary>
    /// 小地图中道路使用的颜色。
    /// </summary>
    [SerializeField] private Color32 pathColor = new Color32(201, 174, 105, 255);

    /// <summary>
    /// 小地图中未识别地形使用的颜色。
    /// </summary>
    [SerializeField] private Color32 fallbackColor = new Color32(128, 128, 128, 255);

    /// <summary>
    /// 小地图外框的背景颜色。
    /// </summary>
    [SerializeField] private Color panelColor = new Color(0.04f, 0.06f, 0.08f, 0.9f);

    /// <summary>
    /// 玩家标记使用的颜色。
    /// </summary>
    [SerializeField] private Color markerColor = new Color(1f, 0.9f, 0.2f, 1f);

    /// <summary>
    /// 出生点标记使用的颜色。
    /// </summary>
    [SerializeField] private Color spawnMarkerColor = new Color(0.3f, 1f, 0.45f, 1f);

    /// <summary>
    /// 出口标记使用的颜色。
    /// </summary>
    [SerializeField] private Color exitMarkerColor = new Color(1f, 0.35f, 0.3f, 1f);

    /// <summary>
    /// 当前生成的小地图纹理。
    /// </summary>
    private Texture2D minimapTexture;

    /// <summary>
    /// 当前生成的战争迷雾纹理。
    /// </summary>
    private Texture2D fogTexture;

    /// <summary>
    /// 战争迷雾纹理的像素缓存，透明像素代表已探索区域。
    /// </summary>
    private Color32[] fogPixels;

    /// <summary>
    /// 当前已经显示的小地图数据。
    /// </summary>
    private MapData displayedMap;

    /// <summary>
    /// 当前地图的逻辑探索状态。
    /// </summary>
    private MapExplorationState explorationState;

    /// <summary>
    /// 上一次触发探索更新时玩家所在的网格。
    /// </summary>
    private Vector2Int lastExplorationCell;

    /// <summary>
    /// 是否已经记录过上一次探索网格。
    /// </summary>
    private bool hasLastExplorationCell;

    /// <summary>
    /// 运行时自动创建的 Canvas（画布）对象。
    /// </summary>
    private GameObject runtimeCanvasObject;

    /// <summary>
    /// 小地图图像的宽高适配组件。
    /// </summary>
    private AspectRatioFitter aspectRatioFitter;

    /// <summary>
    /// 获取当前显示的地图数据，便于测试和其他系统读取状态。
    /// </summary>
    public MapData DisplayedMap => displayedMap;

    /// <summary>
    /// 获取当前使用的地图生成控制器。
    /// </summary>
    public MapGenerationController MapController => mapController;

    /// <summary>
    /// 获取当前使用的 Tilemap（瓦片地图）渲染器。
    /// </summary>
    public MapTilemapRenderer TilemapRenderer => tilemapRenderer;

    /// <summary>
    /// 获取当前显示位置所使用的玩家对象。
    /// </summary>
    public Transform Player => player;

    /// <summary>
    /// 获取出生点 UI 标记引用。
    /// </summary>
    public RectTransform SpawnMarker => spawnMarker;

    /// <summary>
    /// 获取出口 UI 标记引用。
    /// </summary>
    public RectTransform ExitMarker => exitMarker;

    /// <summary>
    /// 获取当前生成的小地图纹理，便于调试和测试。
    /// </summary>
    public Texture2D MinimapTexture => minimapTexture;

    /// <summary>
    /// 获取当前生成的战争迷雾纹理，便于调试和测试。
    /// </summary>
    public Texture2D FogTexture => fogTexture;

    /// <summary>
    /// 获取当前地图的探索状态，便于测试和保存系统读取。
    /// </summary>
    public MapExplorationState ExplorationState => explorationState;

    /// <summary>
    /// 获取战争迷雾 UI 覆盖层引用。
    /// </summary>
    public RawImage FogOverlay => fogOverlay;

    /// <summary>
    /// 缓存依赖并在运行时创建默认界面。
    /// </summary>
    private void Awake()
    {
        ResolveReferences();

        if (Application.isPlaying && createRuntimeView)
            EnsureRuntimeView();
    }

    /// <summary>
    /// 订阅地图生成事件，保证每次生成后只重建一次底图。
    /// </summary>
    private void OnEnable()
    {
        ResolveReferences();

        if (mapController == null)
            return;

        mapController.MapGenerated -= HandleMapGenerated;
        mapController.MapGenerated += HandleMapGenerated;
        mapController.MapCleared -= HandleMapCleared;
        mapController.MapCleared += HandleMapCleared;
    }

    /// <summary>
    /// 处理控制器可能早于本组件生成地图的情况。
    /// </summary>
    private void Start()
    {
        if (mapController != null && mapController.LastGeneratedMap != null)
            RenderMap(mapController.LastGeneratedMap);
    }

    /// <summary>
    /// 每帧只更新玩家标记，不重新构建小地图底图纹理。
    /// </summary>
    private void LateUpdate()
    {
        UpdateExplorationAroundPlayer();
        UpdatePlayerMarker();
    }

    /// <summary>
    /// 取消事件订阅，避免对象销毁后仍被控制器调用。
    /// </summary>
    private void OnDisable()
    {
        if (mapController == null)
            return;

        mapController.MapGenerated -= HandleMapGenerated;
        mapController.MapCleared -= HandleMapCleared;
    }

    /// <summary>
    /// 释放运行时创建的纹理和界面对象。
    /// </summary>
    private void OnDestroy()
    {
        ReleaseTexture();
        ReleaseFogTexture();

        if (runtimeCanvasObject != null)
            Destroy(runtimeCanvasObject);
    }

    /// <summary>
    /// 响应地图生成完成事件。
    /// </summary>
    /// <param name="mapData">刚刚生成的地图数据。</param>
    private void HandleMapGenerated(MapData mapData)
    {
        RenderMap(mapData);
    }

    /// <summary>
    /// 响应地图清空事件并隐藏小地图内容。
    /// </summary>
    private void HandleMapCleared()
    {
        ClearMap();
    }

    /// <summary>
    /// 构建并显示一张新的小地图底图。
    /// </summary>
    /// <param name="mapData">需要显示的地图数据。</param>
    public void RenderMap(MapData mapData)
    {
        if (mapData == null)
            return;

        EnsureRuntimeView();

        if (minimapImage == null)
        {
            if (!Application.isPlaying)
            {
                displayedMap = mapData;
                return;
            }

            Debug.LogWarning("MapMinimapController 缺少 Minimap Image，无法显示小地图。", this);
            return;
        }

        Color32[] pixels = MapMinimapRasterizer.BuildColorBuffer(
            mapData,
            grassColor,
            waterColor,
            pathColor,
            fallbackColor);

        CreateOrResizeTexture(mapData.Width, mapData.Height);
        minimapTexture.SetPixels32(pixels);
        minimapTexture.Apply(false, false);
        minimapImage.texture = minimapTexture;
        minimapImage.color = Color.white;
        minimapImage.enabled = true;

        displayedMap = mapData;
        UpdateAspectRatio(mapData);
        ResetExploration(mapData);
        UpdateMapMarkers();
        UpdatePlayerMarker();
    }

    /// <summary>
    /// 清空当前小地图纹理并隐藏小地图内容。
    /// </summary>
    public void ClearMap()
    {
        displayedMap = null;
        explorationState = null;
        hasLastExplorationCell = false;
        ReleaseTexture();
        ReleaseFogTexture();

        if (minimapImage != null)
        {
            minimapImage.texture = null;
            minimapImage.enabled = false;
        }

        if (playerMarker != null)
            playerMarker.gameObject.SetActive(false);

        if (spawnMarker != null)
            spawnMarker.gameObject.SetActive(false);

        if (exitMarker != null)
            exitMarker.gameObject.SetActive(false);

        if (fogOverlay != null)
        {
            fogOverlay.texture = null;
            fogOverlay.enabled = false;
        }
    }

    /// <summary>
    /// 解析 Inspector（检视面板）引用，并从地图控制器补齐默认依赖。
    /// </summary>
    private void ResolveReferences()
    {
        if (mapController == null)
            mapController = GetComponent<MapGenerationController>();

        if (mapController == null)
            mapController = FindObjectOfType<MapGenerationController>();

        if (mapController != null)
        {
            if (tilemapRenderer == null)
                tilemapRenderer = mapController.TilemapRenderer;

            if (player == null)
                player = mapController.Player;
        }

        if (tilemapRenderer == null)
            tilemapRenderer = FindObjectOfType<MapTilemapRenderer>();
    }

    /// <summary>
    /// 确保运行时小地图界面存在；手动配置的界面会被优先保留。
    /// </summary>
    private void EnsureRuntimeView()
    {
        if (minimapImage != null && playerMarker != null)
            return;

        if (!createRuntimeView || !Application.isPlaying)
            return;

        if (runtimeCanvasObject == null)
            CreateRuntimeView();
    }

    /// <summary>
    /// 创建屏幕右上角的小地图、外框和玩家标记。
    /// </summary>
    private void CreateRuntimeView()
    {
        runtimeCanvasObject = new GameObject(
            "GeneratedMinimapCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        runtimeCanvasObject.transform.SetParent(transform, false);

        Canvas canvas = runtimeCanvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = runtimeCanvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = new GameObject(
            "MinimapPanel",
            typeof(RectTransform),
            typeof(RawImage));
        panelObject.transform.SetParent(runtimeCanvasObject.transform, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.one;
        panelRect.anchorMax = Vector2.one;
        panelRect.pivot = Vector2.one;
        panelRect.sizeDelta = new Vector2(minimapSize, minimapSize);
        panelRect.anchoredPosition = new Vector2(-screenMargin, -screenMargin);

        RawImage panelImage = panelObject.GetComponent<RawImage>();
        panelImage.texture = Texture2D.whiteTexture;
        panelImage.color = panelColor;
        panelImage.raycastTarget = false;

        GameObject mapObject = new GameObject(
            "MinimapImage",
            typeof(RectTransform),
            typeof(RawImage),
            typeof(AspectRatioFitter));
        mapObject.transform.SetParent(panelObject.transform, false);

        RectTransform mapRect = mapObject.GetComponent<RectTransform>();
        mapRect.anchorMin = Vector2.zero;
        mapRect.anchorMax = Vector2.one;
        mapRect.offsetMin = new Vector2(contentPadding, contentPadding);
        mapRect.offsetMax = new Vector2(-contentPadding, -contentPadding);

        minimapImage = mapObject.GetComponent<RawImage>();
        minimapImage.raycastTarget = false;
        aspectRatioFitter = mapObject.GetComponent<AspectRatioFitter>();
        aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspectRatioFitter.aspectRatio = 1f;

        fogOverlay = CreateRuntimeFogOverlay();

        GameObject markerObject = new GameObject(
            "PlayerMarker",
            typeof(RectTransform),
            typeof(RawImage));
        markerObject.transform.SetParent(mapObject.transform, false);

        playerMarker = markerObject.GetComponent<RectTransform>();
        playerMarker.anchorMin = new Vector2(0.5f, 0.5f);
        playerMarker.anchorMax = new Vector2(0.5f, 0.5f);
        playerMarker.pivot = new Vector2(0.5f, 0.5f);
        playerMarker.sizeDelta = new Vector2(markerSize, markerSize);
        playerMarker.localRotation = Quaternion.Euler(0f, 0f, 45f);

        RawImage markerImage = markerObject.GetComponent<RawImage>();
        markerImage.texture = Texture2D.whiteTexture;
        markerImage.color = markerColor;
        markerImage.raycastTarget = false;

        spawnMarker = CreateRuntimeMarker(
            "SpawnMarker",
            spawnMarkerColor,
            Quaternion.identity);
        exitMarker = CreateRuntimeMarker(
            "ExitMarker",
            exitMarkerColor,
            Quaternion.identity);
    }

    /// <summary>
    /// 创建覆盖在地图底图上、但位于各种位置标记下方的战争迷雾图像。
    /// </summary>
    /// <returns>新建的战争迷雾 RawImage（原始图像）组件。</returns>
    private RawImage CreateRuntimeFogOverlay()
    {
        GameObject fogObject = new GameObject(
            "FogOverlay",
            typeof(RectTransform),
            typeof(RawImage));
        fogObject.transform.SetParent(minimapImage.transform, false);

        RectTransform fogRect = fogObject.GetComponent<RectTransform>();
        fogRect.anchorMin = Vector2.zero;
        fogRect.anchorMax = Vector2.one;
        fogRect.offsetMin = Vector2.zero;
        fogRect.offsetMax = Vector2.zero;

        RawImage fogImage = fogObject.GetComponent<RawImage>();
        fogImage.raycastTarget = false;
        fogImage.color = Color.white;
        fogImage.transform.SetAsFirstSibling();
        return fogImage;
    }

    /// <summary>
    /// 创建一个运行时位置标记，并把它放在小地图图像上。
    /// </summary>
    /// <param name="markerName">标记对象名称。</param>
    /// <param name="color">标记颜色。</param>
    /// <param name="rotation">标记旋转。</param>
    /// <returns>创建好的标记矩形变换组件。</returns>
    private RectTransform CreateRuntimeMarker(
        string markerName,
        Color color,
        Quaternion rotation)
    {
        GameObject markerObject = new GameObject(
            markerName,
            typeof(RectTransform),
            typeof(RawImage));
        markerObject.transform.SetParent(minimapImage.transform, false);

        RectTransform markerRect = markerObject.GetComponent<RectTransform>();
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = new Vector2(markerSize, markerSize);
        markerRect.localRotation = rotation;

        RawImage markerImage = markerObject.GetComponent<RawImage>();
        markerImage.texture = Texture2D.whiteTexture;
        markerImage.color = color;
        markerImage.raycastTarget = false;
        return markerRect;
    }

    /// <summary>
    /// 创建或调整与地图尺寸一致的点过滤纹理。
    /// </summary>
    /// <param name="width">地图宽度。</param>
    /// <param name="height">地图高度。</param>
    private void CreateOrResizeTexture(int width, int height)
    {
        if (minimapTexture != null &&
            minimapTexture.width == width &&
            minimapTexture.height == height)
        {
            return;
        }

        ReleaseTexture();
        minimapTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        minimapTexture.name = "GeneratedMinimapTexture";
        minimapTexture.filterMode = FilterMode.Point;
        minimapTexture.wrapMode = TextureWrapMode.Clamp;
    }

    /// <summary>
    /// 根据地图宽高调整 UI 图像的宽高比。
    /// </summary>
    /// <param name="mapData">当前地图数据。</param>
    private void UpdateAspectRatio(MapData mapData)
    {
        if (aspectRatioFitter == null)
            aspectRatioFitter = minimapImage.GetComponent<AspectRatioFitter>();

        if (aspectRatioFitter != null)
            aspectRatioFitter.aspectRatio = (float)mapData.Width / mapData.Height;
    }

    /// <summary>
    /// 为新地图重置逻辑探索状态和战争迷雾纹理。
    /// </summary>
    /// <param name="mapData">刚刚生成的地图数据。</param>
    private void ResetExploration(MapData mapData)
    {
        explorationState = new MapExplorationState(mapData);
        hasLastExplorationCell = false;
        CreateOrResizeFogTexture(mapData.Width, mapData.Height);

        if (fogOverlay != null)
        {
            fogOverlay.texture = fogTexture;
            fogOverlay.color = Color.white;
            fogOverlay.raycastTarget = false;
            fogOverlay.transform.SetAsFirstSibling();
            fogOverlay.enabled = enableFogOfWar;
        }

        ApplyFogTexture();

        if (enableFogOfWar)
            UpdateExplorationAroundPlayer();
    }

    /// <summary>
    /// 在玩家进入新网格时揭示周围区域，并更新战争迷雾纹理。
    /// </summary>
    private void UpdateExplorationAroundPlayer()
    {
        if (displayedMap == null || explorationState == null || player == null)
            return;

        if (fogOverlay != null)
            fogOverlay.enabled = enableFogOfWar;

        if (!enableFogOfWar)
            return;

        Vector2Int playerCell = GetPlayerCell();
        if (hasLastExplorationCell && playerCell == lastExplorationCell)
            return;

        explorationState.RevealAround(playerCell, explorationRadius);
        lastExplorationCell = playerCell;
        hasLastExplorationCell = true;
        ApplyFogTexture();
    }

    /// <summary>
    /// 根据逻辑探索状态重建战争迷雾像素。
    /// </summary>
    private void ApplyFogTexture()
    {
        if (fogTexture == null || explorationState == null || fogPixels == null)
            return;

        Color32 unexploredColor = fogColor;
        Color32 exploredColor = new Color32(0, 0, 0, 0);

        for (int localY = 0; localY < explorationState.Height; localY++)
        {
            for (int localX = 0; localX < explorationState.Width; localX++)
            {
                Vector2Int cell = explorationState.Origin + new Vector2Int(localX, localY);
                int pixelIndex = localY * explorationState.Width + localX;
                fogPixels[pixelIndex] = explorationState.IsExplored(cell)
                    ? exploredColor
                    : unexploredColor;
            }
        }

        fogTexture.SetPixels32(fogPixels);
        fogTexture.Apply(false, false);
    }

    /// <summary>
    /// 创建或调整与地图尺寸一致的战争迷雾纹理。
    /// </summary>
    /// <param name="width">地图宽度。</param>
    /// <param name="height">地图高度。</param>
    private void CreateOrResizeFogTexture(int width, int height)
    {
        if (fogTexture != null && fogTexture.width == width && fogTexture.height == height)
        {
            fogPixels = new Color32[width * height];
            return;
        }

        ReleaseFogTexture();
        fogTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        fogTexture.name = "GeneratedFogTexture";
        fogTexture.filterMode = FilterMode.Point;
        fogTexture.wrapMode = TextureWrapMode.Clamp;
        fogPixels = new Color32[width * height];
    }

    /// <summary>
    /// 根据当前地图数据更新出生点和出口标记的位置与可见状态。
    /// </summary>
    private void UpdateMapMarkers()
    {
        if (displayedMap == null)
            return;

        UpdateStaticMarker(
            spawnMarker,
            displayedMap.SpawnCell,
            showSpawnMarker,
            spawnMarkerColor);
        UpdateStaticMarker(
            exitMarker,
            displayedMap.ExitCell,
            showExitMarker,
            exitMarkerColor);
    }

    /// <summary>
    /// 设置一个固定地图标记的归一化位置、颜色和可见状态。
    /// </summary>
    /// <param name="marker">待更新的 UI 标记。</param>
    /// <param name="cell">标记对应的地图网格坐标。</param>
    /// <param name="shouldShow">是否允许显示该标记。</param>
    /// <param name="color">标记颜色。</param>
    private void UpdateStaticMarker(
        RectTransform marker,
        Vector2Int cell,
        bool shouldShow,
        Color color)
    {
        if (marker == null)
            return;

        bool isInsideMap = displayedMap.IsInside(cell);
        marker.gameObject.SetActive(shouldShow && isInsideMap);

        if (!shouldShow || !isInsideMap)
            return;

        Vector2 normalizedPosition = MapMinimapRasterizer.CellToNormalizedPosition(
            displayedMap,
            cell);
        marker.anchorMin = normalizedPosition;
        marker.anchorMax = normalizedPosition;
        marker.anchoredPosition = Vector2.zero;
        SetMarkerColor(marker, color);
    }

    /// <summary>
    /// 修改 UI 标记上的 Graphic（图形）颜色，同时兼容 Image 和 RawImage。
    /// </summary>
    /// <param name="marker">待修改的 UI 标记。</param>
    /// <param name="color">目标颜色。</param>
    private static void SetMarkerColor(RectTransform marker, Color color)
    {
        Graphic graphic = marker.GetComponent<Graphic>();
        if (graphic != null)
            graphic.color = color;
    }

    /// <summary>
    /// 根据玩家所在网格更新标记锚点位置。
    /// </summary>
    private void UpdatePlayerMarker()
    {
        if (displayedMap == null || player == null || playerMarker == null)
            return;

        Vector2Int playerCell = GetPlayerCell();
        bool isInsideMap = displayedMap.IsInside(playerCell);
        playerMarker.gameObject.SetActive(isInsideMap);

        if (!isInsideMap)
            return;

        Vector2 normalizedPosition = MapMinimapRasterizer.CellToNormalizedPosition(
            displayedMap,
            playerCell);
        playerMarker.anchorMin = normalizedPosition;
        playerMarker.anchorMax = normalizedPosition;
        playerMarker.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// 将玩家世界坐标转换为地图网格坐标。
    /// </summary>
    /// <returns>玩家当前所在的网格坐标。</returns>
    private Vector2Int GetPlayerCell()
    {
        if (tilemapRenderer != null && tilemapRenderer.GroundTilemap != null)
        {
            Vector3Int cell = tilemapRenderer.GroundTilemap.WorldToCell(player.position);
            return new Vector2Int(cell.x, cell.y);
        }

        return new Vector2Int(
            Mathf.FloorToInt(player.position.x),
            Mathf.FloorToInt(player.position.y));
    }

    /// <summary>
    /// 销毁当前运行时纹理，避免重新生成地图时积累纹理对象。
    /// </summary>
    private void ReleaseTexture()
    {
        if (minimapTexture == null)
            return;

        if (Application.isPlaying)
            Destroy(minimapTexture);
        else
            DestroyImmediate(minimapTexture);

        minimapTexture = null;
    }

    /// <summary>
    /// 销毁当前战争迷雾纹理，避免重新生成地图时积累纹理对象。
    /// </summary>
    private void ReleaseFogTexture()
    {
        if (fogTexture == null)
            return;

        if (Application.isPlaying)
            Destroy(fogTexture);
        else
            DestroyImmediate(fogTexture);

        fogTexture = null;
        fogPixels = null;
    }

    /// <summary>
    /// 在 Inspector（检视面板）修改参数时修正 UI 配置。
    /// </summary>
    private void OnValidate()
    {
        minimapSize = Mathf.Max(96f, minimapSize);
        screenMargin = Mathf.Max(0f, screenMargin);
        contentPadding = Mathf.Clamp(contentPadding, 0f, minimapSize * 0.5f);
        markerSize = Mathf.Max(2f, markerSize);
        explorationRadius = Mathf.Max(0, explorationRadius);
        fogColor.a = Mathf.Clamp01(fogColor.a);
    }
}
