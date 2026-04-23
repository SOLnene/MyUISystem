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

## 项目约束
- 这是 Unity C# 项目
- 保持现有 MVVM / ViewModel 方向
- UI 优先遵循 UIConfig / UIType / UIView
- 资源加载优先复用 ResourceManager
- 优先小范围、可验证改动
- 如需使用外部 sprite，只允许从 `F:\ChormeDownload\resources-main\resources-main\resources\gi\Sprite` 读取；必须先导入到项目 `Assets/Art` 下按用途归类的目录并应用项目内资源引用，禁止直接引用外部绝对路径或运行时加载外部图片。
- 新增资源默认优先放入现有的最贴近用途目录；除非我明确指定新目录或先确认过，否则不要为了单次需求新建资源文件夹。
- Unity UI 的固定视觉节点应优先做在 prefab 内并通过序列化字段绑定；除非明确需要对象池、动态列表或运行时实例化，否则禁止为了静态 UI 效果在脚本中动态创建 UI 层级。

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
