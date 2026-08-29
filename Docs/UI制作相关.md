## 二、核心设计理念（最重要） ### 1️⃣ UI 不是“摆位置”，而是“空间结构设计” UI ≠ 美术排版 UI = **容器层级 + 空间分配规则** 真正决定 UI 是否健康的是：
Content Layer（内容骨架）
而不是背景或装饰
--- ### 2️⃣ 工业级 UI 的核心原则 #### ✅ 使用比例布局（Layout 驱动） 而不是：
固定宽度 / 固定坐标
--- #### ✅ Stretch > Position 优先：
anchorMin (0,0)
anchorMax (1,1)
避免：
anchor = center + 手调 offset
--- #### ✅ Layout 控制尺寸，而不是代码或手调 使用： * HorizontalLayoutGroup * VerticalLayoutGroup * LayoutElement 而不是： * 手改 RectTransform width/height --- ### 3️⃣ Content First 原则 构建顺序必须是：
ContentLayer（主内容）
→ Panel Sections
→ Top/Bottom Bars
→ Decoration
原因： > ContentLayer 决定整个界面的空间数学关系。 --- ## 三、推荐 UI 分层结构（标准模板） 角色详情界面推荐结构：
CharacterDetailView
│
├── BackgroundLayer     （纯视觉）
├── ContentLayer        ⭐核心骨架
│     ├── CharacterPreviewArea
│     └── InfoPanelArea
│
├── TabLayer
├── TopBarLayer
└── PopupLayer
--- ## 四、ContentLayer 标准设计（核心） ### ContentLayer RectTransform：
Stretch Full Screen
组件：
HorizontalLayoutGroup
作用： 👉 横向分配 UI 空间 --- ### 子区域比例
CharacterPreviewArea   FlexibleWidth = 1.2
InfoPanelArea          FlexibleWidth = 1
得到类似原神：
角色展示 > 信息面板
比例，而非像素。 --- ## 五、InfoPanelArea 内部设计 使用：
VerticalLayoutGroup
分为 Section：
HeaderSection
LevelSection
AttributeList
DetailButtonSection
FavorSection
DescriptionSection
规则： * 每个 Section 用 LayoutElement * 使用 PreferredHeight * 不强制 Expand Height --- ## 六、角色展示区域设计原则 不要直接放模型。 结构：
CharacterPreviewArea
└── CenterAnchor (0.5,0.5)
└── CharacterModel
目的： * 分辨率变化不漂移 * 始终视觉居中 --- ## 七、为什么之前 UI 修改困难 原因不是 Unity。 而是旧 UI： ❌ 基于 3840 分辨率制作 ❌ 固定像素尺寸 ❌ 中心锚点绝对定位 ❌ 非 Layout 驱动 导致：
修改 ReferenceResolution → 全部需要手调
这是结构问题，不是操作问题。 --- ## 八、当前开发策略（非常关键） 选择： > ❌ 不立即重构旧 UI > ✅ 先建立新 UI 标准 流程：
1. 用角色详情界面建立标准
2. 验证多分辨率稳定
3. 形成UI模板
4. 再重构旧界面
   这是商业项目常用策略。 --- ## 九、UI 技术规范（已确定） * UI 基准分辨率：1920×1080 * CanvasScaler：Scale With Screen Size * Layout 驱动优先 * View 不包含业务逻辑（MVVM） * UI 不依赖固定像素尺寸 ---
5. 方案 A：Top / Middle / Bottom

结构：

Root
├── TopBar
├── Content
└── BottomBar


适合：

标准手游主界面

导航型 UI

Tab 结构

优点：

清晰

模块化

易维护

方案 B：Content First（你现在用的）

结构：

Root
├── Background
├── ContentLayer（骨架）
├── Tabs
├── Overlay


优点：

更自由

更适合复杂 UI

类似原神这种大块结构

两者谁对？

都对。

区别是：

项目规模	推荐结构
小项目	Top/Middle/Bottom
中大型	Content 骨架优先