# AGENTS.md

## 红线（违反即停止）
1. 禁止整文件重写。
2. 禁止删除并重建现有 .cs 文件。
3. 禁止修改任何中文注释、中文字符串、#region、Header、Tooltip 文案，除非我明确要求。
4. 如果目标文件存在乱码、疑似非 UTF-8、或 patch 无法稳定命中，必须停止并汇报；不得改用整文件覆盖。
5. ResourceManager.cs、UIManager.cs、UIViewHandle.cs、GameContext.cs 属于核心文件。一次任务最多修改其中 1 个文件，且只允许修 1 个确定性问题。

## 执行方式
1. 修改前先输出：
  - 文件名
  - 预计修改的具体行段
  - 为什么只改这里
2. 如果执行中需要扩大修改范围，必须先停止并重新确认。
3. 只允许最小 diff；禁止顺手重构、禁止修改无关代码。
4. 如果会改动 public API、资源路径、UIType、Addressable key，必须先停止并确认。
5. 除非我明确说“实现、修改、改代码、应用、接入、删除、提交”等执行性词语，否则先只提供分析、方案或代码片段，不直接修改文件。
6. 读取含中文的代码或文本时，默认按 UTF-8 处理；如果 PowerShell 默认 `Get-Content` 显示乱码，必须先用 `Get-Content -Encoding UTF8`、`rg`、`Select-String` 或字节检查复核。只有复核后仍异常，或 patch 无法稳定命中，才按“疑似乱码/非 UTF-8”停止汇报；禁止仅凭 PowerShell 默认输出判断乱码，也不要为解决该问题要求批量添加 BOM。

## 项目约束
- 这是 Unity C# 项目
- Git 提交标题必须使用中文，除非用户明确要求使用其他语言。
- 保持现有 MVVM / ViewModel 方向
- UI 优先遵循 UIConfig / UIType / UIView
- 资源加载优先复用 ResourceManager
- 优先小范围、可验证改动
- 对 prefab 内必填的 `[SerializeField]` 依赖，不要在每次使用前反复 `null` 检测，也不要为了隐藏 `null` 检测而封装无业务语义的 helper；应直接使用，让漏绑尽早暴露。只有可选节点、兼容旧 prefab、动态加载对象、外部输入才做运行时 `null` 保护。
- C# 类型引用优先通过 `using` 引入命名空间，不要在方法签名和正文里反复写 `System.Action`、`UnityEngine.Vector2` 这类全限定名；只有命名冲突、局部消歧义、或临时避免扩大 using 时才使用全限定名。
- 禁止新增只有 `if (x != null)` 包裹调用、或仅转发 `SetActive/alpha/DOFade` 的无业务语义 helper。除非该方法表达明确 UI 状态或领域动作，例如 `ShowProcessing`、`ShowResultText`、`EnterSelectedState`。
- 如需使用外部 sprite，只允许从 `F:\ChormeDownload\resources-main\resources-main\resources\gi\Sprite` 读取；必须先导入到项目 `Assets/Art` 下按用途归类的目录并应用项目内资源引用，禁止直接引用外部绝对路径或运行时加载外部图片。
- 新增资源默认优先放入现有的最贴近用途目录；除非我明确指定新目录或先确认过，否则不要为了单次需求新建资源文件夹。
- Unity UI 的固定视觉节点应优先做在 prefab 内并通过序列化字段绑定；除非明确需要对象池、动态列表或运行时实例化，否则禁止为了静态 UI 效果在脚本中动态创建 UI 层级。
- Codex 截帧、视频分析、粒子效果对比、候选图预览等临时产物必须统一放入项目根目录 `.codex_tmp_frame_analysis/` 下；禁止在项目根目录分散新建多个 `.codex_tmp_*` 临时目录，临时分析文件不得放入 `Assets/`，除非用户明确要求导入为项目资源。

## 交付格式
修改前先说明：
1. 改哪个问题
2. 改哪些文件
3. 风险点
4. Unity 验证步骤

修改后再说明：
1. 实际改了什么
2. 为什么不会影响现有功能
3. 哪些地方没有验证
