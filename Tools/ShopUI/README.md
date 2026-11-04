# 商店 UI / 统一 MyTMP 的本地私有资源恢复

此目录只公开代码、Unity 引用身份和 SHA-256，不公开新增 UI 位图或字体图集。素材许可须由使用者自行持有。完整 Windows 验收包也只在本机交付。

首次从 Git 克隆后，**先恢复资源，再打开 Unity**。使用与 `private-assets.json` 完全相同版本的本地私有资源包，包必须位于所有 Git 工作树之外：

```powershell
python Tools/ShopUI/private_assets.py restore --kit <本地已授权资源包目录> --receipt Artifacts/ShopUI/private-restore.json
python Tools/ShopUI/private_assets.py check
```

恢复器先核对所有资源、元数据、GUID、完整校验值和 Unity 空闲状态，然后只创建缺少的精确资源；未知现存内容不会被覆盖。重复执行是无修改核对。恢复包缺失或版本不符时不能打开 Unity，以免生成新的字体资产身份；这不表示公开冷克隆可以脱离私有资源运行。

`Assets/Font/MyTMP.asset.meta` 与原 GUID 保留，业务 UI 继续引用统一 `MyTMP`。字体像素资产退出当前版本的 Git 跟踪，但本地新版资产保留；不创建第二业务字体、不修改历史提交或原始内容清单。`private-assets.json` 含17个精确资源（16张商店 PNG 和1个字体资产）。

正式入口仍为 `Assets/Scenes/SampleScene.unity`，真实靠近商人按 F 打开；WASD 移动，Esc/关闭按钮关店，Tab 背包。普通验收 Player 不带商店自动测试编译开关。独立的 `RPG_SHOP_INTEGRATION_TESTS` 变体只用于自动输入、截图与四种商人测试摆位，不能作为实体键盘人工通过。

架构扫描对这17个资源以精确元数据与清单表示为 opaque（不读取像素内容的私有资产）节点；它不替代恢复器完整 SHA 检查或 Unity 字体／渲染验证。
