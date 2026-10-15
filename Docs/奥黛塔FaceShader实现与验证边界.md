# 奥黛塔 Face Shader：原理、改动与验证边界

## 1. 范围与结论

本文记录 HoyoToon 中 Face／Brow 的原版 SDF 阴影、鼻子高光、头部参考轴和 AO 组合。瞳孔与 Body 的实现分别见现有对应文档。

依据是提取的原始材质 JSON 和编译 Shader 的反编译 HLSL，不是开发者原始源码。局部公式经过同输入数值对照，但未证明提取变体、游戏运行时参数和完整渲染链全部一致，不能称为整条 Face Shader 完全还原。

当前保留风格化 SDF／AO 阴影与鼻子高光。原版共用环境明暗状态暂缓接入；不再把 Unity 脸部逐像素投影直接当成该状态。最近的 GI／Tonemapping 实验没有修改正式公式或关闭正式材质开关。

## 2. 文件与改动职责

Shader 文件位于 `Packages/com.meliverse.hoyotoon/Shaders/Genshin Impact/`。

| 文件 | 本轮实际改动 |
|---|---|
| `HoyoToonGenshin.shader` | 增加 `_UseNativeFaceSDF`、独立 `_FaceSdfMap` 和原版镜像、阈值、鼻子高光参数；开关默认关闭，大部分原始参数隐藏于 Inspector。 |
| `Includes/HoyoToonGenshin-declarations.hlsl` | 声明 SDF 贴图及配套材质输入。 |
| `Includes/HoyoToonGenshin-common.hlsl` | 实现光照方向投影、R／A 距离解码、Sigmoid、镜像、G／B 高光带、原版亮环境下的 AO／SDF 阴影组合。 |
| `Includes/HoyoToonGenshin-inputs.hlsl` | 在 Face 编译分支增加 `native_face_ndotl` 插值量。 |
| `Includes/HoyoToonGenshin-program.hlsl` | VS 计算该插值量，PS 原版 Face 分支使用它；鼻子高光在基础阴影合成后单独相加。 |

`Assets/Script/Game/UI/Components/CharacterDetail/ModelPreview/CharacterPupilShaderInputs.cs` 复用已有头部输入，在 `LateUpdate` 将头部轴同步给 `CharacterPreviewActor.FaceRenderers`；未增加新序列化字段或 public API。

没有改模型网格、骨骼层级或 BlendShape，也没有通过移动脸部顶点改变脸型。阴影位置、鼻子亮斑和色调变化会改变视觉上的立体感，但不能因此推断网格变形。

## 3. 贴图与开关

`_FaceSdfMap` 当前绑定 `Avatar_Girl_Tex_FaceSDF.png`，承担距离与高光带数据；不能用普通 Face Diffuse 替代。

`_LightMapTex` 当前绑定 `Avatar_Tex_Face02_Shadow.png`，提供分区遮罩、AO 和材质编号；它不是 `_FaceMapTex` 的绑定图。

已有 `_FaceMapTex` 当前绑定共用的 `Avatar_Girl_Tex_FaceLightmap.png`（资源位于 Barbara 目录），供旧 Face 算法、描边宽度和调试显示使用；新原版 Face 路线的 SDF 阴影形状与鼻子高光读取 `_FaceSdfMap`，没有覆盖这个入口。

上述三张数据图的 `sRGBTexture` 均为 0，避免采样 RGB 时被当作颜色转换。Face Diffuse 仍作为颜色图使用。此约定不表示所有名字含 Face 的图都必须关闭 sRGB。

启用新路线需要 Face 材质分支、`_UseFaceMapNew=1` 和 `_UseNativeFaceSDF=1`。关闭原版开关保留原有 Face 算法，不代表恢复所有材质、光照和场景配置。

本地 Face／Brow 的共同配置为：`_FaceFlipFactor=0`、`_NewFaceShadowOffset=0`、`_FaceSdfShadowSharpness=100`、`_NewShadowRampMin=0`、`_NewShadowRampMax=1`、`_SdfThresholdClamp=(0.03,0.97)`。贴图 AO 和顶点 AO 开关均为 1。

Face 鼻子高光配置：`_EnableSdfSpecA=1`、Sharpness=10、Strength=0.7、BacklightIntensity=1、Vertical=0、FlipUv=1、InvertStrength=0、UvSplitY=0.4、UvBaseOffset=(0,0.5)、UvOffsetRegions=(0,0,0.0125,-0.015)、RegionParams=(1,0.1,1,1)。Brow 的 `_EnableSdfSpecA=0`，不能把 Face 高光设置直接套给眉毛。

上述数值是已核对的本地材质配置，不是所有角色通用的 Shader 默认值，也不是对游戏全部运行时覆盖值的确认。

## 4. 核心计算与产生的效果

### 4.1 头部参考坐标与光照投影

光源方向必须投影到头部的上、前、侧三个方向，而不是直接取世界 X／Z。SkinnedMeshRenderer 所在对象的轴不会自然跟随 Head 骨骼转动。

组件在动画求值后的 `LateUpdate` 上传 `_CharacterHeadCenterXDirWS` 和 `_CharacterFaceWorldDirection`，逐个读取再写回 Renderer 的 MaterialPropertyBlock，保留其他已有覆盖值。Face 复用这两个已有输入，不需要新建独立控制器。

奥黛塔导出头骨的局部 X 向下、Y 朝前。Face 因此使用 `up=-headX`、`forward=faceDirection`、`right=cross(forward,up)`；输入无有效轴时使用已有材质头部轴。

```hlsl
localLight = float3(dot(light, up), dot(light, forward), dot(light, right));
direction = float4(localLight.y, -localLight.z, -localLight.x, -localLight.y)
    / max(float4(length(localLight.yz).xx, length(localLight.xy).xx), 0.0001);
```

前两个分量表达水平投影，后两个表达竖直投影，服务于 SDF 阴影及可选竖直高光。轴换算是导入模型适配；同世界基准下计算一致，不证明游戏实际上传矩阵已经得到。

### 4.2 R／A 解码及风格化阴影

SDF 的 R 和 A 表达距离区间的两半，不能任选一个通道作为整张阴影遮罩。

```hlsl
sdf = packed.a > 0.0001 ? 0.5 * (1 + packed.a) : 0.5 * packed.r;
threshold = clamp(0.5 - 0.5 * direction.x + offset, clampMin, clampMax);
lit = sigmoid((sdf - threshold) * sharpness);
```

`sigmoid` 使用源分支的指数常数 `49.8289223`；实现分正负计算，避免直接计算很大的指数。光源侧向符号决定是否镜像 UV，`_FaceFlipFactor` 可强制翻转或禁止翻转。

效果是按美术距离图推进的脸部明暗边界，不是按每个脸部多边形的法线直接画阴影。它控制阴影形状，不负责相机曝光或最终色调映射。

### 4.3 G／B 高光带与鼻子亮斑

G／B 不是 RGB 底色，它们参与角度带的上下阈值。计算先处理镜像、UV 上下分区、基础偏移与区域偏移，再采样 G／B；两个 Sigmoid 的乘积产生高光带。

高光带乘以强度、区域参数以及正面／背光权重，返回标量亮度。生产流程在基础色乘阴影之后加上该亮度，再继续现有 HoyoToon 光照与输出处理。

因此鼻子亮斑不是普通 Blinn-Phong 光泽点，也不是从 Face Diffuse 中直接抠出的白块。BacklightIntensity=1 时，背光分支仍能贡献高光；“脸暗而鼻子亮”不能单凭现象判定为错误。当前最后显示有多亮，还取决于后续颜色合成。

### 4.4 AO、顶点插值与 SDF 的组合

贴图 AO 先按源分支重映射，再按开关乘顶点颜色 R。中间 AO 区间结合 `N·L` 与 `_LightArea` 判断亮暗；两端有明确的全暗／全亮处理。

原版使用顶点阶段计算并插值的 `N·L`。当前新增 Face 专用插值量，避免在片元中重新归一化法线后替代它；旧路线仍使用旧输入。

区域遮罩 R 大于 0.5 时，明暗权重取 AO 与 SDF 的共同限制；否则采用 AO 判定。暗色再结合现有 ShadowTransition 的范围、柔化和暖／冷阴影颜色生成。

```hlsl
shadowWeight = 1 - (regionMask > 0.5 ? min(aoLit, sdfLit) : aoLit);
tint = useTransition ? lerp(1, shadowColor, transition) : shadowColor;
sdfTint = lerp(1, tint, 1 - sdfLit);
color = lerp(1, sdfTint, shadowWeight);
```

这是当前保留的亮环境合成路线。AO 没有被替换为 Unity 投影；两者是不同输入。暖／冷颜色及自动昼夜选择仍依赖既有 HoyoToon 处理，未证明其运行时来源等同原版。

## 5. 为什么暂缓环境状态

提取的原版 VS 在同一次绘制共用的 UV 上用 `SampleLevel(...,0)` 读取一个值、以 0.5 判断状态，并受多个统一开关覆盖。PS 接收该状态后，可以强制阴影权重为 1，并切换阴影颜色组合。

已确认的是它如何被消费；上游贴图如何生成、字段怎样绑定以及游戏实际值仍未确认。资源中存在 AmbientSensor 相关名称，但名称本身不能证明它就是屏幕 AO、Shadowmap 或碰撞射线结果。

此前曾将 Unity 每像素的投影衰减接入该状态：二值判断会把轻微遮挡突然放大为整块暗色；连续插值虽然减轻跳变，却会把局部投影形状混入原版风格化分区，并非原版路线。

最终移除这项 Face 适配，保留 SDF／AO 的亮环境路线。不新增检测器、射线、手动环境控件或原版状态入口。原有 `lightmapao>0.8` 的旧投影规则没有全局删除；当前原版 Face 颜色分支不消费被它改写的 `casted`。

这不是全部 Face 回退。SDF 解码、鼻子高光、头部轴和顶点插值保留；Body／丝袜／金属此前的环境替代没有在本轮一起修改。

## 6. 验证依据与能证明的范围

数值参照来自反编译公式，在双方输入明确一致的条件下比较；不是独立游戏截图，也不是完整引擎帧的逐像素还原。

| 检查 | 保留结果 | 验证边界 |
|---|---|---|
| SDF／高光局部计算 | 112 组，最大误差约 5.96e-8，无无效值，Shader 错误 0 | 所测公式和参数组合。 |
| 实际贴图高光采样 | 3 组，最大误差约 7.45e-9 | 所测贴图与角度；不含最终光照。 |
| 头部投影计算 | 252 组，最大误差约 2.98e-7 | 使用匹配世界坐标基准的方向计算，不证明游戏头部输入生产一致。 |
| 光照前阴影颜色 | 240 组，最大误差 0 | 显式环境状态的受控参照；当前生产路线仅保留亮环境，不含完整 GBuffer。 |
| 头轴／顶点插值回归 | 36 张离屏对照，轴和标记失败 0，所测旧模式差异像素 0 | 烘焙网格、固定照明及部分转角；不是实际动画全量回归。 |
| 撤除环境替代回归 | 24 张对照与 5 个光照角度，测试 Shader 错误 0 | 检查当前路线不被投影替代驱动，以及所测旧模式保持一致。 |
| GI／Tonemapping 隔离 | 5 张场景图和 20 张受控侧光图 | 测试临时 Shader／材质，未改正式文件，未切屏或进入 PlayMode。 |

最后一项中，临时基线与正式场景图差异像素为 0。当前 `_GI_Intensity=0`，单独移除 GI 加色后所有对照差异像素为 0，所以不能将当前肤色偏亮归因于这项加色；这不排除 `CalcLighting` 的主光／环境乘色影响。

关闭内置 Tonemapping 会改变暗侧色调与对比，SDF 分区位置不变，亮侧仍接近白色。这个开关位于材质 Shader 的片元输出流程，不是相机统一后处理；本轮实验没有改相机后处理，也未确认存在重复 Tonemapping。

对照 PNG 部分通道达到 255，不能仅凭最终图片确定更早哪一步超范围，更不能据此将关闭 Tonemapping 定为正确修复。

本地报告均位于 `.codex_tmp_frame_analysis/odette_face_source_20261005/`：`face_finish_numerical_results.json`、`face_numerical_high_results.json`、`face_frame_audit_results.json`、`face_color_audit_results.json`、`face_finish_visual_results.json`、`face_uniform_regression_results.json`、`face_uniform_angle_results.json`、`face_color_isolation_results.json`、`face_color_isolation_pixel_results.json`。

同目录 `programs/Face_SDF_vs_offset_0057788E.hlsl` 与 `programs/Face_SDF_ps_offset_0010FF12.hlsl` 为主要反编译参照。不同阶段报告反映不同历史版本，不能把显式环境测试或已经撤除的实验路径当成当前生产功能。

## 7. 剩余差异与后续验证顺序

1. **最终颜色链优先。** 当前仍走 HoyoToon 的 `CalcLighting`、边缘光和内置 Tonemapping，不是原版完整 GBuffer／光照／后处理。应以浮点目标分别读取光照前、光照后、边缘光后和 Tonemapping 后的值，检查肤色与鼻子高光在哪一步改变，不先改 SDF 公式。
2. **环境状态有明确保留缺口。** 当前不实现角色整体进入暗处时的原版状态切换。恢复此功能应先确认上游生产与参数，不自动回到逐像素投影替代。
3. **动态边界未全测。** 需要补实际动画中的头部俯仰、左右镜像临界、光源正上／下方、表情变形以及极端观察角度，检查跳变和闪烁；已测静态转角不能代替这些检查。
4. **采样状态未完整对齐。** sRGB 已处理，但压缩、过滤、Mip 和原版采样器状态仍需核对。不要为消除截图差异直接把生产采样全部锁 mip 0。
5. **没有同输入实机逐像素参照。** 仍缺相同姿态、模型、相机、光照、环境与曝光的游戏中间输出。当前结果不能证明最终肤色、边缘光、鼻子亮度与游戏完全一致。
6. **覆盖与性能边界。** 其他角色、全部关键词、跨平台和长期 PlayMode 未全量回归；未做 GPU 性能测量，不能声称高精度运算没有成本，也没有证据证明它造成性能瓶颈。部分统一光照投影仍在 PS 重算属于可优化项，不等于已经确认的视觉错误。

## 8. 本地配置复查与保存边界

Unity 复查时固定姿态、相机、主光及曝光；确认使用 `HoyoToon/Genshin/Character` 的 Face 材质分支并绑定独立 SDF 图，分别对照原版开关和鼻子高光开关，不同时改贴图、光源和公式。

头部输入组件需要绑定 Head 和 Pupil Renderer，Actor 的 FaceRenderers 需要包含相关面部 Renderer。检查 Head 动画后轴更新，不把 Renderer 对象自身的轴当作动画头骨轴。

模型、材质、贴图和导入设置位于被现有 Git 规则忽略的 `Assets/Art/Model/`；反编译资源、测试代码、报告与截图也在忽略的临时目录。本文保存方法、参数和结果，不能替代这些本地输入，单独检出代码不会自动恢复全部视觉配置。

本次归档不改场景、包依赖、卡池功能或 Addressable key，也不提交已有瞳孔文档的独立改动。没有新的 Unity 重导入、PlayMode 或视觉验证；这里只核对已有代码、材质文件、测试报告及 Git 差异。
