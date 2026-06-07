using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIRaycastProbe : MonoBehaviour
{
    [SerializeField]
    int maxResults = 20;
    [SerializeField]
    bool includeCanvasGroups = true;

    readonly List<RaycastResult> results = new();

    void Update()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        if (EventSystem.current == null)
        {
            Debug.LogWarning("[UIRaycast] No EventSystem in scene.");
            return;
        }

        var eventData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        results.Clear();
        EventSystem.current.RaycastAll(eventData, results);

        Debug.Log(BuildLog());
    }

    string BuildLog()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[UIRaycast] Click: {Input.mousePosition}, Count: {results.Count}");

        int count = Mathf.Min(results.Count, maxResults);
        for (int i = 0; i < count; i++)
        {
            RaycastResult result = results[i];
            GameObject target = result.gameObject;
            Graphic graphic = target.GetComponent<Graphic>();
            Selectable selectable = target.GetComponent<Selectable>();

            sb.AppendLine($"#{i} {GetPath(target.transform)}");
            sb.AppendLine($"    module={result.module?.GetType().Name}, depth={result.depth}, distance={result.distance}");
            sb.AppendLine($"    graphic={graphic?.GetType().Name}, raycastTarget={graphic?.raycastTarget}, alpha={graphic?.color.a}");
            sb.AppendLine($"    selectable={selectable?.GetType().Name}, interactable={selectable?.interactable}");

            if (includeCanvasGroups)
                AppendCanvasGroups(sb, target.transform);
        }

        return sb.ToString();
    }

    void AppendCanvasGroups(StringBuilder sb, Transform target)
    {
        CanvasGroup[] groups = target.GetComponentsInParent<CanvasGroup>(true);
        for (int i = 0; i < groups.Length; i++)
        {
            CanvasGroup group = groups[i];
            sb.AppendLine($"    CanvasGroup[{i}] {GetPath(group.transform)} alpha={group.alpha}, blocks={group.blocksRaycasts}, interactable={group.interactable}, ignoreParent={group.ignoreParentGroups}");
        }
    }

    static string GetPath(Transform target)
    {
        if (target == null)
            return "<null>";

        string path = target.name;
        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }

        return path;
    }
}
