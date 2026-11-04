
<!-- SHOP-V35-CURRENT-20261105 -->
> **2026-11-05 当前商店成果**：正式 `SampleScene` 接入已完成的商店 UI，并包含必要的统一 MyTMP 内容／Host 前置代码。新版字体沿原 GUID；新增 UI 位图与字体图集为本地私有资源，首次冷克隆须先按 [精确恢复说明](Tools/ShopUI/README.md) 恢复17项，再打开 Unity。当前新槽位1保留。使用、实际验证范围及最终成品人工待验收状态见 [本次集成记录](Docs/Releases/ShopUI-V35-FormalIntegration-20261105.md)。下方按日期保留的旧发布范围不覆盖这次新增成果；原始日志与本地完整验收包不随Git公开。

# RPG_Demo

Unity 像素 ARPG 项目。正式新冒险默认使用已验收的 VNext 地图与背包 UI。旧地图版本可在 GitHub 提交历史中查看。

## 拉取与环境

```powershell
git clone https://github.com/MiniY/RPG_Demo.git
cd RPG_Demo
```

- Windows 64 位，Python 3.11 或更新版本。
- Unity **2022.3.62f3c1**（项目锁定版本），已完成本机正常授权并具备 Windows 构建能力。不要为运行本项目升级 Editor 或包版本。
- 保留 `Packages/manifest.json` 与 `Packages/packages-lock.json`。首次通过下方构建入口运行时，由 Unity Package Manager 恢复依赖；不要复制别人的 Library。包下载需要可用网络。
- 原内容校验涉及 Cinemachine 2.10.7、TMP 3.0.7、uGUI 1.0.0 的 21 个包内 `.meta`；首次恢复后必须满足原 SHA，不能删除 pins 或略过缺项。

## 从 Unity Hub 打开正式主项目

1. 打开拉取后的项目根目录，使用锁定的 Unity 版本完成依赖恢复。
2. 打开 `Assets/Scenes/SampleScene.unity` 并点 Play。
3. 点击“开始新冒险”，进入新版随机地图。普通随机开局和后续层最多使用 12 个输入，自动复用原重试流程筛选合法地图，耗尽后显示原失败与重试界面。

WASD 移动。东西楼梯沿相应斜向上下，单独 A/D 应阻止完整跨层。新版包含五色高程、南侧岩壁禁入，以及已接入的怪物、掉落、出口和保存重开流程。

正式构建的首场景同样为 `SampleScene`。在 Unity Build Settings 中使用此场景即可沿用普通正式入口。

## 生成隔离数据的 Windows 验收包

在仓库根目录运行，`--unity` 换成你实际安装的相同版本 Editor 路径：

```powershell
python Tools/MapVNext/run_i1.py build --id first-formal-build --startup-target normal --timeout 1800 --unity "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe"
```

也可在当前 PowerShell 会话设置 `$env:RPG_I1_UNITY` 后省略 `--unity`。脚本只读取 exe 的版本资源确认版本，所有 Unity 执行仍经已有 Guard → Supervisor。若其他任务占用或状态未知，等待持有者正常收尾，不删除锁或结束其他进程。

成功后双击：

```text
Artifacts/MapAcceptance/MapVNext-Integration-I1-20261029/Build-map-formal-default-20261104-d03/DEFAULT/MVP_Windows_x64/RPG_Demo_MapVNext_I1.exe
```

此包从普通 `SampleScene` 启动，携带正常 DEFAULT 能力，使用独立的验收 company/product 数据空间。它运行与主项目相同的新游戏流程。

第一次构建成功后，直接运行 exe。同一构建目录只绑定一组受审源码，源码变化后需要新的身份与输出目录。

## 存档与历史

普通新冒险统一选择 VNext。恢复已有存档时读取该存档记录的版本，已有 MainV1 兼容解码继续保护原进度。新冒险默认切换不会替旧档改身份。

GitHub 历史提交 `998c337433c968d6db0abde2557c02cb2649edb7` 保存了推广前的 MainV1 默认和专用 I1 入口。历史专项验证仍使用其明确的构建能力及版本身份。

## 完成范围与历史边界

- 已采纳的地形、空间与实体规则来自 a41/r3 人工验收地图，后续完成普通正式入口接入；背包列表、选中与提示 UI 沿用已验收成果。
- 随宿主保留必要审计/制作/预览源码及 `.meta`，仅保持受审输入闭包，不自动激活怪物库。
- 本轮不采用：尚未完成的 23 个储备怪物素材/预制体、商店 V3.5、统一字体升级、来源未明的字符表改动。
- 旧内容清单保留。原始完整性实查为 **1196 项原字节一致 + 24 项受控差异**，其中包含正式主场景；整组兼容资格绑定本轮 684 项输入与构建身份。
- 旧导航首因 UNKNOWN、历史 NUnit 退出超时及其他失败回执保留。功能验收、自动化测试和自然退出各自有范围，不互相替代。

已知 TMP 构建缓存变化只复用既有精确字节对和三份回调源码 pin。随工具提供的两个原始资格 JSON 是历史证据，不是新运行 PASS；未知差异继续阻断，不能动态添加可信哈希。

[正式新版入口与验证记录](Docs/Releases/MapVNext-FormalDefault-20261104.md) · [2026-11-02 发布历史](Docs/Releases/Accepted-Map-I1-and-Inventory-20261102.md)
