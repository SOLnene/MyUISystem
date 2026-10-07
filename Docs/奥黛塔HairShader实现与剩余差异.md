# 奥黛塔 Hair Shader：原理、实现与剩余差异

## 1. 范围与结论

本轮处理奥黛塔 Hair 材质的分区、法线、AO、Ramp、金属，以及水晶饰件的 MatCap、直接高光和边缘反光。普通发束主体可以阶段性收尾，不能称为整个原版 Shader 或最终游戏画面的完整还原。

依据是原始材质 JSON、编译 Shader 的反编译 HLSL，以及保留下来的数值和离屏测试。它们不是开发者提供的可编辑源码，也不是游戏实际绘制时的抓帧数据。收尾仅整理既有实现，不再调整 Shader 公式、材质或场景。

主要原版参照是 `Hair_091_ps_offset_002DAE32.hlsl`，对应 Shader PathID `3555544351309267020`。原始 `Shader#128.dat` 的 SHA256 为 `8D4205A7080EBBD0334701F5ECC0A3BDF1BFCA4B7EDA2E6B3564BB6447BA0534`。

该资源目前确认到的质量标记为 `MHY_SHADER_LOW`；尚未确认它就是 PC 游戏当前活跃的变体。主要片元参照输出 7 个 `SV_Target`，属于中间缓冲输出，不能将其中一个颜色结果直接当作最终屏幕颜色。

## 2. 改动文件及职责

以下文件均位于 `Packages/com.meliverse.hoyotoon/Shaders/Genshin Impact/`：

| 文件 | 具体职责 |
|---|---|
| `HoyoToonGenshin.shader` | 增加独立 Hair 模式和水晶 MatCap 贴图入口；将已有分区柔化、金属参数的 Inspector 显示条件扩展到 Hair。 |
| `Includes/HoyoToonGenshin-declarations.hlsl` | 声明 `_UseNativeHairResponse` 和 `_HairCrystalMatcap`。 |
| `Includes/HoyoToonGenshin-common.hlsl` | Hair 接入原版分区、半兰伯特、Ramp 柔化及金属共用算法；新增水晶坐标、直接高光、边缘反光和增量合成。 |
| `Includes/HoyoToonGenshin-inputs.hlsl` | 增加内部插值量 `native_hair_view : TEXCOORD11`，保存顶点先归一化的视线。 |
| `Includes/HoyoToonGenshin-program.hlsl` | 在 VS 传递新视线；在 PS 选择 Hair 法线／AO 规则，并按顺序合成水晶贡献。 |

未新增 C# 控制器、公开 C# API、资源加载路线或 Addressable key。没有将 Body、Face 或瞳孔改为 Hair 模式。

## 3. 模型、材质与开关

### 3.1 一份 Hair 材质作用于哪些网格

展示 Prefab 中，`Bang` Renderer 和 `Body` 网格的 submesh 0 共用 `Avatar_Girl_Sword_Odette_Mat_Hair`。因此同一个开关同时影响普通发束和该 submesh 中的饰件，并不表示修改了 Body 材质。

Hair 材质的 `variant_selector=0`，即 HoyoToon 的 Base 类型。`Bangs` 类型主要参与已有头发投影路线，不是另一套普通发束高光算法；本轮没有为了名称对应而将 Bang 改成 Bangs 类型。

### 3.2 保存的本地配置

| 参数 | 当前值 | 作用 |
|---|---|---|
| `_UseNativeHairResponse` | 1 | 启用本轮 Hair 计算。Shader 默认值仍为 0。 |
| `_UseNativeBodyResponse` / `_ShiningMaterialBranch` | 0 / 0 | 不启用 Body 或闪点分区模式。 |
| `_useShadowSoft5` / `_shadowSoftRange5` | 1 / 2 | 第 5 分区使用原材质的阴影柔化配置。 |
| `_UseBumpMap` / `_BumpScale` | 1 / 0.5 | 启用法线数据入口及其强度。 |
| `_DummyFixedForNormal` / `_isNativeMainNormal` | 1 / 0 | 使用当前材质对应的导数参考位置模式。 |
| `_SpecularHighlights` / `_UseToonSpecular` | 0 / 0 | 当前材质不启用普通 Toon 高光。 |
| `_UseRimLight` / `_RimLightType` | 1 / 2 | 保留已有屏幕深度边缘光路线。 |
| `_EnableTonemapping` | 1 | 保留已有材质内色调映射。 |
| `_AutomaticNight` / `_DayOrNight` | 1 / 0 | 保留 HoyoToon 当前色调输入方式。 |
| `_UseHairShadow` | 0 | 当前未启用 HoyoToon 头发投影功能。 |

`_HitColor` 和 `_ElementRimColor` 当前都为零。它们不是水晶的直接高光／边缘反光颜色，不应混为同一个开关。

### 3.3 已绑定贴图

| Shader 入口 | 贴图 | 使用方式 |
|---|---|---|
| `_MainTex` | `Avatar_Girl_Sword_Odette_Tex_Hair_Diffuse` | 普通发束与饰件的基础颜色；图内已经画有宽色带和发束明暗。 |
| `_BumpMap` | `Avatar_Girl_Sword_Odette_Tex_Hair_Normalmap` | 非第 5 区的方向数据、第 5 区的 R 通道 AO，以及水晶 B 通道遮罩。 |
| `_LightMapTex` | `Avatar_Girl_Sword_Odette_Tex_Hair_Lightmap` | 材质分区、其他区域 AO 和已有材质控制数据。 |
| `_HairCrystalMatcap` | `Avatar_Tex_Crystal_Diffuse` | 局部水晶的 MatCap 颜色／Alpha。 |

Hair Normalmap 保持 Default 类型、线性数据读取；不能因为文件名包含 Normalmap 就改用会重新打包通道的常规法线导入。此次 Hair 收尾没有修改贴图导入器。

## 4. 普通发束主体：具体如何计算

### 4.1 分区与法线必须按材质选择

原版模式的材质 ID 使用排他上界，避免边界值落入多个区。Hair 复用已接通的原版分区算法，但保留原材质已有的各区启用开关。

奥黛塔 Hair 的原始 `_SelectMatID=5`。在该区域，Shader 中和法线图 RG，让其保留模型表面方向；原始 R 值另用于 AO。不能将这个 R 同时当作普通法线 X 分量，否则会把 AO 图案转成错误的反光方向。

其他区域复用已解码的原版 RG／Z 法线和屏幕导数参考轴。`_DummyFixedForNormal=1` 时使用与原版位置方向对应的参考量，避免位置方向符号反转。部分原版粗导数在当前 Unity 目标上仍适配为普通 `ddx/ddy`，不宣称全部采样等价。

Hair 的第 5 区规则与 Body／Dress01 的第 1 区规则分别判断，不把 Body 的区域中和条件套到 Hair 上。

### 4.2 AO 与 Ramp 坐标

当前预设的第 5 区 AO 来自原始 Normalmap R；结合原材质 `HairMapAntiAlias=1`、`AOShadowWarpScale=1`，低于 0.25 的值被拒绝：

```hlsl
hairAO = rawNormalR >= 0.25 ? rawNormalR : 0;
```

其他区域仍使用 Lightmap G。后续结合顶点 AO、顶点控制的 Ramp 宽度和分区参数生成阴影坐标。原版半兰伯特系数采用 `NdotL * 0.4975 + 0.5`，不是旧路径的 `0.5` 系数。

第 5 区柔化通过既有 `_useShadowSoft5`／`_shadowSoftRange5` 接入，没有新增另一套阴影工具。当前原版 Ramp 合成仅在外部 AO／投影组合因子 `ao == 1` 时生效；否则保留旧兼容路线。这是现有移植边界，不是原版要求。

### 4.3 金属与普通高光

Hair 接入已有原版金属函数，将金属底色和加法高光分开。金属底色完成自身阴影处理后，不再重复乘普通 Toon Ramp，高光最后独立叠加。

Hair 金属暂用亮环境基线 `environmentShadow=0`，没有把 Body／丝袜的逐像素 Unity 阴影替代直接套到 Hair。原版共享环境状态的生产方式仍未还原。

当前原材质关闭普通高光，所以本轮没有为大面积发束添加新各向异性高光。宽亮带很大一部分已画在 Diffuse 中；这不表示金属、水晶或屏幕边缘光也被关闭。

## 5. 水晶饰件：已实现哪些贡献

### 5.1 MatCap 不是直接使用相机滚转矩阵

`native_hair_crystal_matcap` 根据相机相对模型原点的方向构建参考轴，再将表面法线投影成二维采样坐标。原版这条路线与直接将法线乘相机 View 矩阵不同，不能随意替换。

读取 `_HairCrystalMatcap` 后，使用已解码的奥黛塔预设：色相旋转 -1 弧度、尺寸 1、Alpha 强度 0.2。当前参数固定在对应计算中，没有扩展为覆盖所有角色的通用水晶参数组。

相机严格位于参考轴正上／正下方时，水平轴无法定义；适配返回零贡献以避免无效值。此处理单独测试，不视为已经证明该退化输入与原版等价。

### 5.2 直接高光与边缘反光

直接高光使用源分支的 GGX 计算、粗糙度 0.3、对应 `alpha²=0.0081`、分布峰值限制 12、颜色 Fresnel 和 `NdotL`。使用原编译计算中的线性颜色 `(0.693,1.781,1.781)`。

边缘反光采用：

```hlsl
edgeAngle = max(0.0001, 1 - min(1, abs(dot(vertexView, normal))));
edgeWeight = min(1, pow(edgeAngle, 10) * 12.6);
```

线性颜色为 `(0.105,1.057,1.623)`。这是水晶饰件响应，不是给所有发束增加统一反光。

### 5.3 为什么增加一个视线插值字段

源 VS 先对视线归一化，再插值给 PS；直接高光随后再次归一化，边缘项则使用没有再次归一化的插值结果。两者不能统一改成“PS 中归一化一次”。

因此新增 `native_hair_view` 保存这条时序，原来的 HoyoToon `view` 保持不变。关闭 Hair 模式时新字段输出零，但未测 GPU 性能，不能由此断言插值带宽或所有平台成本完全不变。

### 5.4 只合成可用贡献，不伪造完整水晶层

当前将 MatCap、直接高光、边缘反光相加，再按 Normalmap 原始 B 和 0.85 权重进入 Overlay 的增量贡献：

```hlsl
overlayScale = baseColor <= 0.5 ? 2 * baseColor : 2 * (1 - baseColor);
delta = availableCrystalResponse * overlayScale * (rawNormalB * 0.85);
result = baseColor + delta;
```

这是从 Overlay 的仿射关系分离出来的局部贡献，不是将缺失的 Cubemap／折射当成完整黑色水晶层。完整水晶响应、GBuffer 编码与最终合成仍未接通。

采样发生在材质级 Hair 开关分支内、B 遮罩乘法之前，避免按逐像素 B 开分支破坏隐式纹理导数。

Hair Normalmap 图集约 94.62% 的 B 为零；这只是图集像素统计，不是角色表面积或屏幕占比。实际调试中普通发束多数区域遮罩为零，所以水晶阶段的改动集中在耳饰／饰件，大面积发束看起来变化小是符合当前分区的。

## 6. 目前仍保留的共用输出差异

### 6.1 原版字段不等于当前 Unity 输入

从 Shader 序列化参数元数据确认到以下绑定，而不是仅按寄存器数值猜名称：

| 原版字段 | 位置 | 当前边界 |
|---|---|---|
| `_ES_CharacterMainLightBrightness` | `cb0[315].x` | 原版外部整体亮度输入；当前仍用 HoyoToon 主光／环境合成，未获取游戏实际值。 |
| `_ES_CharacterColorTone` | `cb0[316].w` | 是原版颜色调节字段，不能直接命名为昼夜比例；当前仍用已有 AutomaticNight 路线。 |
| `_ElementRimColor` / `_HitColor` | `cb0[341]` / `cb0[342]` | 当前两种颜色均为零，不产生常态贡献。 |

另一个单颜色输出分支固定方向采样的 `t7` 是 Cubemap，对应 `_BakedLightInMirror`。它是镜像照明线索，不能把该采样当作普通 PC 视图的统一光照修正。

### 6.2 泛用 Fresnel 仍有公式与顺序差异

原版在取幂前限制角度项：`pow(max(0.0001, 1-NdotV), power)`；现有 HoyoToon 在取幂后限制结果：`max(pow(1-NdotV, power), 0.00001)`。

原版所测分支在 Ramp／水晶之后相加，当前泛用 Fresnel 在阴影乘色和水晶之前执行。实际颜色为零时两边贡献都为零，所以这不是当前普通发束观感差异的原因；将来启用受击／元素边缘色时仍需重新核对。

### 6.3 照明、深度 Rim 与 Tonemapping

`CalcLighting` 用 Unity 主光／环境的较大值乘色，并可另加球谐 GI；不是原版完整共用光照 Pass。此前检查时 `_GI_Intensity=0`，该加色项不贡献，但主光／环境乘色仍在。

Rim Type 2 通过 `_CameraDepthTexture` 的当前及偏移位置采样生成屏幕边缘，再结合视线／光照方向。它主要改变轮廓附近，不是宽发束高光。收尾检查到场景相机 `depthTextureMode=None`；未显式请求深度，不能据此保证每次绘制都没有其他来源，也不能称正式预览已接通并验证这项输入。

材质内 Tonemapping 包含模拟 Bloom 的提亮、有理曲线和白平衡矩阵。它不是相机统一后处理，也未证明等价于原版后处理。诊断关闭它会改变画面，但这种对照不是目标游戏效果，不能直接据此永久关闭或认定存在重复 Tonemapping。

## 7. 验证结果及其证明范围

本地报告和测试代码位于 `.codex_tmp_frame_analysis/odette_hair_source_20261007/`，不纳入项目资源或 Git。数值参照与生产函数分别从反编译指令和当前函数中提取，再适配绑定；不是拿生产函数自身生成目标图片。

| 检查 | 结果 | 能证明的范围 |
|---|---|---|
| 主体局部 GPU 对照 | 72 组，最大误差约 2.98e-7，无无效值／测试 Shader 错误 | 分区、法线、AO／Ramp 坐标、金属及柔化的所测输入。 |
| 水晶 MatCap | 18 组同输入对照，最大误差约 2.06e-7；另 9 组竖直轴退化适配检查 | 局部采样与增量公式；退化处理不属于原版等价证明。 |
| 水晶直接光／边缘项 | 54 组，最大误差约 4.77e-7，无无效值／测试 Shader 错误 | 3 个观察位置、3 个光方向、2 种法线长度下的所测响应。 |
| 普通发束非金属 Ramp | 12 组同输入基线，最大误差约 9.92e-5、最大 RMSE 约 5.98e-7，每组最多 1 个像素超过 1e-5 | 显式 LOD0、普通高光关闭、原版亮环境基线；不是完整光照。 |
| 外部局部阴影适配 | 另 24 组故意不同输入，最大误差约 0.373 | 确认 Unity 局部投影与固定原版环境基线会分歧，不能算同输入公式失败。 |
| 正式 Shader 与临时实验基线 | 6 张同配置图，像素完全一致 | 临时 Shader 的基线没有引入额外画面偏差。 |
| 当前与源非金属 Ramp 合成 | 6 个相机／光照组合，所选 Hair 区域差异像素为 0；隔离底色 3 组也为 0 | 当前测试条件下普通发束合成已基本对上。 |
| 弱光／深度探针 | 源 Ramp 对照最多 1 像素相差 1 个 8 位等级；移除照明或 Rim 会产生明确差异 | 证明最终输入／输出链会改变画面，不证明哪个消融图是原版。 |
| 泛用 Fresnel | 12 组：实际零色 6 组误差 0；非零测试色最大误差约 4.05e-5 | 局部项的限制顺序差异；不包含整个合成顺序验证。 |

旧模式回归：此前 6 组关闭 Hair 模式的图与修改前基线像素一致。水晶新增直接光／边缘项相对 MatCap-only 模式改变每张 1100×1000 图约 170–515 个像素，集中在饰件；不能宣称改善了整片头发。

最终照明／色调映射／Rim 消融实验共 18 张图。关闭材质 Tonemapping 改变几乎整个 Hair 蒙版区域；关闭深度 Rim 改变约 4,554–8,772 个区域像素。这些实验是定位贡献，不是以游戏画面作为目标的视觉验证。

前期出现过纯青色的异步 Shader 编译占位图，已拒绝作为结果，改为临时同步编译后重新渲染，并在结束时恢复编译设置。

收尾的新增检查仅为读回：当前材质使用 `HoyoToon/Genshin/Character`、8 个 Pass，Shader 未报告编译错误，Hair 开关／柔化值／4 张贴图与上述配置一致。没有重新执行全部历史数值或视觉测试。Console 保留其他导入／历史诊断错误，不应将本结论写成整个项目 Console 无错误。

## 8. 尚未还原、尚未验证的部分

1. 水晶 Cubemap／折射资源与完整响应未接入；不能拿随意的反射图冒充原资源。
2. 原版活跃质量变体、关联 VS／PS、实际逐次绘制参数尚未全面配对。
3. 共用环境状态、整体亮度、颜色调节的上游生产未还原；不能将剩余问题一概归因于引擎或后处理。
4. 外部 AO／局部投影条件下的 Ramp 兼容路线、泛用 Fresnel 及最终照明合成仍有边界。
5. 原版采样器、压缩、Mip、导数与实际运行时资源绑定未全部一致；诊断 LOD0 不等于生产应该锁定 LOD0。
6. 描边、头发投影、完整 GBuffer／共用照明／后处理未完成原版整链路对照。
7. 动画、极端角度、其他角色、其他平台和全部关键词未全量回归；未做 GPU 性能测量。
8. 没有相同姿态、相机、光照、环境及曝光的实机逐像素参照，不宣称最终外观完全相同。

普通 Hair 主体不再因“尚未全验证”自动增加新功能。后续应先确认明确错误属于公式、输入、资源或最终合成中的哪一层，再做范围明确的修正。

## 9. 与其他部位的关系和恢复检查

Body／服装、Face、虹膜的主要实现分别保留在已有文档中；本轮不重新修改它们。`Face_Eye` 有独立 Renderer 但共享 Face 材质；`Brow` 有独立材质但同样走 Face／SDF，鼻子高光关闭。原始 Face／Brow JSON 也引用同一 Shader 和相同关键词，不能将眉毛列成缺少整套独立 Shader 的部位。

睫毛具体几何归属尚未单独核对，不仅凭节点名推断。独立白色亮斑虽然已有 SDF 计算，完整游戏绘制／合成证据仍不足；`Alpha=1` 本身不能作为错误依据，暂不凭亮度或大小调整。`Eff_001/002`／`EffectMesh` 未纳入这轮主体复现，是否用于常态展示要先核对原材质及使用场景。

本地模型、材质和贴图位于被现有 Git 规则忽略的 `Assets/Art/Model/`。代码和本文保存的是实现与配置说明，不是完整模型资源包；只检出代码不能自动恢复全部本地视觉输入。

恢复／复查时：

1. 确认 Bang 和 Body submesh 0 的 Hair 材质引用，不把模式开在 Body 或 Face 上。
2. 读回 `_UseNativeHairResponse=1`、第 5 区柔化 `1 / 2`、水晶贴图及线性 Normalmap 设置。
3. 固定姿态、时间、相机和光照，分别测试 Hair 模式、局部水晶贡献和输出链，不同时更换贴图及参数。
4. 比较 Rim 时先确认该相机本次绘制确有有效深度输入，不将参数开关等同于效果已经生效。
5. 保留 Shader 默认 Hair 模式关闭以兼容旧角色；奥黛塔本地已验证配置保持开启。

此次收尾不切屏、不进入 PlayMode、不保存原本未保存的场景；卡池、Addressables、包依赖和其他文档的已有改动不属于本轮范围。
