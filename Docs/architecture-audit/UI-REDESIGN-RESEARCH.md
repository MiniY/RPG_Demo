> 本轮整合上下文（2026-10-25）：本文原有叙述与验收数字保留为各节标明日期的历史快照。游戏主线已在候选提交 `96371eb36a787585cf5e70141c03555419821058` 保存，其父链包含九个 TowerRun/Portal/UI 提交；默认新冒险 Profile 为 `MainV1`，十二种保留原生怪物，Boss 仍待内容。旧 `ecology-v1`、UI v6 未提交、旧工作树/HEAD 等现在式叙述均不代表本轮状态。新整合 Build/Player 与人工验收只认本轮独立收据；Stage1、B1、Boss 和旧 R4 人工结论不升级。当前整合总账见 `../Integration/MainConsolidation20261025/README.md`。

# 查看器 UI/UX 升级调研与问题审计（2026-10-14）

## 范围与证据

目标是离线打开的 RPG_Demo 架构审计工作台。目标读者会一点 Unity，第一次读这个图谱。2026-10-14 用 `agent-browser` 在独立会话实际打开下列官方页面，并读取页面结构、示例导航和截图；这些网页展示的交互原则是参考，不构成本项目实现或性能的证明。没有复制外部产品的素材、代码或图标。

| 案例 | 实际观察 | 适合本项目 | 不照搬的部分 | 本次原创转化 |
|---|---|---|---|---|
| [Obsidian Graph View](https://obsidian.md/help/plugins/graph) | 官方帮助明确提供 Arrows、Groups、Filters 与 Local Graph depth；页面内有交互图。研究截图：[页面](research-obsidian.png)。 | 从全局转入局部，按目标缩小节点数量；方向必须有开关或说明。 | 笔记间链接方向不能直接套给 Unity 的调用、继承和 Inspector 引用。 | 系统总览只作分组；对象附近固定“谁指向它／它指向谁”两侧，并给每条线独立证据。 |
| [React Flow Edge Markers](https://reactflow.dev/examples/edges/markers) | 官方示例导航含 Custom Nodes、Edge Label Renderer、Edge Markers、Minimap、Horizontal Flow；实际打开 Edge Markers 示例，图中箭头由 SVG marker 渲染。研究截图：[页面](research-reactflow.png)。 | 节点卡片、边的悬停标签、聚焦反馈和明确箭头。 | 不为当前静态离线页引入 React、构建链和新的图谱数据模型。 | 沿用现有 SVG，在确认语义的关系上加 marker；悬停/选中时才显露线标签。 |
| [AntV G6 Dagre 流程图](https://g6.antv.antgroup.com/zh/examples/layout/dagre/antv-dagre/) | 用户给的旧 `/examples` 返回 404；从官方首页进入现行目录，见 Dagre、Force 防重叠、平行边、边过滤、Minimap 等实例。实际打开 Dagre，等到运行图出现：节点分层排列，线按结构连接，示例允许切换 `default` / `LR` / `LR&UL`；[截图](research-g6.png)。 | 按任务改变布局：总览分组、局部图按关系方向分列；处理同一对节点的多条边。 | 不移植 G6 运行库或把自动布局的结果当关系语义。 | SVG 采用总览网格与局部三列布局；同节点对关系错开 7px，降低重叠。 |
| [Neo4j Bloom Scene interactions](https://neo4j.com/docs/bloom-user-guide/current/bloom-visual-tour/bloom-scene-interactions/) | 官方文档列出 Select related nodes、Reveal relationships、Expand nodes、Shortest path、Layouts 和 Filtering。页面曾等待超时，随后快照确认文档已加载；研究截图：[页面](research-neo4j.png)。 | 渐进探索和邻居聚焦；复杂数据不能一次铺满。 | 不引入数据库查询界面、自由编辑或未实现的最短路承诺。 | 默认只画少量相邻对象，右侧仍列完整关系；方向按钮可只看输入或输出。 |

## 当前页面的 UX 审计

依据 2026-10-14 的浏览器实拍 [改造前总览](viewer-before-20261014.png)、2026-10-13 旧 [Torch Prefab](viewer-before-torch-20261013.png)、旧 [背包详情](viewer-before-inventory-20261013.png)、模板代码和 `graph.json`。P0 指会误导关系理解，P1 指明显阻碍操作，P2 指重要体验改善，P3 指可延后打磨。

| 优先级 | 发现 | 修正决定 |
|---|---|---|
| P0 | 所有关系都画成无向直线，无法区分“包含／继承／调用”；右侧详情却写 `source → target`，两处表达不一致。 | 对序列化引用、层级和人工核证的调用/事件/生成/数据流绘制箭头；系统汇总与有限 C# 名称匹配不画箭头。边详情用白话解释方向。 |
| P0 | `source confirmed; runtime evidence stale or unavailable` 未在查看器文案表中翻译。 | 单独显示“旧运行证据不可沿用”，避免旧 XML 被误读为当前 PASS。 |
| P1 | 同类圆圈难以区分 Prefab、GameObject、组件；长名称缩写后很难辨认。 | 换为带类型缩写与色条的卡片；完整名称和路径在悬停 title、搜索结果、右侧详情保留。 |
| P1 | 1264×568 时页头和两侧卡片挤压图谱；局部环形布局没有上下游的空间位置。 | 缩短页头，收敛边框与留白；局部关系按左进右出分列。 |
| P1 | 同一节点对的多条引用完全叠在一起，线难点击。 | 平行边错位，独立 17px 命中宽度，选中线加粗，悬停时弱化其他线。 |
| P2 | 连线标签始终不显示，用户不易知道点哪条；大量标签常显又会拥挤。 | 只在 Hover 或选中时显示关系类型；详情显示更完整语义和证据。 |
| P2 | 业务链按钮叫“第 N 步”，而元数据顺序不是游戏执行顺序。 | 改成“连接 N”，明确这是一组核证关系，不能把排列当运行时间线。 |
| P3 | 小地图、自由聚类、连续流动动画。 | 暂缓；当前局部视图节点数有限，优先保证离线语义和可读性。 |

## Skill 分工与设计约束

- `frontend-design`：以“游戏研发取证工作台”为明确主题，先确定深色材料、字号层级和三栏比例，再做 CSS。没有套用通用霓虹仪表盘。
- `ui-practice-learning`：读取配置、Obsidian 知识库入口和相关检索结果。当前库没有“关系图／开发者工具”先例；本案例结论只记为案例经验，不提升为通用 Skill 规则。用户已明确授权本轮写 RPG_Demo 查看器，覆盖该 Skill 早期“RPG_Demo 只读”默认边界。
- `ui-visual-qa`：以实际新旧截图、同分辨率状态和浏览器交互复查视觉层级、遮挡、可读性与证据文案。旧 Torch/背包截图来自上一轮，比较时标出日期；不把旧图当当前运行证明。
- `agent-browser`：在线四案例观察和本地页面逐项点击、截图、尺寸检查。
- `diagram-design` 的静态单图模板与复杂度上限不适用于这个可探索的动态图谱；仅参考其“减少无信息线条”和“箭头含义必须明确”的原则，没有套用该 Skill 的单图输出流程。

设计独特性来自 RPG_Demo 的证据分层：颜色只提示对象类型或问题状态，线型表示证据层级，箭头只表示确知的来源与目标。三者互不替代。详细 token 和状态见 [UI-DESIGN-SYSTEM](UI-DESIGN-SYSTEM.md)。
