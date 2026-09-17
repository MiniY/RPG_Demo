# RPG_Demo Development Log（开发日志）

> 本文档记录 `RPG_Demo` 的主要开发提交，方便回顾每次提交做了什么、影响了哪些系统、是否已经推送到远程仓库。

## 2026-09-15

### Add inventory package slot UI（新增背包格子界面）

- Commit Hash（提交编号）：`1e8f2ff`
- Branch（分支）：`main（主分支）`
- Remote（远程仓库）：`origin/main（远程主分支）`
- Push Status（推送状态）：已推送

#### Changed Files（改动文件）

- `Assets/Prefabs/UI/PackageSlot.prefab（背包格子预制体）`
- `Assets/Prefabs/UI/PackageSlot.prefab.meta（背包格子预制体元数据）`
- `Assets/Scenes/SampleScene.unity（示例场景）`
- `Assets/ScriptObjects/RewardSOs/Gold.asset（金币奖励配置）`
- `Assets/Scripts/PlayerInventory.cs（玩家背包脚本）`
- `Assets/Scripts/PlayerInventory.cs.meta（玩家背包脚本元数据）`
- `Assets/Scripts/UI/PackageSlotUI.cs（背包格子界面脚本）`
- `Assets/Scripts/UI/PackageSlotUI.cs.meta（背包格子界面脚本元数据）`

#### Summary（内容总结）

- 新增 PlayerInventory（玩家背包）逻辑，用于保存玩家获得的奖励物品。
- 新增 PackageSlotUI（背包格子界面）逻辑，用于显示单个背包格子的图标、数量和选中状态。
- 新增 PackageSlot Prefab（背包格子预制体），作为背包界面中可复用的 UI 单元。
- 更新 Gold RewardSO（金币奖励配置），让金币奖励可以参与背包显示或拾取流程。
- 更新 SampleScene（示例场景），接入背包 UI 相关对象与引用。

#### Impact（影响范围）

- 影响背包系统、奖励拾取显示、金币奖励配置和示例场景 UI。
- 为后续 Inventory UI（背包界面）、Reward Pickup（奖励拾取）、Item Stack（物品堆叠）功能打基础。

#### Verification（验证结果）

- 本地提交成功。
- 远程推送成功。
- 本地 `main（主分支）` 与远程 `origin/main（远程主分支）` 已同步。

## 2026-09-17

### Improve package panel HUD interaction（完善背包面板和 HUD 交互）

- Commit Hash（提交编号）：本条记录随本提交一起生成，具体编号见 Git 历史记录
- Branch（分支）：`main（主分支）`
- Remote（远程仓库）：`origin/main（远程主分支）`
- Push Status（推送状态）：随本提交推送

#### Changed Files（改动文件）

- `Assets/Scripts/UI/BaseRedDot.cs（通用红点脚本）`
- `Assets/Scripts/UI/CurrencyHudUI.cs（货币 HUD 界面脚本）`
- `Assets/Scripts/UI/PackageGridUI.cs（背包网格界面脚本）`
- `Assets/Scripts/UI/PackageHudButtonUI.cs（背包 HUD 按钮脚本）`
- `Assets/Scripts/UI/PackagePanelController.cs（背包面板控制器脚本）`
- `Assets/Scripts/UI/PackageRewardTooltipUI.cs（背包奖励提示框脚本）`
- `Assets/Scripts/UI/PackageSlotUI.cs（背包格子界面脚本）`
- `Assets/Scripts/GameInput.cs（游戏输入管理脚本）`
- `Assets/GameControls.inputactions（输入动作配置）`
- `Assets/GameControls.cs（输入动作生成代码）`
- `Assets/Scripts/Player/PlayerAction.cs（玩家行为脚本）`
- `Assets/Scripts/RewardPickup.cs（奖励拾取脚本）`
- `Assets/Scripts/ScriptableObject/RewardSO.cs（奖励数据脚本）`
- `Assets/ScriptObjects/RewardSOs/Gold.asset（金币奖励配置）`
- `Assets/Prefabs/UI/PackageSlot.prefab（背包格子预制体）`
- `Assets/Scenes/SampleScene.unity（示例场景）`
- `Assets/Font/MyTMP.asset（TextMeshPro 字体资源）`
- `Assets/_Resources/_Font/character.txt（字体字符表）`

#### Summary（内容总结）

- 新增 `BaseRedDot（通用红点）`，为 HUD 图标提供统一的红点显示和隐藏接口。
- 新增 `CurrencyHudUI（货币 HUD 界面）`，实时显示玩家当前金币数量。
- 新增 `PackageHudButtonUI（背包 HUD 按钮）`，支持点击打开或关闭背包，并根据金币数量显示红点提醒。
- 新增 `PackagePanelController（背包面板控制器）`，支持通过 Tab（背包快捷键）打开或关闭背包，并在背包打开时锁定 `Gameplay Input（玩法输入）`。
- 新增 `PackageGridUI（背包网格界面）`，根据 `PlayerInventory（玩家背包）` 动态生成背包格子，并处理格子选中和提示框显示。
- 新增 `PackageRewardTooltipUI（背包奖励提示框）`，显示奖励名称和说明，并将提示框限制在背包背景范围内。
- 扩展 `PackageSlotUI（背包格子界面）`，支持点击选中、鼠标悬浮延迟显示提示框和鼠标移出隐藏提示框。
- 扩展 `GameInput（游戏输入管理）`，加入背包输入事件、攻击输入事件和玩法输入启用状态。
- 调整 `RewardPickup（奖励拾取）` 和 `RewardSO（奖励数据）`，让奖励拾取数量进入背包系统，并支持奖励说明显示。
- 更新场景、UI 预制体和 TextMeshPro 字体资源，接入背包面板、金币显示、红点和提示框。

#### Impact（影响范围）

- 背包系统从“显示格子”扩展为“打开、关闭、选中、查看详情和红点提醒”的 UI 闭环。
- 打开背包时会锁定移动、攻击和控制切换等玩法输入，避免操作 UI 时角色继续行动。
- 金币奖励拾取后会同步更新玩家背包、货币 HUD 和背包红点。
- 为后续物品提示框、商店界面和任务奖励系统打基础。

#### Verification（验证结果）

- 已检查主要脚本逻辑和工作区改动范围。
- 本地编译检查通过。
- 本条记录随本次提交一起推送到远程仓库。