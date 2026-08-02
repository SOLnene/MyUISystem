using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

internal static class AchievementConfigValidator
{
    internal static bool TryValidate(
        AchievementConfigData config,
        ItemDatabase itemDatabase)
    {
        List<string> errors = new();
        if (config == null)
        {
            errors.Add("Config is null.");
        }
        else if (config.categories == null)
        {
            errors.Add("Categories is null.");
        }

        bool hasItemDatabase = itemDatabase != null;
        if (!hasItemDatabase)
        {
            errors.Add("ItemDatabase is unavailable.");
        }

        if (config?.categories != null)
        {
            HashSet<string> categoryIds = new(StringComparer.Ordinal);
            HashSet<string> achievementIds = new(StringComparer.Ordinal);
            for (int categoryIndex = 0;
                 categoryIndex < config.categories.Count;
                 categoryIndex++)
            {
                AchievementCategoryConfigData category =
                    config.categories[categoryIndex];
                if (category == null)
                {
                    errors.Add($"Category[{categoryIndex}] is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(category.id))
                {
                    errors.Add($"Category[{categoryIndex}] has an empty id.");
                }
                else if (!categoryIds.Add(category.id))
                {
                    errors.Add($"Category id is duplicated: {category.id}.");
                }

                if (category.achievements == null)
                {
                    errors.Add($"Category '{category.id}' has null achievements.");
                    continue;
                }

                for (int achievementIndex = 0;
                     achievementIndex < category.achievements.Count;
                     achievementIndex++)
                {
                    AchievementDefinition definition =
                        category.achievements[achievementIndex];
                    string achievementPath =
                        $"Category '{category.id}' achievement[{achievementIndex}]";
                    if (definition == null)
                    {
                        errors.Add($"{achievementPath} is null.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(definition.id))
                    {
                        errors.Add($"{achievementPath} has an empty id.");
                    }
                    else if (!achievementIds.Add(definition.id))
                    {
                        errors.Add($"Achievement id is duplicated: {definition.id}.");
                    }

                    if (string.IsNullOrWhiteSpace(definition.progressKey))
                    {
                        errors.Add($"Achievement '{definition.id}' has an empty progressKey.");
                    }

                    if (definition.target <= 0)
                    {
                        errors.Add($"Achievement '{definition.id}' target must be greater than zero.");
                    }

                    if (definition.reward == null)
                    {
                        errors.Add($"Achievement '{definition.id}' reward is missing.");
                        continue;
                    }

                    if (definition.reward.amount <= 0)
                    {
                        errors.Add($"Achievement '{definition.id}' reward amount must be greater than zero.");
                    }

                    if (hasItemDatabase &&
                        itemDatabase.GetItemByID(definition.reward.itemId) == null)
                    {
                        errors.Add(
                            $"Achievement '{definition.id}' reward item is missing: {definition.reward.itemId}.");
                    }
                }
            }
        }

        if (errors.Count == 0)
        {
            return true;
        }

        StringBuilder message = new("Achievement config validation failed:");
        foreach (string error in errors)
        {
            message.Append("\n- ");
            message.Append(error);
        }

        Debug.LogError(message.ToString());
        return false;
    }
}
