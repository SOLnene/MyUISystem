using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class ExactDuplicateImageFinder
{
    private const string MenuPath = "Tools/Asset Audit/查找完全重复图片";
    private const int FileCompareBufferSize = 81920;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".tga",
        ".psd",
        ".bmp",
        ".tif",
        ".tiff",
        ".exr",
        ".hdr"
    };

    [MenuItem(MenuPath, false, 2000)]
    private static void FindDuplicates()
    {
        string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        IReadOnlyList<string> targetPaths = GetTargetImagePaths(selectedPath);

        if (targetPaths.Count == 0)
        {
            Debug.LogWarning($"[重复图片检查] 所选路径中没有可检查的图片：{selectedPath}");
            return;
        }

        try
        {
            IReadOnlyList<List<ImageFileRecord>> duplicateGroups = FindDuplicateGroups(targetPaths);
            LogResults(selectedPath, targetPaths.Count, duplicateGroups);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[重复图片检查] 扫描失败：{exception.Message}");
            Debug.LogException(exception);
        }
    }

    private static IReadOnlyList<List<ImageFileRecord>> FindDuplicateGroups(
        IReadOnlyList<string> targetPaths)
    {
        var targetSet = new HashSet<string>(targetPaths, StringComparer.OrdinalIgnoreCase);
        List<ImageFileRecord> projectImages = GetProjectImagePaths()
            .Select(CreateFileRecord)
            .Where(record => record != null)
            .ToList();

        var hashCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<List<ImageFileRecord>>();

        // 文件完全相同时长度必然相同。先按长度过滤，避免读取无关图片内容。
        IEnumerable<IGrouping<long, ImageFileRecord>> candidateBuckets = projectImages
            .GroupBy(record => record.Length)
            .Where(group => group.Count() > 1 && group.Any(record => targetSet.Contains(record.AssetPath)));

        foreach (IGrouping<long, ImageFileRecord> sizeBucket in candidateBuckets)
        {
            IEnumerable<IGrouping<string, ImageFileRecord>> hashBuckets = sizeBucket
                .GroupBy(record => GetSha256(record.AssetPath, hashCache))
                .Where(group => group.Count() > 1 && group.Any(record => targetSet.Contains(record.AssetPath)));

            foreach (IGrouping<string, ImageFileRecord> hashBucket in hashBuckets)
            {
                // SHA-256 只用于快速归组；最终仍逐字节比较，保证结果确实是同一文件。
                foreach (List<ImageFileRecord> exactGroup in SplitByExactContent(hashBucket))
                {
                    if (exactGroup.Count > 1 && exactGroup.Any(record => targetSet.Contains(record.AssetPath)))
                    {
                        exactGroup.Sort((left, right) =>
                            string.Compare(left.AssetPath, right.AssetPath, StringComparison.OrdinalIgnoreCase));
                        results.Add(exactGroup);
                    }
                }
            }
        }

        return results;
    }

    private static IReadOnlyList<string> GetTargetImagePaths(string selectedPath)
    {
        if (IsSupportedImage(selectedPath))
        {
            return new[] { selectedPath };
        }

        if (!AssetDatabase.IsValidFolder(selectedPath))
        {
            return Array.Empty<string>();
        }

        return FindImagePaths(new[] { selectedPath });
    }

    private static IReadOnlyList<string> GetProjectImagePaths()
    {
        return FindImagePaths(new[] { "Assets" });
    }

    private static IReadOnlyList<string> FindImagePaths(string[] folders)
    {
        return AssetDatabase.FindAssets("t:Texture2D", folders)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(IsSupportedImage)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsSupportedImage(string assetPath)
    {
        return !string.IsNullOrEmpty(assetPath) &&
               assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
               SupportedExtensions.Contains(Path.GetExtension(assetPath));
    }

    private static ImageFileRecord CreateFileRecord(string assetPath)
    {
        string fullPath = Path.GetFullPath(assetPath);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        return new ImageFileRecord(assetPath, new FileInfo(fullPath).Length);
    }

    private static string GetSha256(
        string assetPath,
        IDictionary<string, string> hashCache)
    {
        if (hashCache.TryGetValue(assetPath, out string cachedHash))
        {
            return cachedHash;
        }

        using var stream = File.OpenRead(Path.GetFullPath(assetPath));
        using SHA256 sha256 = SHA256.Create();
        string hash = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
        hashCache.Add(assetPath, hash);
        return hash;
    }

    private static IEnumerable<List<ImageFileRecord>> SplitByExactContent(
        IEnumerable<ImageFileRecord> records)
    {
        var exactGroups = new List<List<ImageFileRecord>>();

        foreach (ImageFileRecord record in records)
        {
            List<ImageFileRecord> matchingGroup = exactGroups.FirstOrDefault(group =>
                FilesAreEqual(group[0].AssetPath, record.AssetPath));

            if (matchingGroup != null)
            {
                matchingGroup.Add(record);
            }
            else
            {
                exactGroups.Add(new List<ImageFileRecord> { record });
            }
        }

        return exactGroups;
    }

    private static bool FilesAreEqual(string firstAssetPath, string secondAssetPath)
    {
        string firstFullPath = Path.GetFullPath(firstAssetPath);
        string secondFullPath = Path.GetFullPath(secondAssetPath);

        var firstInfo = new FileInfo(firstFullPath);
        var secondInfo = new FileInfo(secondFullPath);
        if (firstInfo.Length != secondInfo.Length)
        {
            return false;
        }

        using FileStream firstStream = File.OpenRead(firstFullPath);
        using FileStream secondStream = File.OpenRead(secondFullPath);
        var firstBuffer = new byte[FileCompareBufferSize];
        var secondBuffer = new byte[FileCompareBufferSize];

        while (true)
        {
            int firstRead = ReadBlock(firstStream, firstBuffer);
            int secondRead = ReadBlock(secondStream, secondBuffer);
            if (firstRead != secondRead)
            {
                return false;
            }

            if (firstRead == 0)
            {
                return true;
            }

            for (int index = 0; index < firstRead; index++)
            {
                if (firstBuffer[index] != secondBuffer[index])
                {
                    return false;
                }
            }
        }
    }

    private static int ReadBlock(Stream stream, byte[] buffer)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }

    private static void LogResults(
        string selectedPath,
        int targetCount,
        IReadOnlyList<List<ImageFileRecord>> duplicateGroups)
    {
        if (duplicateGroups.Count == 0)
        {
            Debug.Log($"[重复图片检查] 完成。目标：{selectedPath}，检查图片：{targetCount}，未发现完全重复文件。");
            return;
        }

        Debug.LogWarning(
            $"[重复图片检查] 完成。目标：{selectedPath}，检查图片：{targetCount}，" +
            $"发现完全重复组：{duplicateGroups.Count}。以下每条结果均可点击定位资源。");

        for (int groupIndex = 0; groupIndex < duplicateGroups.Count; groupIndex++)
        {
            List<ImageFileRecord> group = duplicateGroups[groupIndex];
            Debug.LogWarning($"[重复图片组 {groupIndex + 1}] 共 {group.Count} 个完全相同的文件。");

            foreach (ImageFileRecord record in group)
            {
                Object context = AssetDatabase.LoadMainAssetAtPath(record.AssetPath);
                Debug.Log($"[重复图片组 {groupIndex + 1}] {record.AssetPath}", context);
            }
        }
    }

    private sealed class ImageFileRecord
    {
        public string AssetPath { get; }
        public long Length { get; }

        public ImageFileRecord(string assetPath, long length)
        {
            AssetPath = assetPath;
            Length = length;
        }
    }
}
