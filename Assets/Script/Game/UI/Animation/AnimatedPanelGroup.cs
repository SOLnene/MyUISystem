using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class AnimatedPanelGroup : MonoBehaviour
{
    [SerializeField]
    AnimatedPanelGroupPreset preset;

    readonly HashSet<AnimatedPanel> trackedPanels = new();
    readonly Dictionary<AnimatedPanel, CancellationTokenSource> showSources = new();

    public UniTask Show(IReadOnlyList<AnimatedPanel> panels)
    {
        if (panels == null)
        {
            throw new ArgumentNullException(nameof(panels));
        }

        var showTasks = new List<UniTask>(panels.Count);
        for (var index = 0; index < panels.Count; index++)
        {
            var panel = panels[index];
            trackedPanels.Add(panel);
            HideImmediate(panel);
            showTasks.Add(ShowAfterDelayAsync(panel, index));
        }

        return UniTask.WhenAll(showTasks);
    }

    public void HideImmediate(AnimatedPanel panel)
    {
        CancelScheduledShow(panel);
        trackedPanels.Add(panel);
        panel.HideImmediate();
    }

    public void HideImmediate(IReadOnlyList<AnimatedPanel> panels)
    {
        for (var index = 0; index < panels.Count; index++)
        {
            HideImmediate(panels[index]);
        }
    }

    public void ShowImmediate(AnimatedPanel panel)
    {
        CancelScheduledShow(panel);
        trackedPanels.Add(panel);
        panel.Show(instant: true).Forget();
    }

    public void ShowImmediate(IReadOnlyList<AnimatedPanel> panels)
    {
        for (var index = 0; index < panels.Count; index++)
        {
            ShowImmediate(panels[index]);
        }
    }

    public void HideAllImmediate()
    {
        CancelAllScheduledShows();
        foreach (var panel in trackedPanels)
        {
            panel.HideImmediate();
        }
    }

    public void ShowAllImmediate()
    {
        CancelAllScheduledShows();
        foreach (var panel in trackedPanels)
        {
            panel.Show(instant: true).Forget();
        }
    }

    async UniTask ShowAfterDelayAsync(AnimatedPanel panel, int displayOrder)
    {
        var currentCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
        showSources[panel] = currentCts;

        try
        {
            if (displayOrder > 0)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(displayOrder * preset.StaggerInterval),
                    cancellationToken: currentCts.Token);
            }

            await panel.Show().AttachExternalCancellation(currentCts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (showSources.TryGetValue(panel, out var activeCts) &&
                ReferenceEquals(activeCts, currentCts))
            {
                showSources.Remove(panel);
                currentCts.Dispose();
            }
        }
    }

    void CancelScheduledShow(AnimatedPanel panel)
    {
        if (!showSources.TryGetValue(panel, out var showCts))
        {
            return;
        }

        showSources.Remove(panel);
        showCts.Cancel();
        showCts.Dispose();
    }

    void CancelAllScheduledShows()
    {
        var sources = new List<CancellationTokenSource>(showSources.Values);
        showSources.Clear();

        foreach (var showCts in sources)
        {
            showCts.Cancel();
            showCts.Dispose();
        }
    }

    void OnDisable()
    {
        HideAllImmediate();
    }
}
