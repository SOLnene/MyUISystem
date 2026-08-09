using System;
using System.Collections.Generic;

internal static class TutorialProgressService
{
    static readonly HashSet<string> CompletedTutorials = new();

    public static bool IsCompleted(string tutorialId)
    {
        return CompletedTutorials.Contains(tutorialId);
    }

    public static bool Complete(string tutorialId)
    {
        return CompletedTutorials.Add(tutorialId);
    }

    public static TutorialSaveData ExportSaveData()
    {
        var completedIds = new List<string>(CompletedTutorials);
        completedIds.Sort(StringComparer.Ordinal);
        return new TutorialSaveData
        {
            completedIds = completedIds
        };
    }

    public static void ImportSaveData(TutorialSaveData saveData)
    {
        CompletedTutorials.Clear();
        if (saveData?.completedIds == null)
        {
            return;
        }

        foreach (string tutorialId in saveData.completedIds)
        {
            CompletedTutorials.Add(tutorialId);
        }
    }
}
