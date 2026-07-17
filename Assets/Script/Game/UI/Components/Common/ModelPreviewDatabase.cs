using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ModelPreviewDatabase",
    menuName = "Game/UI/ModelViewer/Preview Database")]
public class ModelPreviewDatabase : ScriptableObject
{
    [SerializeField] List<ModelPreviewDefinition> definitions = new();

    Dictionary<ModelPreviewType, Dictionary<string, ModelPreviewDefinition>> lookup;

    public ModelPreviewDefinition Get(ModelPreviewType previewType, string targetKey)
    {
        EnsureLookup();
        return lookup.TryGetValue(previewType, out var definitionsByKey)
               && definitionsByKey.TryGetValue(targetKey, out var definition)
            ? definition
            : null;
    }

    void EnsureLookup()
    {
        if (lookup != null)
        {
            return;
        }

        lookup = new Dictionary<ModelPreviewType, Dictionary<string, ModelPreviewDefinition>>();
        foreach (ModelPreviewDefinition definition in definitions)
        {
            if (definition == null || string.IsNullOrEmpty(definition.TargetKey))
            {
                continue;
            }

            if (!lookup.TryGetValue(definition.PreviewType, out var definitionsByKey))
            {
                definitionsByKey =
                    new Dictionary<string, ModelPreviewDefinition>(StringComparer.Ordinal);
                lookup.Add(definition.PreviewType, definitionsByKey);
            }

            if (!definitionsByKey.TryAdd(definition.TargetKey, definition))
            {
                Debug.LogWarning(
                    $"Duplicate model preview definition: {definition.PreviewType}/{definition.TargetKey}",
                    this);
            }
        }
    }

    void OnValidate()
    {
        lookup = null;
    }
}
