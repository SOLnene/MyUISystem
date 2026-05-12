using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class UITransitionGroup : MonoBehaviour
{
    [SerializeField]
    AnimatedPanel[] panels;

    public async UniTask Show(bool instant = false)
    {
        var tasks = new List<UniTask>();
        AddShowTasks(tasks, instant);
        await UniTask.WhenAll(tasks);
    }

    public async UniTask Hide(bool instant = false)
    {
        var tasks = new List<UniTask>();
        AddHideTasks(tasks, instant);
        await UniTask.WhenAll(tasks);
    }

    void AddShowTasks(List<UniTask> tasks, bool instant)
    {
        if (panels == null)
        {
            return;
        }

        for (int i = 0; i < panels.Length; i++)
        {
            var panel = panels[i];
            if (panel == null)
            {
                continue;
            }

            tasks.Add(panel.Show(instant));
        }
    }

    void AddHideTasks(List<UniTask> tasks, bool instant)
    {
        if (panels == null)
        {
            return;
        }

        for (int i = 0; i < panels.Length; i++)
        {
            var panel = panels[i];
            if (panel == null)
            {
                continue;
            }

            tasks.Add(HidePanel(panel, instant));
        }
    }

    static async UniTask HidePanel(AnimatedPanel panel, bool instant)
    {
        await panel.Hide(instant);
    }
}
