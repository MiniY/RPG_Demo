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

## 2026-10-04

### Implement monster AI and damage reaction system（实现怪物 AI 与伤害受击系统）

- Commit Hash（提交编号）：本条记录随本提交一起生成，具体编号见 Git 历史记录
- Branch（分支）：`main（主分支）`
- Remote（远程仓库）：`origin/main（远程主分支）`
- Push Status（推送状态）：随本提交推送

#### Changed Files（改动文件）

- `Assets/Scripts/Damageable/DamageInfo.cs（伤害信息数据）` 及其 `.meta` 元数据
- `Assets/Scripts/Damageable/BaseDamageable.cs（可受伤对象基类）`
- `Assets/Scripts/Damageable/Animal/AnimalHurtController.cs（动物受击控制器）`
- `Assets/Scripts/Damageable/Plant/PlantBehaviorController.cs（植物行为控制器）`
- `Assets/Scripts/Damageable/Monster/BaseMonster.cs（怪物基类）`
- `Assets/Scripts/Damageable/Monster/BaseMonsterState.cs（怪物状态基类）` 及其 `.meta` 元数据
- `Assets/Scripts/Damageable/Monster/MonsterStateType.cs（怪物状态类型）` 及其 `.meta` 元数据
- `Assets/Scripts/Damageable/Monster/MonsterAIController.cs（怪物 AI 控制器）` 及其 `.meta` 元数据
- `Assets/Scripts/Damageable/Monster/MonsterPerceptionController.cs（怪物感知控制器）` 及其 `.meta` 元数据
- `Assets/Scripts/Damageable/Monster/MonsterMovementController.cs（怪物移动控制器）` 及其 `.meta` 元数据
- `Assets/Scripts/Damageable/Monster/MonsterAttackController.cs（怪物攻击控制器）` 及其 `.meta` 元数据
- `Assets/Scripts/Damageable/Monster/MonsterAnimationController.cs（怪物动画控制器）`
- `Assets/Scripts/Damageable/Monster/MonsterHurtController.cs（怪物受击控制器）`
- `Assets/Scripts/Damageable/Monster/MonsterPatrolState.cs（怪物巡逻状态）`、`MonsterChaseState.cs（怪物追击状态）`、`MonsterSearchState.cs（怪物搜索状态）`、`MonsterReturnState.cs（怪物返回状态）`、`MonsterAttackState.cs（怪物攻击状态）` 及其 `.meta` 元数据
- `Assets/Scripts/Player/PlayerDamageController.cs（玩家造成伤害控制器）` 及其 `.meta` 元数据
- `Assets/Scripts/Player/PlayerHurtController.cs（玩家受击控制器）` 及其 `.meta` 元数据
- `Assets/Scripts/Player/PlayerAction.cs（玩家行为脚本）`
- `Assets/Prefabs/Monsters/Torch_Blue.prefab（蓝色火炬敌人预制体）` 及其 `.meta` 元数据
- `Assets/Prefabs/Monsters/HappySheep.prefab（快乐绵羊预制体）`
- `Assets/Prefabs/Players/Warrior_Blue.prefab（蓝色战士玩家预制体）`
- `Assets/Scenes/SampleScene.unity（示例场景）`
- `Assets/Animations/Monsters（怪物动画资源目录）` 及其 `.meta` 元数据
- `Assets/_Resources/HomeMadeResources（自制资源目录）` 及其 `.meta` 元数据

#### Summary（内容总结）

- 新增 `DamageInfo（伤害信息）`，统一描述伤害数值、伤害来源及后续受击处理所需的数据，减少玩家、怪物和可破坏对象之间重复定义伤害参数的情况。
- 扩展 `BaseDamageable（可受伤对象基类）`，并更新动物、植物、怪物和玩家相关控制器，使不同类型对象可以接入统一的 Damageable（可受伤对象）处理流程。
- 新增 `MonsterAIController（怪物 AI 控制器）` 和 `MonsterStateType（怪物状态类型）`，以状态机（State Machine，状态机）组织 Patrol（巡逻）、Chase（追击）、Search（搜索）、Return（返回）和 Attack（攻击）等行为状态。
- 将怪物行为职责拆分为 `MonsterPerceptionController（感知控制器）`、`MonsterMovementController（移动控制器）`、`MonsterAttackController（攻击控制器）` 和 `MonsterAnimationController（动画控制器）`，让行为决策、移动执行、攻击执行和动画表现彼此独立，降低耦合度。
- 新增 `PlayerDamageController（玩家造成伤害控制器）` 和 `PlayerHurtController（玩家受击控制器）`，更新 `PlayerAction（玩家行为）`，接入玩家攻击、伤害判定和受击反馈流程。
- 更新 `HappySheep（快乐绵羊）`、`Warrior_Blue（蓝色战士）` 和 `SampleScene（示例场景）` 的组件、引用和行为配置，并新增 `Torch_Blue（蓝色火炬敌人）` 预制体及怪物动画资源。

#### Impact（影响范围）

- 影响玩家战斗、怪物 AI、动物受击、植物行为、伤害传递、受击反馈、敌人预制体和示例场景配置。
- 怪物的 Animation Control（动画控制逻辑）与 Behavior Control（行为控制逻辑）通过控制器职责分离，后续可以独立调整动画状态和 AI 决策，减少互相修改造成的回归风险。
- 状态机为后续扩展警戒、受击硬直、死亡、技能和更复杂的敌人行为提供了统一扩展位置。

#### Verification（验证结果）

- 已读取并核对工作区状态，确认本次改动包含 C# 脚本、动画资源、预制体和场景配置。
- `learn/tilemap-minimap/course-state.md（学习进度记录）` 及 `learn/（学习记录目录）` 保持在工作区中，不纳入本次提交。
- 本次尚未在 Unity 编辑器中运行场景或执行 Play Mode（播放模式）测试；提交前仅进行 Git 文件范围核对。
- 本条记录随本次提交一起推送到远程仓库。

## 2026-09-26

### Add merchant shop and persistent save system（新增商人商店与持久化存档系统）

- Commit Hash（提交编号）：本条记录随本提交一起生成，具体编号见 Git 历史记录
- Branch（分支）：`main（主分支）`
- Remote（远程仓库）：`origin/main（远程主分支）`
- Push Status（推送状态）：随本提交推送

#### Changed Files（改动文件）

- `Assets/Scripts/NPC/MerchantInteractionController.cs（商人交互控制器）`
- `Assets/Scripts/Shop/ShopCatalogSO.cs（商店目录配置）`
- `Assets/Scripts/Shop/ShopTradeController.cs（商店交易控制器）`
- `Assets/Scripts/UI/Shop/ShopGridUI.cs（商店商品网格界面）`
- `Assets/Scripts/UI/Shop/ShopModeTabButtonUI.cs（商店分类标签按钮界面）`
- `Assets/Scripts/UI/Shop/ShopPanelController.cs（商店面板控制器）`
- `Assets/Scripts/UI/Shop/ShopPurchaseFlyEffect.cs（购买物品飞入效果）`
- `Assets/Scripts/UI/Shop/ShopSlotUI.cs（商店商品格子界面）`
- `Assets/Scripts/GameSaveService.cs（游戏存档服务）`
- `Assets/Scripts/ScriptableObject/RewardRegistrySO.cs（奖励注册表配置）`
- `Assets/Scripts/PlayerInventory.cs（玩家背包）`
- `Assets/Scripts/ScriptableObject/RewardSO.cs（奖励数据）`
- `Assets/Scripts/RewardPickup.cs（奖励拾取）`
- `Assets/Scripts/GameInput.cs（游戏输入管理）`
- `Assets/Scripts/Player/PlayerAction.cs（玩家行为）`
- `Assets/ScriptObjects/RewardRegistry.asset（奖励注册表数据）`
- `Assets/ScriptObjects/Shop/ShopCatalog_Merchant.asset（商人商品目录）`
- `Assets/Prefabs/NPC/Merchant_Pawn.prefab（商人角色预制体）`
- `Assets/Prefabs/UI/ShopSlot.prefab（商店商品格子预制体）`
- `Assets/Animations/MerchantSign（商人交互标识动画资源）`
- `Assets/Animations/MerchantVisual（商人角色动画资源）`
- `Assets/GameControls.inputactions（输入动作配置）` 与 `Assets/GameControls.cs（输入动作生成代码）`
- `Assets/Scenes/SampleScene.unity（示例场景）`、`Assets/Prefabs/Players/Warrior_Blue.prefab（玩家角色预制体）`
- 奖励数据、TextMeshPro 字体字符表及 UI 图标资源

#### Summary（内容总结）

- 新增商人交互逻辑：玩家靠近商人后可按 F（交互键）打开商店；交互行为脚本与商人待机、头顶交互标识动画资源分开管理，减少行为逻辑与动画表现之间的耦合。
- 新增商店目录配置与购买流程，支持食品、武器、技能、材料和其他分类，以及价格、库存、单次购买数量和稳定商品编号等配置。
- 商店交易会校验商品、金币、库存和背包状态；失败时恢复交易前状态，并以存档写入成功作为确认交易的条件。
- 新增商店分类标签、商品网格、库存与售罄状态、商品提示、错误提示和购买飞入背包效果。打开商店时锁定玩法输入，按 Escape（退出键）关闭商店。
- 新增版本化 JSON（JavaScript 对象表示法）存档，将背包和商店库存写入本地存档，并使用正式文件、备份文件和临时文件管理写入与恢复。
- 新增 RewardRegistrySO（奖励注册表配置），用稳定的 RewardId（奖励唯一编号）将存档中的奖励记录映射回 Unity 奖励资源。
- 扩展背包的存档加载、数量增减和交易快照回滚；拾取物品只有在背包确认成功保存后才会从场景中回收。
- 更新输入动作：将原 Controls（控制动作）和 Attack（攻击动作）调整为 Interact（交互动作）和 Battle（战斗动作），并加入 F（交互键）交互绑定。
- 当前商人目录仅配置一个食品商品；武器和技能分类尚无已配置商品。本次实现的是购买流程，尚未实现出售流程。

#### Impact（影响范围）

- 影响商人交互、商店购买、背包与奖励、输入管理、游戏存档、示例场景及相关 UI 和美术资源。
- 背包拾取与商店交易都接入统一存档服务，减少奖励、金币和库存状态在异常或重新启动后不一致的风险。
- 商店界面打开时暂停玩家玩法输入，避免 UI 操作期间角色继续移动或攻击。

#### Verification（验证结果）

- 已检查本次新增商店、商人、存档与奖励注册相关文件，并核对工作区新增、修改和删除内容。
- `dotnet build Assembly-CSharp.csproj --no-restore` 和 `dotnet build Assembly-CSharp-Editor.csproj --no-restore` 分别构建通过：两个程序集成功生成，0 个警告，0 个错误。
- `dotnet build RPG_Demo.sln --no-restore` 在解决方案包装层返回失败，但没有输出编译错误；两个实际 Unity 项目分别构建成功。
- 尚未在 Unity 编辑器中运行场景或执行 Play Mode（播放模式）测试。
- 本条记录随本次提交一起推送到远程仓库。

## 2026-09-26

### Add player progression and stamina system（新增玩家成长与体力系统）

- Commit Hash（提交编号）：本条记录随本提交一起生成，具体编号见 Git 历史记录
- Branch（分支）：`main（主分支）`
- Remote（远程仓库）：`origin/main（远程主分支）`
- Push Status（推送状态）：随本提交推送

#### Changed Files（改动文件）

- `Assets/Scripts/GameSession.cs（游戏会话）`
- `Assets/Scripts/PlayerStatsRuntime.cs（玩家属性运行时数据）`
- `Assets/Scripts/PlayerProgressionService.cs（玩家成长服务）`
- `Assets/Scripts/PlayerStaminaController.cs（玩家体力控制器）`
- `Assets/Scripts/UI/PlayerStatsUpgradeUI.cs（玩家属性升级界面）`
- `Assets/Scripts/UI/PlayerStatusPanelUI.cs（玩家状态面板界面）`
- `Assets/Scripts/GameSaveService.cs（游戏存档服务）`
- `Assets/Scripts/GameInput.cs（游戏输入管理）`
- `Assets/Scripts/Player/PlayerAction.cs（玩家行为）`
- `Assets/Scenes/SampleScene.unity（示例场景）`
- `Assets/Prefabs/UI/PackageSlot.prefab（背包格子预制体）`

#### Summary（内容总结）

- 新增 GameSession（游戏会话）单例，在首个场景加载前自动创建，并通过 DontDestroyOnLoad（跨场景不销毁）保存玩家属性和成长服务。
- 新增 PlayerStatsRuntime（玩家属性运行时数据），统一管理生命值、魔法值和体力值的最大值、当前值、范围限制、变更通知和属性快照。
- 新增 PlayerProgressionService（玩家成长服务），支持通过金币增加或减少生命值、魔法值和体力值上限。默认每个属性点消耗或返还 10 金币，且交易写入失败时会恢复金币和属性快照。
- 新增 PlayerStatsUpgradeUI（玩家属性升级界面），为生命值、魔法值和体力值提供加点、减点按钮，显示“当前值 / 最大值”，并在金币不足、达到上限或保存失败时显示原因。
- 重构 PlayerStatusPanelUI（玩家状态面板界面），从独立管理体力条改为只负责显示生命值、魔法值和体力值三个状态条及对应文本，属性修改交由运行时数据和成长服务处理。
- 新增 PlayerStaminaController（玩家体力控制器），支持冲刺时按时间消耗体力，停止冲刺或攻击后经过恢复延迟自动恢复体力；默认冲刺消耗速率为每秒 2 点、恢复速率为每秒 5 点、恢复延迟为 1 秒。
- 更新 PlayerAction（玩家行为），普通攻击开始时取消冲刺状态，并按配置消耗体力；体力不足时不会播放攻击流程或造成伤害，成功消耗后立即保存运行时状态。
- 更新 GameInput（游戏输入管理），新增 CancelControlState（取消加速状态）接口，在界面禁用玩法输入或开始攻击时清理左 Ctrl（加速键）的切换状态。
- 扩展 GameSaveService（游戏存档服务），将存档版本升级到 v2，新增玩家名称和生命、魔法、体力属性字段，并为旧版本存档补充默认属性、规范化数值和自动迁移保存逻辑。
- 更新 SampleScene（示例场景）中的角色面板、状态文本、状态条和按钮绑定，并调整 PackageSlot（背包格子）文本自动缩放及悬浮提示延迟配置。

#### Impact（影响范围）

- 影响玩家状态 HUD（游戏主界面）、角色属性面板、攻击和冲刺行为、金币背包、统一存档及场景初始化流程。
- 玩家属性数据与 UI（用户界面）显示、属性成长交易、攻击/冲刺消耗之间通过事件和服务接口协作，减少 UI 直接修改核心属性数据造成的耦合。
- 旧版商店和背包存档仍可迁移到 v2 格式；新增的玩家属性会使用默认值初始化，不会因旧存档缺少字段而读取失败。

#### Verification（验证结果）

- 已读取并核对本次新增脚本、既有脚本差异、场景配置和预制体配置。
- `dotnet build Assembly-CSharp.csproj --no-restore` 构建通过：0 个警告，0 个错误。
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore` 构建通过：0 个警告，0 个错误。
- `git diff --check` 仅报告 Unity 场景序列化文件中的两个空字段行尾空白，没有发现 C# 代码格式错误。
- 尚未在 Unity 编辑器中运行场景或执行 Play Mode（播放模式）测试。
- 本条记录随本次提交一起推送到远程仓库。

## 2026-09-29

### Add bridge tiles and update sprite import data（新增桥梁瓦片并更新精灵导入数据）

- Commit Hash（提交编号）：本条记录随本提交一起生成，具体编号见 Git 历史记录
- Branch（分支）：`main（主分支）`
- Remote（远程仓库）：`origin/main（远程主分支）`
- Push Status（推送状态）：随本提交推送

#### Changed Files（改动文件）

- `Assets/Sprites/GrassTiles.prefab（草地瓦片调色板预制体）`
- `Assets/Sprites/GrassTiles/Bridge_All_*.asset（桥梁瓦片资源）`
- `Assets/Sprites/GrassTiles/Bridge_All_*.asset.meta（桥梁瓦片资源元数据）`
- `Assets/_Resources/TinySwords/Terrain/Bridge/Bridge_All.png.meta（桥梁精灵导入配置）`
- `Assets/_Resources/TinySwords/Resources/Gold Mine/GoldMine_Active.png.meta（金矿激活状态精灵导入配置）`
- `Assets/_Resources/TinySwords/Resources/Gold Mine/GoldMine_Destroyed.png.meta（金矿摧毁状态精灵导入配置）`
- `Assets/_Resources/TinySwords/Resources/Gold Mine/GoldMine_Inactive.png.meta（金矿未激活状态精灵导入配置）`

#### Summary（内容总结）

- 新增 12 个 Bridge_All（桥梁合集）瓦片资源，按 4 行 3 列命名为 `Bridge_All_0_0` 到 `Bridge_All_3_2`，用于后续 Tilemap（瓦片地图）场景绘制。
- 更新 GrassTiles（草地瓦片调色板）预制体，将新桥梁瓦片纳入已有瓦片绘制资源集合。
- 更新 Bridge_All.png（桥梁合集精灵图）的 Unity 导入元数据，使桥梁精灵切片和瓦片资源引用保持一致。
- 更新 GoldMine_Active、GoldMine_Destroyed 和 GoldMine_Inactive（金矿三种状态）精灵导入元数据，为后续资源采集或地图物件表现保持导入配置一致。
- 保留 `learn/tilemap-minimap/course-state.md（学习进度记录）` 在工作区中，不纳入本次提交。

#### Impact（影响范围）

- 影响 Tilemap（瓦片地图）绘制资源、草地瓦片调色板、桥梁地图元素和金矿地图物件的 Sprite Import Settings（精灵导入设置）。
- 为后续在场景中绘制桥梁通路、制作地图连通区域或接入小地图/地形系统打基础。
- 本次主要是 Unity 资源和元数据更新，没有新增或修改 C# 运行时代码。

#### Verification（验证结果）

- 已核对工作区状态，确认本次 Unity 资源变更包含桥梁瓦片资源、草地瓦片预制体以及桥梁/金矿精灵导入元数据。
- 本次未修改 C# 脚本，因此未重新执行 C# 编译检查。
- 尚未在 Unity 编辑器中运行场景或执行 Tile Palette（瓦片调色板）绘制验证。
- 本条记录随本次提交一起推送到远程仓库。
