# 奥黛塔 Body Shader：原理、改动与剩余差异

## 1. 范围与结论

本文整理 HoyoToon 中奥黛塔 Body、Body01、Dress01 的分区、法线、丝袜、丝绸、闪点、金属和阴影实现，不包含瞳孔、Face、Hair 的新功能。

依据是原始材质 JSON 和编译 Shader 的反编译 HLSL，并非开发者原始源码，也未确认所有提取变体都是 PC 游戏当前实际启用的变体。

当前状态：部分原版局部计算已移植并经过受控数值对照，但输入生产、遮蔽组合、纹理采样和最终渲染链尚未全部对齐。不能称为完整还原，也不能把剩余差异全部归因于管线或引擎。

## 2. 文件职责

以下文件位于 `Packages/com.meliverse.hoyotoon/Shaders/Genshin Impact/`。

| 文件 | 改动 |
|---|---|
| `HoyoToonGenshin.shader` | 丝绸、Shining V2、丝袜原版响应、渐变、第二层视角阴影、金属补充参数和五组阴影柔化控件。 |
| `Includes/HoyoToonGenshin-declarations.hlsl` | 贴图、材质参数和不显示于 Inspector 的环境明暗输入声明。 |
| `Includes/HoyoToonGenshin-common.hlsl` | 分区、法线、丝袜、丝绸、闪点、金属和 Ramp 柔化计算，保留旧模式分支。 |
| `Includes/HoyoToonGenshin-program.hlsl` | 生产片元流程中的分支选择、坐标／法线输入，以及底色与各类高光的合成顺序。 |

本次收尾只补充本文并保存已有改动，不再修改公式，不改动画、模型层级、Addressable key 或渲染管线。

## 3. 材质分支和贴图

Body01 不是给 Body 再叠一张图，而是独立材质分支；Dress01 也有自己的参数和算法差异。材质槽通常作用于对应 submesh，可见区域必须以 Renderer 的 submesh／材质绑定为准，不能只按名字判断。

当前本地材质开关：

| 参数 | Body | Body01 | Dress01 |
|---|---:|---:|---:|
| `_UseNativeBodyResponse` | 1 | 0 | 0 |
| `_ShiningMaterialBranch` | 0 | 1 | 2 |
| `_EnableCharacterSilksAnisotropy` | 1 | 0 | 0 |
| `_UseCharacterShining_V2` | 1 | 1 | 1 |
| `_UseGradientStocking` | 1 | 0 | 0 |
| `_UseSecondFresnel` | 1 | 0 | 0 |

Body01／Dress01 通过 `_ShiningMaterialBranch>0.5` 进入原版织物基础响应。仅关闭 V2 不会退回全部旧算法；丝绸开关也不决定底色算法。

下表贴图简称省略 `Avatar_Girl_Sword_Odette_Tex_` 前缀；Escoffier 项保留真实名称。

| 入口 | Body 绑定 | Body01／Dress01 绑定 | 作用 |
|---|---|---|---|
| `_BumpMap` | `Body_Normalmap` | `Body01_Normalmap` | RG 参与法线，B 还参与相关材质／闪点分区，不只有普通法线信息。 |
| `_LightMapTex` | `Body_Lightmap` | `Body01_Lightmap` | R 参与高光强度／金属判断，B 参与高光区域及细节控制，A 参与五区材质 ID。 |
| `_PackedShadowRampTex` | `Body_Shadow_Ramp` | `Body01_Shadow_Ramp` | 按材质 ID、明暗坐标和昼夜选择阴影颜色。 |
| `_StockingsDetailTex` | `Avatar_Girl_Pole_Escoffier_Tex_Stockings_Detailmap` | 同左 | 丝袜细节法线、花纹和混合遮罩；源 JSON 也引用此名称，不应擅自替换成奥黛塔织物图。 |
| `_AnisoSilkThreadMap` | `Stockings_Detailmap` | 未绑定，丝绸关闭 | 丝绸 RG 细节方向、B 平滑度、A 遮罩／分区；与上一行不是同一入口。 |
| `_ShiningCustomIDMask_V2` | `Stockings` | 同左 | Body 使用 B 图形；织物按组使用 R／G／A 图形。 |

奥黛塔 Stockings_Detailmap.png 当前 sRGBTexture=0，避免数据通道采样时发生颜色转换。Body Packed Normal 当前以 Default、线性数据导入，由 Shader 手动解释通道；不能因名称含 Normalmap 就直接套 Unity 自动法线重编码。

## 4. 具体实现与效果

### 4.1 分区与法线

原版模式采用材质 ID 区间的排他上界，避免 0.4、0.6 等边界被多个区域覆盖；旧模式保持原边界。

新增 cloth_normal_mapping，按提取分支的 RG／Z 方式解码，并由位置和 UV 的屏幕导数构建参考方向。生产流程修正传入位置的方向；Body／Dress01 区域 1 按源分支中和 RG。退化 UV 无法构建参考轴时保留几何法线，这是本地安全边界，不是原版全部异常输入的等价证明。

丝袜细节在相应区域、Lightmap B 条件满足时与基础法线组合，并传给相关高光和闪点。旧模式保留此前对同名变量遮蔽和 RG 重复解码的修正。预期是细节真正影响反光方向，不是整体提亮，固定正面光照下不一定明显。

### 4.2 丝袜底色与高光

character_stocking 分开处理花纹底色、视角亮部、第一层视角阴影、第二层视角阴影、白色处理、UV 渐变和高光距离衰减。

第二层视角阴影保留源分支 Soft 两次参与的公式，不任意替换成 smoothstep。渐变保留轴交换及 `1-t*(1-t)` 曲线，放在白色处理之后、阴影之前。

原版模式不再把基础色重复乘入丝袜阴影。丝袜高光单独返回为 clothSpecular，在基础阴影之后叠加，避免被 Ramp 再次压暗。高光衰减使用真实相机距离；环境亮暗颜色与强度已接入，但状态来自 Unity 阴影替代。

这些修改改变底色、花纹、反光和阴影，不是增加透明度；看起来更透不等于 Alpha 被修改。

### 4.3 丝绸各向异性

character_silk 围绕组合后的表面法线建立织物方向，读取 Thread 图，结合粗糙度、各向异性、主／第二高光瓣、观察方向和光照方向计算响应。

随后处理输入色混合、Thread ID 颜色、饱和度、导数柔化与最终强度，将结果叠加到已有底色。它产生方向性织物反光，不是闪烁星星，也不替代丝袜底色。当前没有覆盖原始材质中的全部丝绸选项。

### 4.4 Shining V2

Body 分支分别实现程序圆点和两组贴图形状，使用单元哈希、密度、相机相对位置、时间频率、强度及颜色。分区依赖 Packed Normal B，部分组还受到 Lightmap B 限制。

Body01／Dress01 使用四组共享采样器，不套 Body 的 B 通道／圆点路线；组别对应 ID4／ID2／ID3／ID1。接入矩形图形、固定／随机旋转、随机尺寸、顶点颜色遮罩、单元中心密度、面向和高光权重。Dress01 另有按相机距离混合的闪点颜色。

V2 开启时替换旧闪点，不重复叠加；关闭时保留旧闪点。时间／相机变化导致脉冲变化属于公式行为，但位置、密度和采样完全等价尚未证明。

旧 Body 形状路径用分支外导数和 SampleGrad 稳定采样；原版 Body 模式保留隐式 Sample。SampleGrad 不等于锁定 mip 0。

### 4.5 普通高光与金属

原版普通高光用幂值作阈值判断，不额外乘幂值衰减。接通各材质区域的 SpecOpacity，高光染色读取丝袜等可选底色响应之前的已着色 diffuse。

native_body_metal 分开计算金属底色与高光：MatCap、Specular Ramp／锐利层、环境衰减、阴影颜色，以及 MTSpecularOffset、MTSpecularShadowScale、MTSpecularAO。

Body01 的提取分支没有相同的偏移／镜像高光／可选 AO 行为，保留独立判断。金属底色自行处理阴影后，不再重复乘普通 Toon Ramp；金属高光最后单独叠加。

### 4.6 五区阴影柔化

增加五个独立开关和五个范围值，放在现有 Shadow 区域，按原版模式、Ramp 和材质区域显示。

```hlsl
weight = min(1, saturate(2 * ramp.r * ramp.g * ramp.b - 1)
    / max(0.001, range));
color = lerp(ramp, 1, weight);
```

开关按非零即启用解释，Body 第 4 区的 0.2 不是 20% 混合权重。本地 Body 开关为 `[1,1,0,0.2,0]`，范围 `[2,2,0,0.5,0]`；Body01／Dress01 开关 `[0,0,1,1,0]`，范围 `[1,0,2,2,0]`。Shader 默认开关均为 0，范围均为 1。

## 5. 验证结果与边界

以下是此前保留的报告；本次收尾读取核对，没有重新运行 Unity、切屏或进入 PlayMode。

| 对照 | 结果 | 能证明的范围 |
|---|---|---|
| 分区／普通高光 | 12,864 样本，不一致 0，旧模式不一致 0 | 所测边界和参数组合的局部计算。 |
| 丝袜亮暗颜色 | 73,728 样本，不一致 0，最大误差约 5.96e-8 | 给定环境状态后的颜色公式，不证明状态生产一致。 |
| Unity 环境替代 | 294,912 样本，不一致 0，最大误差约 5.96e-8 | 替代公式与独立替代参考一致，不证明等价于游戏。 |
| Body 金属 | 96 组，所测底色／高光最大误差均为 0 | 局部金属路径。 |
| Body01／Dress01 金属 | 96 组，底色最大误差约 0.000300318，高光误差 0，无无效值 | 所测织物金属分支。 |
| 生产阴影柔化 | 90 组，最大误差约 2.98e-8，无无效值／洋红错误 | 共用明暗坐标、主亮度 1、环境 0、双方 mip 0 的 Ramp 颜色组合，不含丝袜、高光和输入生产。 |
| 柔化离屏对照 | 原版模式最大通道变化 15～16/255；旧模式及重复渲染变化 0 | 该次修正生效，所测旧模式未变化；隔离时关闭了丝绸／闪点，不是实机参照。 |

本地证据在 `.codex_tmp_frame_analysis/odette_body_shining/`，不纳入 Git。主要报告为 body_partition_spec_gpu_audit.json、stocking_light_color_gpu_audit.json、stocking_unity_shadow_gpu_audit.json、body_metal_gpu_audit.json、cloth_metal_native_audit.json 和 native_shadow_soft_production_audit_20261005.json。

## 6. 还差什么

1. **环境状态生产未对齐。** 丝袜和金属共用 `max(_NativeStockingEnvironmentShadow,1-unity_shadow)`，是逐像素投影阴影替代，不是原游戏环境状态。内部输入 0 不强制亮部，1 强制暗部；金属还有环境状态触发二值遮罩的处理，半影连续值不能直接视为等价。
2. **遮蔽组合未完整接通。** 新 Ramp 柔化只在 ao 完全等于 1 时生效。ao 可能包含自定义 AO 和投影衰减；其他情况仍用旧处理。这是移植限制，不是原版条件或引擎要求。
3. **丝绸未覆盖全部功能／变体。** 虹彩、正面增强、其他颜色控制与采样配置未实现或未完整核对。未启用选项不能误报为奥黛塔一定缺少的可见贡献。
4. **活跃原版变体和运行时参数未全面确认。** 材质 JSON、提取程序和游戏实际变体需要配对；环境／主光等输入不能仅按字段名推定值一致。
5. **导数与采样仍有差异。** 部分粗导数用普通 ddx／ddy。48 组织物实际贴图隐式采样最坏 RMSE 约 0.0123177、最大误差约 0.67909；改粗导数没有消除差异。诊断 mip 0 将最大误差降至约 0.00123787，但生产未强制 mip 0，不能认定锁 mip 是正确修复。
6. **完整颜色／光照／输出顺序未证明等价。** 当前保留 HoyoToon 光照与最终输出，不是原游戏完整 GBuffer／光照／后处理链。局部通过不代表整体颜色一致，也不能把公式缺漏全部归因于管线。
7. **没有同输入实机逐像素参照。** 离屏能验证功能、变化与回归，不能证明相同姿态、相机、光照、环境和曝光下游戏质感一致。更透、更平、闪点是否过强等观感仍未定性。

## 7. 后续验证与旧功能

新增功能有开关，旧材质不启用新模式时保留旧算法；已有局部回归，不代表全部角色、关键词和平台都已验证。

Unity 验证应先确认 Renderer 的材质／submesh 分区；固定姿态、相机、主光、时间和曝光，分别对照底色、丝袜、丝绸、V2、金属和柔化，不同时调整贴图和公式。重点补测半影、ao 小于 1、区域边界和相机距离。

原版对照先统一输入并比较后处理前线性结果，再核对输入生产和最终输出。不要为追求截图亮度随意更换 Ramp，也不要把所有关闭状态都叫原始旧版。

旧角色、PlayMode、跨平台、动态场景与全关键词回归仍未完成。本次收尾只有文档写入和 Git diff 检查，没有新的视觉验证。

## 8. 保存范围

本文与四个 Shader 文件由 Git 保存。`Assets/Art/Model/` 被现有规则忽略，三份材质、贴图和导入设置保留本机，不强制纳入；只检出代码不能自动恢复全部本地视觉配置。

恢复配置应核对本地原始 JSON 的分支、贴图和已实现参数，再应用第 3、4 节的开关及柔化配置，不把尚未实现的参数当作已经支持的功能。本文侧重原理、改动和缺失，不记录提交时间或提交过程。
