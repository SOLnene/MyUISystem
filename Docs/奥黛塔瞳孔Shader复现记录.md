# 奥黛塔瞳孔 Shader：原理、改动与实现

本文解释奥黛塔 `Pupil` 的渲染组成，以及 HoyoToon 中各项修正为什么需要、具体怎样实现。不包含身体材质复现。

## 1. 先理解：眼睛不是一张贴图加一个高光

当前眼睛由三类贡献组成：

- **虹膜图案**：基础颜色及两张带 Alpha 的装饰层。它们通过不同 UV 偏移产生深度层次。
- **随观察方向变化的反光**：内部 Packed Normal／MatCap 响应，以及外部球面 MatCap 响应。它们不是同一种法线，也不共用同一个采样 UV。
- **独立亮斑和焦散贡献**：独立亮斑由另一组网格面上的 SDF 决定形状；焦散在虹膜上增加与径向位置、光照有关的颜色。

所谓三张 Pupil Diffuse，是三次贴图颜色计算，不意味着新增三张网格或三个透明绘制层。虹膜的贴图层在同一个片元中合成；独立 SDF 亮斑才使用额外 Pass。

修正的重点不是单纯增加亮度，而是让这些贡献使用正确的参考轴、贴图通道、UV、混合权重和处理顺序。相同的贴图和参数，接错层或改变合成顺序，也会得到不同结果。

依据是导出的材质 JSON 和原版编译 Shader 的反编译 HLSL，不是开发者原始源码。以下说明区分已移植计算与仍未验证的完整渲染效果。

## 2. 代码职责与具体改动

以下路径均相对项目根目录。

| 文件 | 具体实现 |
|---|---|
| `Packages/com.meliverse.hoyotoon/Shaders/Genshin Impact/HoyoToonGenshin.shader` | 在现有瞳孔高光栏内增加焦散子栏、开关和参数。默认关闭焦散。 |
| `Packages/com.meliverse.hoyotoon/Shaders/Genshin Impact/Includes/HoyoToonGenshin-declarations.hlsl` | 增加对应焦散参数声明。 |
| `Packages/com.meliverse.hoyotoon/Shaders/Genshin Impact/Includes/HoyoToonGenshin-program.hlsl` | 修正折射求交、镜像方向、分层权重、最终虹膜遮罩和外层反光坐标；加入非噪声焦散及正确叠加时机。 |
| `Packages/com.meliverse.hoyotoon/Scripts/Editor/Shader UI/HoyoToonEditor.cs` | 记录材质属性顺序，在 Layout 阶段发现属性增删／重排时重新初始化 Inspector，避免重导入后沿用旧折叠栏缓存。 |

折射／高光基础、网格分区和头部输入脚本属于前面已实现的基础；后续修正建立在它们之上。下文的“原先”指对应功能修正前的实现，不表示这些功能是在同一步一起新增。

头部输入脚本为 `Assets/Script/Game/UI/Components/CharacterDetail/ModelPreview/CharacterPupilShaderInputs.cs`，作用是把骨骼运动后的参考位置和方向传给 Shader；具体原理见 5.2。所有修正都不要求移动眼睛网格或改变模型骨骼层级。

## 3. 贴图对应与当前导入状态

以下名称省略共同前缀 `Avatar_Girl_Sword_Odette_Tex_`。

| Shader 槽位 | 资源后缀 | 当前用途 | 当前 sRGB / Mip / 压缩 |
|---|---|---|---|
| `_MainTex` | `Pupil01_Diffuse` | 基础虹膜 RGB；Alpha 也参与焦散权重。 | 开 / 关 / CompressedHQ |
| `_ParallaxLayer1Tex` | `Pupil02_Diffuse` | Layer 1 颜色／Alpha 叠加及视差。 | 开 / 关 / CompressedHQ |
| `_ParallaxLayer3Tex` | `Pupil03_Diffuse` | Layer 3 颜色／Alpha 叠加及视差。 | 开 / 关 / CompressedHQ |
| `_BaseMatcapPackedTex` | `Pupil_Normalmap` | RG 在虹膜 UV 处解码方向；B 在由观察方向计算的独立 UV 处采样内部反光。 | 关 / 开 / Uncompressed |
| `_EyeMaskWithMatcap` | `Pupil_Matcap_Mask` | Alpha 提供虹膜遮罩；RGB 在外层 MatCap UV 处提供反光颜色。 | 关 / 关 / Uncompressed |
| `_PackedHighlightRampTex` | `Pupil_Packed_Ramp` | 独立高光颜色渐变，当前采样 `V=0.875`。 | 开 / 关 / Uncompressed |
| `_SDFGradientMap` | `EyeHighlight_Diffuse` | R 通道提供独立白色高光的 SDF 形状。 | 关 / 开 / Compressed |

以上是当前 Unity 导入器读回值，不等于原游戏使用相同的压缩、Mip 或过滤配置。7 张图目前均为 `Default` 类型、Bilinear 过滤。Packed Normal 保留为线性数据图，而不是 Unity `NormalMap`：Shader 要自行读出 RG 和 B 的不同信息，不能让常规法线导入／解码流程改变通道含义。颜色贴图的 RGB 使用 sRGB；遮罩／方向数据使用线性读取。

原材质记录：`Assets/Art/Model/Character/odette-snow-swan-s-dance/Materials/Avatar_Girl_Sword_Odette_Mat_Pupil.json`；当前材质为同目录、同名 `.mat`。

## 4. 参数怎样作用于计算

当前材质不是简单使用 Shader 默认值；以下值已与导出 JSON 核对。

参数分成三组：视差／深度参数决定“采样位置”，Tint／Intensity 决定“颜色贡献”，遮罩／曲率／光照响应参数决定“哪些位置参与，以及怎样随视角变化”。只调整强度不能修正坐标轴或 UV 错误。

| 参数 | 当前值 |
|---|---|
| `_ParallaxScale` / `_DepthPower` | `0.15` / `0.35` |
| Layer 1 强度／视差／Tint | `1` / `0.01` / `(1,1,1,1)` |
| Layer 3 强度／视差／Tint | `0.85` / `-0.025` / `(0.9632353,0.9878296,1,1)` |
| `_BaseMatcapIntensity` / `_BaseMatcapSize` | `1.2` / `1` |
| `_BaseMatcapTint` | `(0.3868404,0.6918327,0.992647,1)` |
| `_EyeMatcapIntensity` / `_EyeMatcapSphericalNormalLerp` | `0.87` / `0.63` |
| `_EyeMatcapTint` / `_EyeMatcapInvertUV` | `(0.7305633,0.9690822,2.113971,1)` / `(1,0,0,0)` |
| `_HighlightIntensity` / `_HighlightRampWidth` | `1.9` / `1.2` |
| `_EyeHighlightColor`（原 JSON 名为 `_HighlightColor`） | `(0.86669254,0.7942258,0.9558824,1)` |
| `_SDFCenterValue` / `_InvertSDFMap` | `0.5` / `0` |
| `_RotationAngle` / `_LightInfluence` / `_LightDirectionOffset` | `0` / `0.1` / `(0,0.7,0,0)` |
| `_CausticColor` | `(0.8451967,0.5068663,0.9191176,1)` |
| 焦散方向锐度／角度响应／光照影响 | `1.77` / `2.63` / `0.707` |
| 焦散前后过渡／背光最低值 | `0.079` / `0.5` |
| `_CausticUseRefractedUV` / `_CausticBinary` / `_CausticThreshold` | `0` / `0` / `0.5` |
| `_CausticCenter` / `_CausticLightDirectionOffset` | `(0.5,0.5,0,0)` / `(0,0,0,0)` |
| `_isUVMirror` | `0` |

部分原配置固定在计算中：圆盘中心 `(0.5,0.5)`、半径 `0.46`、羽化 `0.127`，内部 MatCap 加法模式、外部 MatCap 颜色调制加法模式，以及 Layer 1／3 的 mode 0。原材质其他模式没有全部作为通用功能实现，不能认为随意换角色参数也会保持原版结果。

## 5. 分项原理、问题与实现

下面的公式是省略分支及变量前缀后的解释性写法；实际入口是 `pupil_refraction_uv`、`pupil_head_frame`、`pupil_caustic`、`ps_model` 和 `ps_pupil_highlight`。它们不是一份可直接替换源码的独立 Shader。

虹膜主路径中的先后关系如下，括号内两种焦散只会采用其中一种：

```text
折射 UV 读取基础色
  → 内部 MatCap
  →（折射 UV 焦散）
  → Layer 1 → Layer 3
  → 最终内层遮罩
  → 原有 HoyoToon 材质／光照颜色处理
  → 外部 MatCap
  →（原 UV 焦散）
```

独立 SDF 亮斑在另外的 Pass 绘制，不在上面的 Layer 1／3 混合链里。这里显示的是当前接入 HoyoToon 的顺序，不表示原版完整后续渲染管线也已经移植。

### 5.1 网格分区：虹膜与白色亮斑分开绘制

**原理**：同一个 Pupil Mesh 内存在不同用途的面，顶点色 R 用来标记所属区域。独立亮斑不能直接当作又一张虹膜表面处理，否则其整片几何可能参与底色、深度或反光计算。

**实现**：`ps_model` 在开启 `_UseEyeRegionRouting` 时用 `clip(0.5 - R)` 排除高光区域；`ps_pupil_highlight` 用相反方向的裁剪保留高光区域，再由 SDF 裁掉不属于亮斑形状的片元。阈值 0.5 是分区判定，不是把眼睛颜色减半。

高光 Pass 使用 `ZWrite Off`、`ZTest LEqual` 和 `Blend SrcAlpha OneMinusSrcAlpha`：不写新的深度，但仍接受已有深度遮挡。当前高光函数保留片元的输出 Alpha 为 1，所以该区域主要是覆盖颜色，并不等于已实现完整的 SDF 渐变透明边缘。这是现有实现的边界。

### 5.2 头部参考轴：为什么不能只用模型对象方向

**原理**：蒙皮会让头部骨骼带动眼睛，但 SkinnedMeshRenderer 所在 GameObject 的朝向不一定同时旋转。用这个对象的固定轴当眼睛轴，会使观察方向、左右分区或高光方向与实际头部运动脱节。

**实现**：`CharacterPupilShaderInputs` 在 `LateUpdate` 中读取 Head 骨骼，避免使用动画求值前的位置；用 `TransformPoint` 转换头部中心，用 `TransformDirection(...).normalized` 转换两个方向，再通过已有 `MaterialPropertyBlock` 写入该 Renderer，而不是修改所有角色共用的材质。

`pupil_head_frame` 将头部向上方向投影到眼睛表面，得到表面内的第一根轴，再叉乘出第二根轴：

```hlsl
headU = normalize(-headX - normalWS * dot(-headX, normalWS));
headV = normalize(cross(headU, normalWS));
```

按单位法线理解，这里不是“乘一次法线就自动得到正确方向”：`normalWS * dot(...)` 是原方向在表面法线上的分量，减掉它才得到贴着眼睛表面的方向。后面用 `dot(view, headU/headV)`，才是把世界空间观察方向转换为眼睛坐标分量。此公式解释投影原理，实际移植保留对应分支的输入和归一化位置，不能直接套用到任意长度的法线上。

头部中心与脸朝向还用于点积符号判定，区分两边的眼睛。各分支的叉乘顺序和镜像符号必须对应原计算，不能把所有分支统一套同一套左右翻转。脸朝向在这些判定中主要影响符号，方向的小幅变化未跨过判定边界时，效果可能保持不变。

但脸朝向并非只负责分区：外部 MatCap 还用它构造头部轴，所以在那里也会连续影响反光方向。必须分清正在观察哪一层，不能因为某个分支没有变化就认为这个参数完全无效。

脚本使用 `ExecuteAlways`，所以编辑模式也能更新。模型导出的局部轴不一定是 Unity 常见的“Y 向上、Z 向前”，需要按 Head 的实际轴绑定；脚本默认轴是针对当前奥黛塔模型选择的，不是原游戏运行时参数已被直接获取的证明。

### 5.3 虹膜折射：沿观察方向寻找一个深度交点

**原理**：当前实现不是改网格，也不是使用一张高度贴图。它先根据 UV 半径构造程序化深度，再沿观察方向寻找与这个深度面的交点，把交点投影回 UV。

`pupil_radial_depth` 中先将虹膜中心移到原点，再按半径缩放：

```hlsl
p = (uv - 0.5) / 0.46;
depth = pow(saturate(dot(p, p)), 0.5 * _DepthPower);
rayUV = -viewInEye.xy * (_ParallaxScale / viewInEye.z);
```

`viewInEye.xy / viewInEye.z` 表示观察射线横向偏移与前向推进的比例。正对眼睛时横向分量小，偏移小；斜看时内部图案的采样位置发生变化。它是原版该分支的程序化视线／深度求交，不应描述成已经实现完整物理折射或真实角膜结构。

**原先的问题**：粗搜索的停止条件、求交细化公式和未收敛时的回退与反编译计算不同，可能使临界区域选中不同的 UV；开启镜像时，横向符号也不完全对应。

**具体修正**：`pupil_refraction_uv` 根据观察角度做约 4～16 次粗搜索。粗搜索在误差严格跨过零之后停止，不把恰好为零当作同一个提前返回条件。随后在正负误差两端之间做 3 次细化：

```hlsl
t = (upperDepth * lowerError - upperError * lowerDepth)
    / (lowerError - upperError);
```

误差小于等于 `0.01` 时使用细化 UV；三次都未收敛则保留粗搜索 UV，而不是无条件使用最后一次细化值。原版比较模式同时修正了 `_isUVMirror` 对横向射线符号的影响，旧比较模式保留原算法。

### 5.4 内部 MatCap：RG 与 B 是两次不同位置的读取

**原理**：RG 编码局部方向，B 提供一个供 MatCap 读取的标量图案。它们装在同一张图里，但不意味着同一 UV 的 RGB 都属于一个法线向量。

**原先的假设**：将 RG 法线直接作用于模拟光滑外层的 MatCap，再尝试将同一采样位置的 B 当作法线强度或反光强度。这会改变效果层和采样关系，不能等同于原版解码路径。

**现在的实现**：先在折射后的 `irisUV` 读取 RG，将 `[0,1]` 转为 `[-1,1]`，补出方向的 Z；再把这三个分量沿眼睛参考轴组合成世界空间方向：

```hlsl
xy = packedRG * 2 - 1;
z = sqrt(max(0, 1 - dot(xy, xy)));
baseNormal = normalize(headV * xy.x + headU * xy.y + normalWS * z);
```

最后转到视空间，构造独立的 MatCap UV，再用这个新 UV 读取同一图的 B：

```hlsl
matcapUV = mul(viewRotation, baseNormal).xy * (0.5 * _BaseMatcapSize) + 0.5;
reflection = packedTexture(matcapUV).b;
color += reflection * _BaseMatcapTint.rgb * _BaseMatcapIntensity;
```

因此 RG 的环形／扇形结构会改变反光图案的采样位置，产生方向性、分块响应，不只是让整个眼睛统一变亮。当前内部反光在装饰层之前加入，后续装饰层会覆盖其中一部分。

### 5.5 装饰层视差与 Alpha 混合：位置和强度都要参与

**原理**：Pupil02 和 Pupil03 是带 Alpha 的图案层。它们以不同的视差系数偏移采样位置，使同一观察角度下两层图案产生相对运动；不是让网格真的前后移动。

**实现**：原版比较模式用眼睛坐标中的观察分量计算 `eyeOffset`，再将圆盘遮罩也乘入偏移。Layer 1 使用 `0.01`，Layer 3 使用 `-0.025`，符号不同意味着偏移方向不同。

**原先的问题**：强度只影响 Layer RGB，Alpha 权重没有按原版 mode 0 同时变化；视差参考轴也不完全对应。

**具体修正**：两层各自使用以下关系：

```hlsl
layerUV = uv * textureScale + textureOffset + eyeOffset * layerParallaxScale;
weight = saturate(layer.a * intensity) * irisMask;
color = lerp(color, layer.rgb * tint.rgb * intensity, weight);
```

`intensity` 既改变层颜色，也改变它盖住下面颜色的权重。不能只改一处，也不能认为 Alpha 已经控制混合就不需要圆盘遮罩。Layer 1 合成后再合成 Layer 3，次序不可任意交换。

### 5.6 最终内层遮罩：限制边缘，但不是再削弱所有高光

**原理**：折射和内部反光有可能在虹膜边缘产生不应该参与完整内层合成的贡献。圆盘范围与贴图 Alpha 一起决定这些计算在哪些位置有效。

**具体修正**：构造径向遮罩，并在小于等于 `0.01` 时归零：

```hlsl
mask = (1 - smoothstep(0.873, 1, dot(p, p))) * eyeMaskAtOriginalUV.a;
innerColor = lerp(baseColorAtOriginalUV, completeInnerColor, mask);
```

这里的 `completeInnerColor` 已包含折射、内部 MatCap、装饰层，以及启用时的折射 UV 焦散。使用原 UV 的底色混回边缘，可以减少内层白色溢出。

这是完整内层的范围控制，每层 Alpha 则是局部图案覆盖权重。两者作用对象不同；原版确实在不同步骤使用遮罩，不能以“重复乘了”作为直接删除其中一步的理由。

### 5.7 外部 MatCap：用球面方向模拟外表面反光

**原理**：外层需要较平滑的弧面响应，不应直接使用内部扇形 RG 法线。由虹膜 UV 构造近似半球方向，再沿头部坐标轴摆放这个半球，模拟眼睛外表面朝向。

**原先的问题**：半球参考轴仍主要使用网格投影切线；外部反光还连续乘了圆盘遮罩，使遮罩渐变再次削弱这一层。

**具体修正**：用 `cross(faceDir, headX)`、`-headX` 及二者叉积构造头部轴；将 `(p.x,p.y,sqrt(max(0,1-|p|²)))` 沿这些轴组合成世界方向。`_EyeMatcapInvertUV` 在构造半球前翻转对应分量，再按 `_EyeMatcapSphericalNormalLerp` 与基础方向混合，转到视空间读取 `_EyeMaskWithMatcap` 的 RGB。

这一步不是改真实网格法线，而是生成供这一层反光使用的计算方向。最终使用原材质的颜色调制加法：

```hlsl
reflection = matcapRGB * _EyeMatcapTint.rgb * _EyeMatcapIntensity;
color = color + reflection * color;
```

因此这层反光保留虹膜颜色参与，不是纯白颜色直接覆盖。原版模式在遮罩 `>0.01` 时启用此层，而非将上式结果再连续乘一次圆盘遮罩；圆盘外则使用基础方向。

### 5.8 焦散：程序化颜色贡献及两种加入位置

**原理**：这里实现的是原版非噪声分支，不是实时追踪穿过角膜的光线。它根据虹膜像素的径向方向、光在眼睛平面内的方向、表面朝向和基础图 Alpha，构造一项颜色贡献。

**新增实现**：`pupil_caustic` 中将径向方向与反向光照方向做点积，再取幂得到方向响应；用表面法线与光方向的点积区分前后侧，以 `smoothstep` 过渡，并按角度响应和光照影响组合。最后保留背光最低值，按需进行二值化，乘 `_CausticColor` 和基础 Alpha。

方向锐度控制亮区集中程度，角度响应控制不同表面朝向的响应，光照影响控制响应变化的参与程度，背光最低值控制背侧贡献下限。它们不是四个等价的亮度乘数。

`_CausticUseRefractedUV` 不只是换一下 UV，它还决定合成位置：

- **开启**：使用折射 UV／折射 Alpha，先加到内层颜色，再经过 Layer 1／3 和最终内层遮罩。
- **关闭**：使用原 UV／原 Alpha，在外部 MatCap 之后加入，并乘圆盘遮罩，不再经过前面的装饰层覆盖。当前奥黛塔使用此模式。

归一化退化点设置小下限避免无定义计算。焦散仅加入基础颜色 Pass，未将它重复加到每个额外光照 Pass。噪声贴图分支不在当前实现范围内。

### 5.9 独立 SDF 亮斑：形状与颜色分开计算

**原理**：SDF 贴图 R 值决定距形状边界的关系，Ramp 决定边界以内的颜色变化。亮斑不是直接把 `EyeHighlight_Diffuse` 的 RGB 贴到眼睛上。

**已有实现**：`ps_pupil_highlight` 先根据头部平面内的光照角度旋转 UV：`angle = _RotationAngle - lightAngle * _LightInfluence`。随后读取 R，按 `_InvertSDFMap` 决定是否反转，减去 `_SDFCenterValue`，裁掉边界外片元。边界内的值除以 `_HighlightRampWidth` 作为 Ramp 横向坐标，当前在 `V=0.875` 读取颜色，再乘 `_EyeHighlightColor` 和 `_HighlightIntensity`。

这样，阈值改变形状范围，RampWidth 改变颜色渐变展开范围，Intensity 改变输出亮度；这三个参数不能互相替代。当前保留片元 Alpha 为 1，独立高光尚未完成与反编译分支相同等级的数值验证。

### 5.10 为什么还需要修正材质 Inspector

**原理**：HoyoToon 将 Shader 属性中的 `start_...`／`end_...` 标记解析成折叠栏，并缓存解析结果。Shader 重导入后可能增加或重排属性，但 Shader 对象本身仍是同一个；只检查对象是否更换，无法发现这种变化。

**具体改动**：`HoyoToonEditor` 初始化时保存属性名称顺序，`HasPropertyOrderChanged` 比较当前属性数量和逐项名称。只在 `EventType.Layout` 阶段将变化纳入重新初始化条件，使新焦散子栏显示正确，也避免在绘制过程中任意重建布局缓存。这是显示结构的修正，不是再次修改材质参数。

## 6. 开关、对比和恢复

| 参数 | 用途 |
|---|---|
| `_UseOriginalEyeFrame` | 开启本轮对齐的头部参考轴、折射细节、原版分层及遮罩计算；关闭保留旧比较路径。 |
| `_UseDecodedEyeMatcap` | 使用解码后内部／外部 MatCap 路径。 |
| `_UseEyeMatcap` | 控制外部 MatCap 贡献；开启解码路径并不代替这一外层开关。 |
| `_UseEyeRegionRouting` | 按顶点色分离虹膜和独立 SDF 高光。 |
| `_UseEyeLayers` / `_UseEyeLayerParallax` / `_UseEyeRefraction` | 分别控制分层、分层视差、基础虹膜折射。 |
| `_UseEyeHighlights` / `_UseHighlightRamp` | 分别控制高光相关处理和高光颜色渐变。 |
| `_UseEyeCaustics` / `_CausticUseRefractedUV` | 控制焦散及加入位置；本轮新增功能可单独关闭对比。 |

核对时上述主功能开关均开启，`_CausticUseRefractedUV=0`。`_UseOriginalEyeFrame` 的名字虽然只写参考轴，实际还切换了折射细节、分层权重和最终遮罩，所以不是一个只旋转坐标轴的小开关。单独关闭某项功能是移除该项贡献，不代表眼睛其他层也会自动回到旧算法。

Inspector 部分 Helpbox 仍是早期近似方案说明，未随本轮全部更新，它们不是验证结论。旧 Packed B 实验开关也不能代替当前解码路径的实际计算。

## 7. 实现边界与验证依据

验证环境：Unity `2022.3.13f1c1`，场景 `Assets/Scenes/UICreate.unity`。此前采用后台临时相机／GPU 测试，不要求切屏或进入 PlayMode。

| 验证 | 条件与结果 |
|---|---|
| 基础 GPU 对照 | 36 组：镜像 0／1 × 焦散关闭／原 UV／折射 UV × 左右眼 × 观察角度 -40／0／40 度。显式 mip 0、统一 linear-clamp，与反编译计算对照；最大 RGB 通道差 `2.3842e-7`，无无效样本。 |
| 扩展头部旋转对照 | 108 组，增加 -27／0／27 度头部旋转及倾斜非单位法线；最大 RGB 通道差约 `0.000378251076`，无超过 `0.01` 的差异或无效样本。 |
| 最终遮罩／镜像修正回归 | 关闭原版头部模式后的前后截图像素差为 0；同配置重复渲染像素差为 0。此结果只覆盖该次修正，不代表本轮所有变化的全量回归。 |
| 实际离屏视觉 | 虹膜边缘白色溢出减少；环形分块反光仍存在，并随观察角度变化。未与原游戏逐像素对齐。 |
| 当前材质读回 | 场景 Renderer 使用目标材质；7 张贴图及上述 39 项暴露参数与原记录一致。 |

数值测试对象是进入原版 GBuffer 前的虹膜 RGB，不包含独立白色高光面、完整 6 MRT 输出、后续光照和后处理。双方强制 mip 0 是为了隔离计算；生产 Shader 没有因此统一改为 mip 0。隐式 Mip 测试曾出现局部差异，不能据此确定哪一方等价于原始编译二进制。

仍未完整实现或验证的部分包括：独立白色高光、噪声焦散／Layer 2／其他混合模式、极端观察角度、原游戏动态参数、原游戏纹理采样状态，以及后续光照与后处理。参考图中的分块反光不能直接判定为错误；当前偏粉、过亮和白色亮斑偏大也不能只靠“贴图名称已一致”判定为正确。

数值对照只能证明所测输入下计算接近，不能把未移植的原版 GBuffer 与整条渲染管线视为已经还原。

## 8. 怎样按效果层定位问题

1. 确认 Shader 无编译错误，目标材质使用 `HoyoToon/Genshin/Character`，头部输入组件正确绑定 Head 和 Pupil Renderer。
2. 保持同一视角、光照及其他参数，单独切换 `_UseEyeCaustics` 比较焦散；不要同时改变多项设置。
3. 单独切换 `_UseOriginalEyeFrame` 比较参考轴／原版计算路径，旋转模型或观察角度，检查左右眼及边缘；只在本地测试，不要误保存为资源默认值。
4. 图案相对运动不对，先检查折射／分层视差及参考轴；内部环形分块响应不对，检查 RG 解码和独立 B 采样，不先把它接到光滑外层。
5. 大白斑形状不对，检查独立 SDF 分支的 UV 旋转、阈值和边缘处理；只是颜色不对，再检查 Ramp、Tint 和 Intensity。
6. 下方整片过亮，单独关闭焦散判断贡献来源；若问题仍在，再检查内部／外部 MatCap，而不是同时调三项强度。
7. 单个功能对照正常，但最终整体偏色，检查纹理颜色空间、后续 HoyoToon 颜色处理和场景照明；不要仅凭最终截图反推某个贴图通道一定错误。
