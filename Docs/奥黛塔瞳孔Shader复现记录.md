# 奥黛塔瞳孔 Shader 复现记录

记录日期：2026-10-03。范围：HoyoToon 的奥黛塔 `Pupil` 渲染，不包含身体材质复现。

本轮代码提交：`24fef959`（对齐奥黛塔虹膜计算并接入焦散及修正材质面板刷新）。

## 1. 当前结论

当前版本是在 HoyoToon 中移植已确认的原版瞳孔计算，不是完整原版 Shader。依据是导出的材质 JSON 和原版编译 Shader 的反编译 HLSL，不是开发者原始源码。

- 当前材质的 7 张有效贴图与导出记录对应，UV 缩放均为 `(1,1)`，偏移均为 `(0,0)`。
- 已检查的 28 个数值、11 个颜色／向量参数一致；这不是对原材质全部参数的覆盖率统计。
- 虹膜折射、分层、内部／外部 MatCap 和非噪声焦散经过受控数值对照。
- 独立 SDF 白色高光、真实运行时输入、原游戏采样状态和最终渲染结果尚未完成全面对齐。
- 参考截图也存在分块反光；当前偏粉、下方偏亮、右侧白色高光偏大的差异仍保留，不能只据此断言贴图错误。

## 2. 文件与改动边界

以下路径均相对项目根目录。

| 文件 | 本轮保存的改动 |
|---|---|
| `Packages/com.meliverse.hoyotoon/Shaders/Genshin Impact/HoyoToonGenshin.shader` | 在现有瞳孔高光栏内增加焦散子栏、开关和参数。默认关闭焦散。 |
| `Packages/com.meliverse.hoyotoon/Shaders/Genshin Impact/Includes/HoyoToonGenshin-declarations.hlsl` | 增加对应焦散参数声明。 |
| `Packages/com.meliverse.hoyotoon/Shaders/Genshin Impact/Includes/HoyoToonGenshin-program.hlsl` | 修正折射求交、镜像方向、分层权重、最终虹膜遮罩和外层反光坐标；加入非噪声焦散及正确叠加时机。 |
| `Packages/com.meliverse.hoyotoon/Scripts/Editor/Shader UI/HoyoToonEditor.cs` | 记录材质属性顺序，在 Layout 阶段发现属性增删／重排时重新初始化 Inspector，避免重导入后沿用旧折叠栏缓存。 |

此前提交 `a695fe5e` 已加入折射／高光基础、网格分区和头部输入脚本。本轮不重复新增这些功能，也不修改模型层级、材质资源、Addressable key 或角色播放链路。

头部输入脚本：`Assets/Script/Game/UI/Components/CharacterDetail/ModelPreview/CharacterPupilShaderInputs.cs`。它使用 `ExecuteAlways`，在 `LateUpdate` 中通过 Renderer 的 `MaterialPropertyBlock` 写入头部中心、头部 X 轴和脸朝向。编辑模式也可更新，不要求进入 PlayMode。三个值来自当前模型骨骼，不是从游戏截帧得到的原始常量。

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

以上是当前 Unity 导入器读回值，不等于原游戏使用相同的压缩、Mip 或过滤配置。7 张图目前均为 `Default` 类型、Bilinear 过滤。Packed Normal 必须保留数据通道；本轮没有将它改为 Unity `NormalMap` 类型，也没有关闭 Mip。

原材质记录：`Assets/Art/Model/Character/odette-snow-swan-s-dance/Materials/Avatar_Girl_Sword_Odette_Mat_Pupil.json`；当前材质为同目录、同名 `.mat`。

## 4. 关键参数快照

当前材质不是简单使用 Shader 默认值；以下值已与导出 JSON 核对。

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

## 5. 计算流程与本轮修正

1. **分区与参考轴**：保留已有顶点色 R 分区，虹膜与独立 SDF 高光面分别处理。原版比较模式用头部参数建立参考轴，不只依赖 SkinnedMeshRenderer 对象方向或网格切线。
2. **折射 UV**：程序化径向深度配合约 4～16 步粗搜索，再做 3 次求交细化。按反编译代码修正越界判断、细化公式和回退；不收敛时保留粗搜索结果。修正 UV 镜像时折射／分层横向符号。
3. **内部 MatCap**：Packed 图 RG 在折射后的虹膜 UV 处提供方向，转入眼睛参考轴；方向转到视空间产生新 UV，再读取同图 B，乘内部 Tint／强度后加到虹膜。不是用同一 UV 的 B 作为 RG 法线强度。
4. **Layer 1／3**：头部参考轴计算视差。原版 mode 0 同时将强度用于 Layer RGB 和 Alpha 权重，再由圆盘遮罩限制混合，不是只把强度乘到颜色上。
5. **最终内层遮罩**：完整内层合成结果按圆盘遮罩混回原 UV 基础颜色，限制边缘溢出。此遮罩与每层 Alpha 权重是两个独立步骤，不能随意合并删除。
6. **外部 MatCap**：原版模式用头部轴构造球面方向，与基础方向混合后读取外层反光。遮罩在这里是 `>0.01` 的启用条件，不再连续乘一次虹膜遮罩。
7. **焦散**：实现奥黛塔使用的非噪声分支，结合径向方向、光照、正反面响应和基础图 Alpha。折射 UV 模式在 Layer 合成前加入；原 UV 模式在外部反光后加入并乘虹膜遮罩。当前材质使用后者。

焦散归一化的退化点使用小下限避免无定义计算；测试域内数值对齐不等于所有极端角度都已验证。独立 SDF 高光沿用已有移植，本轮没有宣称它已完成 GPU 数值闭环。

## 6. 开关、对比和恢复

| 参数 | 用途 |
|---|---|
| `_UseOriginalEyeFrame` | 开启本轮对齐的头部参考轴、折射细节、原版分层及遮罩计算；关闭保留旧比较路径。 |
| `_UseDecodedEyeMatcap` | 使用解码后内部／外部 MatCap 路径。 |
| `_UseEyeRegionRouting` | 按顶点色分离虹膜和独立 SDF 高光。 |
| `_UseEyeLayers` / `_UseEyeLayerParallax` / `_UseEyeRefraction` | 分别控制分层、分层视差、基础虹膜折射。 |
| `_UseEyeHighlights` / `_UseHighlightRamp` | 分别控制高光相关处理和高光颜色渐变。 |
| `_UseEyeCaustics` / `_CausticUseRefractedUV` | 控制焦散及加入位置；本轮新增功能可单独关闭对比。 |

核对时上述主功能开关均开启，`_CausticUseRefractedUV=0`。关闭开关表示回到对应比较路径／移除贡献，不表示恢复完整旧版；精确恢复代码应使用 Git 历史。

Inspector 部分 Helpbox 仍是早期近似方案说明，未随本轮全部更新，它们不是验证结论。旧 Packed B 实验开关也不能代替当前解码路径的实际计算。

## 7. 验证结果与边界

验证环境：Unity `2022.3.13f1c1`，场景 `Assets/Scenes/UICreate.unity`。此前采用后台临时相机／GPU 测试，不要求切屏或进入 PlayMode。

| 验证 | 条件与结果 |
|---|---|
| 基础 GPU 对照 | 36 组：镜像 0／1 × 焦散关闭／原 UV／折射 UV × 左右眼 × 观察角度 -40／0／40 度。显式 mip 0、统一 linear-clamp，与反编译计算对照；最大 RGB 通道差 `2.3842e-7`，无无效样本。 |
| 扩展头部旋转对照 | 108 组，增加 -27／0／27 度头部旋转及倾斜非单位法线；最大 RGB 通道差约 `0.000378251076`，无超过 `0.01` 的差异或无效样本。 |
| 最终遮罩／镜像修正回归 | 关闭原版头部模式后的前后截图像素差为 0；同配置重复渲染像素差为 0。此结果只覆盖该次修正，不代表本轮所有变化的全量回归。 |
| 实际离屏视觉 | 虹膜边缘白色溢出减少；环形分块反光仍存在，并随观察角度变化。未与原游戏逐像素对齐。 |
| 当前材质读回 | 场景 Renderer 使用目标材质；7 张贴图及上述 39 项暴露参数与原记录一致。 |

数值测试对象是进入原版 GBuffer 前的虹膜 RGB，不包含独立白色高光面、完整 6 MRT 输出、后续光照和后处理。双方强制 mip 0 是为了隔离计算；生产 Shader 没有因此统一改为 mip 0。隐式 Mip 测试曾出现局部差异，不能据此确定哪一方等价于原始编译二进制。

已有验证未发现新增 Shader 编译错误。本次归档只做 Git 差异检查，不重新进入 PlayMode、重导入或修改场景。非目标角色和其他 Shader 的 Inspector 没有全量回归；只能确认新焦散默认关闭，以及已经测试的旧比较路径一致。

本机证据在 `.codex_tmp_frame_analysis/odette_matcap_20261002/`：

- `composition_gpu_mip0.txt`：36 组数值结果。
- `composition_gpu_rotated_mip0.txt`：108 组扩展结果。
- `composition_before_after.png`：修正前后截图。
- `composition_focused_views_grid.png`：三个观察角度。
- `game_reference_current_matched.png`：尺度匹配的参考对比，未使用 AI 补细节。

反编译参照在 `.codex_tmp_frame_analysis/odette_shader_cli_20261002/programs/Pupil/Pupil_019_ps_offset_00057196.hlsl`。分析文件、第三方模型及材质被 Git 忽略，本次不提交，其他机器需要本地资源才能复查。文档保存方法、结论和边界，不能替代未入库的测试输入。

## 8. Unity 手动复查与后续顺序

1. 确认 Shader 无编译错误，目标材质使用 `HoyoToon/Genshin/Character`，头部输入组件正确绑定 Head 和 Pupil Renderer。
2. 保持同一视角、光照及其他参数，单独切换 `_UseEyeCaustics` 比较焦散；不要同时改变多项设置。
3. 单独切换 `_UseOriginalEyeFrame` 比较参考轴／原版计算路径，旋转模型或观察角度，检查左右眼及边缘；只在本地测试，不要误保存为资源默认值。
4. 下一步优先数值核对独立 SDF 白色高光，再核对纹理采样／颜色空间、原游戏动态输入，以及最终光照／输出／后处理。
5. 保留偏粉、过亮、高光偏大的待验证结论，不凭感觉关闭 Mip、删除反光色块或修改贴图来宣称复现完成。

未完成项不自动授权继续修改。当前按用户要求暂时保留效果，先提交归档。
