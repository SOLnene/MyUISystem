using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class TutorialScheduler : IDisposable
{
    readonly TutorialDefinition[] definitions;
    readonly IDisposable[] signalAdapters;

    TutorialController currentController;
    int nextDefinitionIndex;
    bool started;
    bool disposed;

    internal TutorialScheduler(
        IReadOnlyList<TextAsset> configs,
        params IDisposable[] signalAdapters)
    {
        definitions = new TutorialDefinition[configs.Count];
        for (int index = 0; index < configs.Count; index++)
        {
            definitions[index] = TutorialDefinitionLoader.Load(configs[index]);
        }

        this.signalAdapters = signalAdapters ?? Array.Empty<IDisposable>();
    }

    internal void Start()
    {
        if (started || disposed)
        {
            return;
        }

        started = true;
        TryStartNext();
    }

    void TryStartNext()
    {
        while (!disposed && nextDefinitionIndex < definitions.Length)
        {
            TutorialDefinition definition = definitions[nextDefinitionIndex++];
            var controller = new TutorialController(definition, HandleTutorialEnded);
            currentController = controller;
            if (controller.TryStart())
            {
                return;
            }

            currentController = null;
            controller.Dispose();
        }
    }

    void HandleTutorialEnded(
        TutorialController controller,
        TutorialExitReason reason)
    {
        if (disposed || currentController != controller)
        {
            return;
        }

        currentController = null;
        controller.Dispose();
        // 外部关闭或测试跳过只取消当前调度；正常完成后才继续下一份配置。
        if (reason == TutorialExitReason.Completed)
        {
            TryStartNext();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        currentController?.Dispose();
        currentController = null;
        foreach (IDisposable signalAdapter in signalAdapters)
        {
            signalAdapter.Dispose();
        }
    }
}
