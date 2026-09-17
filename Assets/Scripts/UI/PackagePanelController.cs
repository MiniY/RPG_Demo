using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 控制背包面板的打开和关闭，并在打开时锁定玩家玩法输入。
/// </summary>
public class PackagePanelController : MonoBehaviour
{
    /// <summary>
    /// 背包面板打开或关闭时发出的事件。
    /// </summary>
    public event Action<bool> OnPackagePanelVisibilityChanged;

    /// <summary>
    /// 需要被打开和关闭的背包面板对象。
    /// </summary>
    [SerializeField] private GameObject packagePanel; // 背包面板对象。

    /// <summary>
    /// 游戏开始时背包面板是否保持打开。
    /// </summary>
    [SerializeField] private bool openAtStart = false; // 是否在开始时打开背包。

    /// <summary>
    /// 当前已经绑定的游戏输入管理器。
    /// </summary>
    private GameInput gameInput; // 游戏输入管理器。

    /// <summary>
    /// 背包面板当前是否正在显示。
    /// </summary>
    public bool IsPackagePanelVisible => IsPackagePanelOpen();

    /// <summary>
    /// 初始化背包面板的初始显示状态。
    /// </summary>
    private void Awake()
    {
        SetPackagePanelVisible(openAtStart);
    }

    /// <summary>
    /// 启用时尝试监听 Tab 背包按键。
    /// </summary>
    private void OnEnable()
    {
        TryBindInput();
    }

    /// <summary>
    /// 输入管理器可能稍后才初始化，因此每帧尝试补绑定一次，并监听 Escape。
    /// </summary>
    private void Update()
    {
        TryBindInput();

        if (IsPackagePanelOpen() && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            ClosePackagePanel();
    }

    /// <summary>
    /// 禁用时取消监听，避免重复订阅事件。
    /// </summary>
    private void OnDisable()
    {
        UnbindInput();
    }

    /// <summary>
    /// 尝试绑定游戏输入管理器，并同步当前背包面板状态。
    /// </summary>
    private void TryBindInput()
    {
        if (gameInput != null || GameInput.Instance == null)
            return;

        gameInput = GameInput.Instance;
        gameInput.OnPackagePressed += HandlePackagePressed;
        gameInput.SetGameplayInputEnabled(!IsPackagePanelOpen());
    }

    /// <summary>
    /// 取消绑定游戏输入管理器。
    /// </summary>
    private void UnbindInput()
    {
        if (gameInput == null)
            return;

        gameInput.OnPackagePressed -= HandlePackagePressed;
        gameInput = null;
    }

    /// <summary>
    /// 响应 Tab 背包按键并切换背包面板状态。
    /// </summary>
    private void HandlePackagePressed()
    {
        TogglePackagePanel();
    }

    /// <summary>
    /// 打开背包面板。
    /// </summary>
    public void OpenPackagePanel()
    {
        SetPackagePanelVisible(true);
    }

    /// <summary>
    /// 关闭背包面板。
    /// </summary>
    public void ClosePackagePanel()
    {
        SetPackagePanelVisible(false);
    }

    /// <summary>
    /// 切换背包面板的打开和关闭状态，供按钮等其他 UI 调用。
    /// </summary>
    public void TogglePackagePanel()
    {
        SetPackagePanelVisible(!IsPackagePanelOpen());
    }

    /// <summary>
    /// 设置背包面板是否显示，并同步锁定或恢复玩法输入。
    /// </summary>
    /// <param name="isVisible">是否显示背包面板。</param>
    private void SetPackagePanelVisible(bool isVisible)
    {
        bool wasVisible = IsPackagePanelOpen(); // 修改前的显示状态。

        if (packagePanel != null)
            packagePanel.SetActive(isVisible);

        if (gameInput != null)
            gameInput.SetGameplayInputEnabled(!isVisible);

        if (wasVisible != isVisible)
            OnPackagePanelVisibilityChanged?.Invoke(isVisible);
    }

    /// <summary>
    /// 判断背包面板当前是否打开。
    /// </summary>
    /// <returns>打开时返回 true，否则返回 false。</returns>
    private bool IsPackagePanelOpen()
    {
        return packagePanel != null && packagePanel.activeSelf;
    }
}