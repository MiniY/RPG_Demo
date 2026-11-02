# RPG_Demo

Unity 像素 ARPG 项目。本次成品范围包含已人工验收的地图 I1 与背包 UI；新版地图保留独立入口，旧 MainV1 与旧档路由继续存在。

## 拉取与环境

```powershell
git clone https://github.com/MiniY/RPG_Demo.git
cd RPG_Demo
```

- Windows 64 位，Python 3.11 或更新版本。
- Unity **2022.3.62f3c1**（项目锁定版本），已完成本机正常授权并具备 Windows 构建能力。不要为运行本项目升级 Editor 或包版本。
- 保留 `Packages/manifest.json` 与 `Packages/packages-lock.json`。首次通过下方构建入口运行时，由 Unity Package Manager 恢复依赖；不要复制别人的 Library。包下载需要可用网络。
- 原内容校验涉及 Cinemachine 2.10.7、TMP 3.0.7、uGUI 1.0.0 的 21 个包内 `.meta`；首次恢复后必须满足原 SHA，不能删除 pins 或略过缺项。

## 构建并试玩已验收的新版地图

在仓库根目录运行，`--unity` 换成你实际安装的相同版本 Editor 路径：

```powershell
python Tools/MapVNext/run_i1.py build --id first-i1-build --startup-target i1 --timeout 1800 --unity "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe"
```

也可在当前 PowerShell 会话设置 `$env:RPG_I1_UNITY` 后省略 `--unity`。脚本只读取 exe 的版本资源确认版本，所有 Unity 执行仍经已有 Guard → Supervisor。若其他任务占用或状态未知，等待持有者正常收尾，不删除锁或结束其他进程。

成功后双击：

```text
Artifacts/MapAcceptance/MapVNext-Integration-I1-20261029/Build-map-main-host-c01/I1_PRIVATE/MVP_Windows_x64/RPG_Demo_MapVNext_I1.exe
```

进入后按界面进行新开局或恢复，WASD 移动；东西楼梯按相应斜向上下，单独 A/D 不应完整跨层。可检查五色高程、南侧岩壁禁入、怪物/掉落/出口以及保存、退出、重开。

本入口使用独立 I1 存档目录（默认 `%LOCALAPPDATA%/RPG_Demo-I1/map-main-host-c01`）与隔离偏好设置，不访问原正式进度。不迁移或改写旧档。第一次构建成功后，通常直接运行 exe 即可；同一构建目录禁止覆盖，源码变化后先核对新身份和构建范围。

## 默认入口与 Editor 的区别

`SampleScene` 仍是正式默认，使用 MainV1。把 I1 源码合入仓库并不代表默认已切成新版。

当前 I1 是专用 Windows 构建入口：构建时仅该变体携带 `RPG_I1_ISOLATED_STARTUP`，不能设置成全局编译开关，也不能只把 `I1Game.unity` 换成默认场景。Editor 中直接打开 I1Game 点 Play 会按现有合同被拒绝。请使用上面的构建入口；Editor 直接试玩新版和正式默认推广是后续独立工作。

## 完成范围与历史边界

- 已采纳：a41/r3 人工验收地图及后续 b01 启动修正；已人工验收的背包列表/选中/提示 UI；c01 整组宿主兼容。
- 随宿主保留必要审计/制作/预览源码及 `.meta`，仅保持受审输入闭包，不自动激活怪物库。
- 本轮不采用：尚未完成的 23 个储备怪物素材/预制体、商店 V3.5、统一字体升级、来源未明的字符表改动。
- 旧内容清单保留。严格原始完整性为 **1197 原字节一致 + 23 受审源码差异**，整组兼容资格另验，不宣称 1220 项全原字节一致。
- 旧导航首因 UNKNOWN、历史 NUnit 退出超时及其他失败回执保留。功能验收、自动化测试和自然退出各自有范围，不互相替代。

已知 TMP 构建缓存变化只复用既有精确字节对和三份回调源码 pin。随工具提供的两个原始资格 JSON 是历史证据，不是新运行 PASS；未知差异继续阻断，不能动态添加可信哈希。

[本次发布范围、依赖和验证记录](Docs/Releases/Accepted-Map-I1-and-Inventory-20261102.md)
