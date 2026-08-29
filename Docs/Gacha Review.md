# Gacha 抽卡系统 Code Review
# 第一轮
    ✔ 1. 抽卡结果是否只由 Service 决定
    
    是的 ✅
    
    ✔ 2. View 是否无法触发新的 Draw
    
    是的（UI 禁用）✅
    
    ✔ 3. 跳过是否只是展示逻辑
    
    是的（不影响抽卡）✅


## GachaReview
- GachaViewModel 当前直接持有 GachaEntryViewModel
- 业务结果 → 展示 VM 的转换发生在 Application 层
- 若后续出现多种展示方式，可能需要拆分
- isDrawing 同时承担 UI 防重入 与 抽卡流程互斥职责
- 后续可能需要拆分为：
        - isBusy (流程)
        - canInput (UI)
- ReactiveProperty 不是线程安全概念 异步中必须拍快照       
- GachaSessionViewModel 生命周期目前由 View 间接控制
- ViewModel 只负责创建，不负责结束 后续可能需要 SessionCoordinator 或显式结束信号
- HasNext() 同时承担查询与状态更新 后续可考虑纯计算 + Reactive 绑定
## 笔记
- reveal可以单独逻辑，但不该作为uiview界面
- 谁持有动画谁负责播放，应该由reveal自己控制动画播放
- OnComplete() 是 DOTween 的链式 API,onComplete 是 Tween 的字段 / 回调槽位，只用OnComplete()
- 可能被快速启动的动画流程和动画本身都该能cancel
- 资源加载：用 requestId ,流程等待：用 CancellationToken
## 1. 当前系统已实现能力
- 卡池切换（TopHub → GachaVM → MiddleView）
- 抽卡流程（DrawCommand → Session → Result）
- UI 与 Domain 解耦（VM / View / Service）

## 2. 当前已知风险点
- UI 异步资源加载可能存在覆盖问题
- Session 生命周期略长
- 存在嵌套 Subscribe
- UI 可重入规则未明确
- detailPopup和revealview的关系不明确，有重复问题
## 3. 本轮 Review 目标
- 确认 CurrentPoolType 数据流是否单向
- 确认 UI 是否能被安全打断
- 为下一个功能（保底 / UP）预留结构
## 4. 后续开发约束（临时）

- UI 异步加载必须是 LatestOnly
- View 不直接创建 Session
- 抽卡流程中是否允许切池需显式定义
- 禁止在 View 中出现业务规则

# Gacha System Overview

## Layers
- Domain: GachaService（抽卡规则 / 保底 / UP）
- Application: GachaViewModel（流程 / 会话）
- Presentation: View / ViewModel（展示）

## Data Flow
UI Input
 → GachaViewModel
 → GachaService
 → GachaSessionViewModel
 → UI Presentation

## Non-goals
- View 不直接处理概率
- View 不创建 Session

# UI Async Rules

- UI 异步加载必须是 LatestOnly
- View 生命周期结束时不得继续 Apply 结果
- 不使用 CancellationToken 作为 UI 主方案
- 优先使用 requestId / SwitchLatest / TakeUntilDisable
# UI动画规则
## 一、核心原则

**动画 ≠ 流程**

- DOTween 只负责“画面怎么动”
- UniTask / async 只负责“流程什么时候继续”
- 二者通过「等待」而不是「互相控制」来协作

---

## 二、职责划分（Who does what）

| 工具 | 职责 |
|---|---|
| DOTween | 动画表现（位移 / 缩放 / 淡入淡出等） |
| UniTask | 业务流程控制 |
| CancellationToken | 流程作废信号 |
| seq.Kill() | 动画立刻停止 |
## 二、常见使用模式

### 1️⃣ 普通动画等待（不可中断）

```csharp
await seq.AsyncWaitForCompletion();
```
2️⃣ 动画可被中断（推荐）
```csharp
await seq.AsyncWaitForCompletion()
         .AttachExternalCancellation(token);
```
配合：
```csharp
token.Cancel();
seq.Kill();
```
动画可能被中断（切 Tab / 切 View）
await seq.AsyncWaitForCompletion()
.AttachExternalCancellation(token);

View 切换 / Tab 切换
Cancel();   // Cancel token + Kill Tween

Skip / 快进
seq.Kill(true); // Complete 当前动画

3. 推荐工程结构
   View 层（流程层）
   View
   ├─ Init()
   ├─ PlayEnter()
   ├─ Cancel()
   ├─ Skip()
   └─ OnDisable() -> Cancel()


职责：

管流程（Init / 切换 / 顺序）

管 CancellationToken

管生命周期（Enable / Disable / Destroy）

决定什么时候播放动画

Motion 层（动画层）
Motion
├─ PlayEnter()
├─ PlayExit()
└─ Cancel()


职责：

只负责 DOTween 动画

不关心 async / UniTask

不关心 CancellationToken

不感知 View 生命周期

4. 关键约束（必须遵守）

Motion 不 await

Motion 不持有 token

View 不直接操作 Sequence

View 切换时必须先 Cancel

OnDisable 必须调用 Cancel
### 商业项目中异步加载 + 动画的 Cancel 处理规范

| 场景 / 操作                  | 商业项目常见处理方式                                                                 | 是否 Cancel | 为什么 / 说明                                                                 |
|------------------------------|-------------------------------------------------------------------------------------|-------------|-------------------------------------------------------------------------------|
| **OnDisable / tab 切走**     | 只 Cancel 当前正在进行的**动画**（motionRoot.Cancel()），**不 Cancel 加载任务**     | 动画：是<br>加载：否 | 动画残留视觉差（切回时可能看到半途动画），但加载可以后台继续（下次切回时可能已完成，避免重复加载） |
| **OnDestroy / 真正销毁**     | Cancel 所有（cts.Cancel() + motionRoot.Cancel()） + Dispose 订阅                     | 是          | 彻底清理资源，避免内存泄漏和后台无用任务                                      |
| **Init / Bind 时**           | 先 Cancel 旧 cts（initCts?.Cancel()），再 new 新 cts，再开始新任务                  | 是（旧的）  | 防止旧任务干扰新任务（e.g. 旧加载设错 sprite）                                |
| **Skip / 用户跳过**          | seq.Kill(true) + ApplyEndState（跳到最终态）                                        | 是          | 用户体验优先，立即结束动画并显示最终结果                                      |
| **池化回收（UIManager）**    | 在 OnRecycle / OnClose 中 Cancel 动画 + ResetToIdle，但不 Cancel 加载（加载可复用） | 动画：是<br>加载：否 | 池化对象要“干净”（无残留动画），但不浪费已完成的加载（下次取出可直接用）      |

**总结原则**：
- **动画**：几乎所有隐藏/销毁/跳过场景都要 Cancel（视觉残留最明显，用户最容易感知）。
- **加载任务**：只在真正销毁或明确需要中断时 Cancel，允许后台完成（节省重复 IO，提升切回流畅度）。
- **订阅**：在 OnDestroy / OnRelease 时 Dispose，OnDisable 只 Clear（视框架而定）。
  这个 UI 是不是只展示真实数据？

如果是 → 可以直接订阅 Model
如果不是 → 必须加一层 VM