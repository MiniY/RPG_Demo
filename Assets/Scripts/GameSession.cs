using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 跨场景保存玩家运行时数据，并协调场景中的 UI（用户界面）与玩家对象。
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameSession : MonoBehaviour
{
    /// <summary>
    /// 当前游戏会话单例。
    /// </summary>
    public static GameSession Instance { get; private set; }

    /// <summary>
    /// 当前会话中的玩家属性数据。
    /// </summary>
    public PlayerStatsRuntime PlayerStats { get; private set; }

    /// <summary>
    /// 当前会话中的玩家成长服务。
    /// </summary>
    public PlayerProgressionService ProgressionService { get; private set; }

    /// <summary>
    /// 自动创建跨场景游戏会话。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        GameObject sessionObject = new GameObject("GameSession");
        sessionObject.AddComponent<GameSession>();
    }

    /// <summary>
    /// 初始化单例、属性数据和成长服务。
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        PlayerStats = GetComponent<PlayerStatsRuntime>();

        if (PlayerStats == null)
            PlayerStats = gameObject.AddComponent<PlayerStatsRuntime>();

        ProgressionService = GetComponent<PlayerProgressionService>();

        if (ProgressionService == null)
            ProgressionService = gameObject.AddComponent<PlayerProgressionService>();

        PlayerStats.EnsureInitializedForRuntime();
        ProgressionService.Initialize(PlayerStats);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    /// <summary>
    /// 首个场景已经加载后绑定运行时组件。
    /// </summary>
    private void Start()
    {
        BindSceneObjects();
    }

    /// <summary>
    /// 销毁时取消场景事件监听。
    /// </summary>
    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Instance = null;
    }

    /// <summary>
    /// 应用切换到后台时保存当前运行时数据。
    /// </summary>
    /// <param name="pauseStatus">是否进入暂停或后台状态。</param>
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            SaveRuntimeState();
    }

    /// <summary>
    /// 应用退出前保存当前运行时数据。
    /// </summary>
    private void OnApplicationQuit()
    {
        SaveRuntimeState();
    }

    /// <summary>
    /// 将当前属性和背包数据写入本地存档。
    /// </summary>
    /// <returns>保存成功返回 true。</returns>
    public bool SaveRuntimeState()
    {
        if (PlayerStats == null)
            return false;

        PlayerStats.EnsureInitializedForRuntime();
        PlayerInventory playerInventory = FindSceneObject<PlayerInventory>();

        if (playerInventory == null)
            return false;

        playerInventory.EnsureInitializedForRuntime();

        GameSaveData candidate = GameSaveService.Clone(GameSaveService.GetOrCreateData());
        playerInventory.WriteToSaveData(candidate);
        PlayerStats.WriteToSaveData(candidate);
        return GameSaveService.TrySave(candidate);
    }

    /// <summary>
    /// 场景加载完成后重新绑定新的玩家和 UI。
    /// </summary>
    /// <param name="scene">刚加载的场景。</param>
    /// <param name="mode">场景加载模式。</param>
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindSceneObjects();
    }

    /// <summary>
    /// 将属性数据绑定到当前场景的 HUD、背包和玩家行为。
    /// </summary>
    private void BindSceneObjects()
    {
        PlayerStats.EnsureInitializedForRuntime();
        ProgressionService.ResolveDependencies();

        PlayerStatusPanelUI statusPanel = FindSceneObject<PlayerStatusPanelUI>();

        if (statusPanel != null)
            statusPanel.SetStatsRuntime(PlayerStats);

        PlayerAction playerAction = FindSceneObject<PlayerAction>();

        if (playerAction != null && playerAction.GetComponent<PlayerStaminaController>() == null)
            playerAction.gameObject.AddComponent<PlayerStaminaController>();

        GameObject characterPanel = FindSceneObjectByName("CharacterPanelUI");

        if (characterPanel != null && characterPanel.GetComponent<PlayerStatsUpgradeUI>() == null)
            characterPanel.AddComponent<PlayerStatsUpgradeUI>();
    }

    /// <summary>
    /// 查找当前场景中的对象，包含暂时隐藏的 UI。
    /// </summary>
    /// <typeparam name="T">要查找的组件类型。</typeparam>
    /// <returns>找到的组件，找不到时返回 null。</returns>
    private static T FindSceneObject<T>() where T : Component
    {
        T[] objects = Resources.FindObjectsOfTypeAll<T>();

        for (int i = 0; i < objects.Length; i++)
        {
            T candidate = objects[i];

            if (candidate != null && candidate.gameObject.scene.IsValid())
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// 按名称查找当前场景中的对象，包含暂时隐藏的 UI。
    /// </summary>
    /// <param name="objectName">对象名称。</param>
    /// <returns>找到的场景对象，找不到时返回 null。</returns>
    private static GameObject FindSceneObjectByName(string objectName)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();

        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];

            if (candidate != null &&
                candidate.scene.IsValid() &&
                candidate.name == objectName)
            {
                return candidate;
            }
        }

        return null;
    }
}
