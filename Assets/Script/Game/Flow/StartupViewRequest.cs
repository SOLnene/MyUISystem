using System;
using Cysharp.Threading.Tasks;

// Startup waits for the view's own binding/animation, independently of legacy UIAnimator callbacks.
internal sealed class StartupViewRequest
{
    readonly UniTaskCompletionSource<UIView> opened = new();
    internal bool AllowAutomaticLogin { get; }
    internal StartupViewRequest(bool allowAutomaticLogin = false) => AllowAutomaticLogin = allowAutomaticLogin;
    internal UniTask<UIView> Opened => opened.Task;
    internal void Complete(UIView view) => opened.TrySetResult(view);
    internal void Fail(Exception error) => opened.TrySetException(error);
}
