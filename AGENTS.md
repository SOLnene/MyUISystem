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