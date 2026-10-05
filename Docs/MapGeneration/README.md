# MapGeneration（地图生成）文档索引

本目录集中保存 `MapGeneration（地图生成）` 分支的主要技术文档。地图系统与 `main（主分支）` 保持独立开发和独立发布；除非经过明确的合并操作，两个分支不会互相移动或自动包含对方的提交。

## 当前发布状态

- 分支：`MapGeneration（地图生成分支）`
- 基线提交：`6abd144`，即桥梁瓦片资源提交。
- 地图功能提交：13 个。
- 当前 `Generator Version（生成器版本）`：`6`。
- 地图运行时程序集：`RPG_Demo.MapGeneration（RPG Demo 地图生成程序集）`。
- 测试程序集：`RPG_Demo.MapGeneration.Tests（RPG Demo 地图生成测试程序集）`。
- 测试场景：`Assets/Scenes/MapGeneration/MapGenerationTest.unity（地图生成测试场景）`。
- 原有 `SampleScene（示例场景）` 不属于本分支地图测试场景的修改目标。

## 文档列表

1. `随机地图系统使用说明.md`：面向 Unity Editor（Unity 编辑器）操作、配置参数、碰撞检查和自动化测试。
2. `架构说明.md`：说明数据层、生成流水线、分层渲染、简单装饰、小地图与程序集边界。
3. `开发状态与后续计划.md`：记录已完成功能、当前限制、发布决策和后续实现顺序。

## 分支隔离原则

- `origin/main（远程主分支）` 保存主游戏系统，包括背包、商店、存档、玩家成长与怪物人工智能。
- `origin/MapGeneration（远程地图生成分支）` 保存随机地图、小地图、战争迷雾、分层地形、简单装饰及其测试和文档。
- 向 `origin/MapGeneration（远程地图生成分支）` 推送不会修改 `origin/main（远程主分支）`。
- 向 `origin/main（远程主分支）` 推送也不会自动修改 `origin/MapGeneration（远程地图生成分支）`。
- 只有显式执行 Merge（合并）、Rebase（变基）或 Cherry-pick（拣选提交）时，两个分支的内容才会发生整合。

## 阅读顺序

第一次接触本模块时，建议先阅读 `架构说明.md`，再按照 `随机地图系统使用说明.md` 打开测试场景并运行 Edit Mode Test（编辑模式测试），最后阅读 `开发状态与后续计划.md` 确认当前边界和下一阶段目标。
