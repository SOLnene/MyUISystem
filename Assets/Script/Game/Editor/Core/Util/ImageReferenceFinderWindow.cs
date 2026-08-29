using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

internal sealed class ImageReferenceFinderWindow : EditorWindow
{
    private const string MenuPath = "Tools/Asset Audit/图片引用检查";

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
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

    private static readonly HashSet<string> SerializedAssetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".prefab",
        ".unity",
        ".asset",
        ".mat",
        ".anim",
        ".controller",
        ".overridecontroller",
        ".rendertexture",
        ".guiskin",
        ".preset",
        ".playable",
        ".timeline"
    };

    private static readonly HashSet<string> SearchableTextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".json",
        ".txt",
        ".xml",
        ".yaml",
        ".yml",
        ".uxml",
        ".uss",
        ".shader",
        ".cginc",
        ".hlsl"
    };

    private static Dictionary<string, List<string>> reverseDependencyIndex;

    private readonly List<string> targetImagePaths = new();
    private readonly List<ImageReferenceResult> results = new();
    private Vector2 scrollPosition;
    private string selectionDescription = "尚未读取 Project 视图选择";
    private bool scanStringReferences;
    private bool lastScanIncludedStringReferences;

    [InitializeOnLoadMethod]
    private static void RegisterDependencyIndexInvalidation()
    {
        EditorApplication.projectChanged -= InvalidateDependencyIndex;
        EditorApplication.projectChanged += InvalidateDependencyIndex;
    }

    private static void InvalidateDependencyIndex()
    {
        reverseDependencyIndex = null;
    }

    [MenuItem(MenuPath, false, 2001)]
    private static void OpenWindow()
    {
        var window = GetWindow<ImageReferenceFinderWindow>();
        window.titleContent = new GUIContent("图片引用检查");
        window.minSize = new Vector2(720f, 420f);
        window.ReadCurrentSelection();
        window.Show();
    }

    private void OnGUI()
    {
        DrawToolbar();
        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "确定引用来自 Unity AssetDatabase 的直接依赖关系；疑似引用来自可选的代码或配置字符串扫描。" +
            "Addressable、Resources 和 SpriteAtlas 状态只表示资源可被加载或收录，不代表业务一定使用。",
            MessageType.Info);

        EditorGUILayout.LabelField("检查范围", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(selectionDescription, EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space(6f);

        if (results.Count == 0)
        {
            EditorGUILayout.HelpBox("读取当前选择后点击“开始扫描”。支持选择图片、多个图片或文件夹。", MessageType.None);
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        foreach (ImageReferenceResult result in results)
        {
            DrawResult(result, lastScanIncludedStringReferences);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("读取当前选择", EditorStyles.toolbarButton, GUILayout.Width(110f)))
            {
                ReadCurrentSelection();
            }

            using (new EditorGUI.DisabledScope(targetImagePaths.Count == 0))
            {
                if (GUILayout.Button("开始扫描", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                {
                    Scan();
                }
            }

            scanStringReferences = GUILayout.Toggle(
                scanStringReferences,
                new GUIContent("扫描代码/配置字符串", "默认关闭。开启后额外读取 C#、JSON 等文本配置，查找动态资源地址。"),
                EditorStyles.toolbarButton,
                GUILayout.Width(145f));

            using (new EditorGUI.DisabledScope(results.Count == 0))
            {
                if (GUILayout.Button("复制报告", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                {
                    EditorGUIUtility.systemCopyBuffer = BuildReport();
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label($"图片 {targetImagePaths.Count} 张", EditorStyles.miniLabel);
        }
    }

    private static void DrawResult(
        ImageReferenceResult result,
        bool includedStringReferences)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        using (new EditorGUILayout.HorizontalScope())
        {
            string status = result.SerializedReferences.Count > 0
                ? "确定引用"
                : result.StringReferences.Count > 0
                    ? "仅疑似引用"
                    : includedStringReferences
                        ? "未发现引用"
                        : "未发现序列化引用";

            EditorGUILayout.LabelField($"{Path.GetFileName(result.AssetPath)}  [{status}]", EditorStyles.boldLabel);
            if (GUILayout.Button("定位图片", GUILayout.Width(80f)))
            {
                PingAsset(result.AssetPath);
            }
        }

        EditorGUILayout.SelectableLabel(result.AssetPath, EditorStyles.miniLabel, GUILayout.Height(18f));
        DrawResourceState(result);
        DrawReferenceSection("确定的序列化引用", result.SerializedReferences);
        DrawReferenceSection("疑似字符串引用", result.StringReferences);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4f);
    }

    private static void DrawResourceState(ImageReferenceResult result)
    {
        var states = new List<string>();
        if (result.IsInResources)
        {
            states.Add("Resources");
        }

        if (!string.IsNullOrEmpty(result.AddressableAddress))
        {
            states.Add($"Addressable: {result.AddressableAddress}");
        }

        if (result.SpriteAtlasPaths.Count > 0)
        {
            states.Add($"SpriteAtlas: {string.Join(", ", result.SpriteAtlasPaths)}");
        }

        EditorGUILayout.LabelField(
            states.Count == 0 ? "资源系统状态：无" : $"资源系统状态：{string.Join(" | ", states)}",
            EditorStyles.miniLabel);
    }

    private static void DrawReferenceSection(string title, IReadOnlyList<string> references)
    {
        if (references.Count == 0)
        {
            return;
        }

        EditorGUILayout.LabelField($"{title}（{references.Count}）", EditorStyles.miniBoldLabel);
        foreach (string assetPath in references)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(assetPath, EditorStyles.miniLabel, GUILayout.Height(18f));
                if (GUILayout.Button("定位", GUILayout.Width(55f)))
                {
                    PingAsset(assetPath);
                }
            }
        }
    }

    private void ReadCurrentSelection()
    {
        targetImagePaths.Clear();
        results.Clear();

        foreach (Object selectedObject in Selection.objects)
        {
            string selectedPath = AssetDatabase.GetAssetPath(selectedObject);
            if (IsImagePath(selectedPath))
            {
                targetImagePaths.Add(selectedPath);
                continue;
            }

            if (!AssetDatabase.IsValidFolder(selectedPath))
            {
                continue;
            }

            targetImagePaths.AddRange(
                AssetDatabase.FindAssets("t:Texture2D", new[] { selectedPath })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(IsImagePath));
        }

        List<string> distinctPaths = targetImagePaths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        targetImagePaths.Clear();
        targetImagePaths.AddRange(distinctPaths);

        selectionDescription = targetImagePaths.Count == 0
            ? "当前选择中没有可检查的图片。"
            : $"已读取 {targetImagePaths.Count} 张图片。";
        Repaint();
    }

    private void Scan()
    {
        results.Clear();
        lastScanIncludedStringReferences = false;

        try
        {
            foreach (string targetPath in targetImagePaths)
            {
                results.Add(CreateResult(targetPath));
            }

            PopulateSpriteAtlasStates(results);
            PopulateDependencyReferences(results);
            if (scanStringReferences)
            {
                ScanStringReferences(results);
            }

            lastScanIncludedStringReferences = scanStringReferences;
            selectionDescription =
                $"扫描完成，共检查 {results.Count} 张图片。" +
                $"字符串扫描：{(lastScanIncludedStringReferences ? "已执行" : "未执行")}。";
        }
        catch (OperationCanceledException)
        {
            selectionDescription = "扫描已取消。";
        }
        catch (Exception exception)
        {
            Debug.LogError($"[图片引用检查] 扫描失败：{exception.Message}");
            Debug.LogException(exception);
            selectionDescription = "扫描失败，详情请查看 Console。";
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Repaint();
        }
    }

    private static ImageReferenceResult CreateResult(string assetPath)
    {
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        var result = new ImageReferenceResult(assetPath, guid)
        {
            IsInResources = assetPath.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0
        };

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var entry = settings?.FindAssetEntry(guid);
        if (entry != null)
        {
            result.AddressableAddress = entry.address;
        }

        return result;
    }

    private static void PopulateSpriteAtlasStates(IReadOnlyList<ImageReferenceResult> imageResults)
    {
        string[] atlasGuids = AssetDatabase.FindAssets("t:SpriteAtlas", new[] { "Assets" });
        for (int atlasIndex = 0; atlasIndex < atlasGuids.Length; atlasIndex++)
        {
            string atlasPath = AssetDatabase.GUIDToAssetPath(atlasGuids[atlasIndex]);
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            if (atlas == null)
            {
                continue;
            }

            Object[] packables = SpriteAtlasExtensions.GetPackables(atlas);
            foreach (ImageReferenceResult result in imageResults)
            {
                if (packables.Any(packable => PackableContains(packable, result.AssetPath)))
                {
                    result.SpriteAtlasPaths.Add(atlasPath);
                }
            }
        }
    }

    private static bool PackableContains(Object packable, string imagePath)
    {
        string packablePath = AssetDatabase.GetAssetPath(packable);
        if (AssetDatabase.IsValidFolder(packablePath))
        {
            return imagePath.StartsWith(packablePath + "/", StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(packablePath, imagePath, StringComparison.OrdinalIgnoreCase);
    }

    private static void PopulateDependencyReferences(IReadOnlyList<ImageReferenceResult> imageResults)
    {
        Dictionary<string, List<string>> dependencyIndex = GetReverseDependencyIndex();
        foreach (ImageReferenceResult result in imageResults)
        {
            if (dependencyIndex.TryGetValue(result.AssetPath, out List<string> referencers))
            {
                result.SerializedReferences.AddRange(referencers);
            }
        }
    }

    private static Dictionary<string, List<string>> GetReverseDependencyIndex()
    {
        if (reverseDependencyIndex != null)
        {
            return reverseDependencyIndex;
        }

        HashSet<string> addressableConfigurationPaths = GetAddressableConfigurationPaths();
        string[] candidatePaths = AssetDatabase.GetAllAssetPaths()
            .Where(path => path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            .Where(path => !addressableConfigurationPaths.Contains(path))
            .Where(path => SerializedAssetExtensions.Contains(Path.GetExtension(path)))
            .ToArray();
        var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        for (int candidateIndex = 0; candidateIndex < candidatePaths.Length; candidateIndex++)
        {
            string candidatePath = candidatePaths[candidateIndex];
            if (EditorUtility.DisplayCancelableProgressBar(
                    "构建资源依赖索引",
                    candidatePath,
                    candidatePaths.Length == 0 ? 1f : (float)candidateIndex / candidatePaths.Length))
            {
                throw new OperationCanceledException("用户取消了扫描。");
            }

            string[] dependencies = AssetDatabase.GetDependencies(candidatePath, false);
            foreach (string dependencyPath in dependencies)
            {
                if (!index.TryGetValue(dependencyPath, out List<string> referencers))
                {
                    referencers = new List<string>();
                    index.Add(dependencyPath, referencers);
                }

                referencers.Add(candidatePath);
            }
        }

        foreach (List<string> referencers in index.Values)
        {
            referencers.Sort(StringComparer.OrdinalIgnoreCase);
        }

        // 只有完整构建成功后才发布缓存；取消扫描时不会留下不完整索引。
        reverseDependencyIndex = index;
        return reverseDependencyIndex;
    }

    private static void ScanStringReferences(IReadOnlyList<ImageReferenceResult> imageResults)
    {
        var searchTokens = imageResults.ToDictionary(
            result => result,
            CreateQuotedReferenceStrings);
        string[] candidatePaths = AssetDatabase.GetAllAssetPaths()
            .Where(path => path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            .Where(path => SearchableTextExtensions.Contains(Path.GetExtension(path)))
            .Where(path => File.Exists(Path.GetFullPath(path)))
            .ToArray();

        for (int candidateIndex = 0; candidateIndex < candidatePaths.Length; candidateIndex++)
        {
            string candidatePath = candidatePaths[candidateIndex];
            if (EditorUtility.DisplayCancelableProgressBar(
                    "扫描代码和配置字符串",
                    candidatePath,
                    candidatePaths.Length == 0 ? 1f : (float)candidateIndex / candidatePaths.Length))
            {
                throw new OperationCanceledException("用户取消了扫描。");
            }

            string content;
            try
            {
                content = File.ReadAllText(Path.GetFullPath(candidatePath));
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (ImageReferenceResult result in imageResults)
            {
                if (ContainsReferenceString(content, searchTokens[result]))
                {
                    result.StringReferences.Add(candidatePath);
                }
            }
        }
    }

    private static HashSet<string> GetAddressableConfigurationPaths()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            return paths;
        }

        AddAssetPath(paths, settings);
        foreach (AddressableAssetGroup group in settings.groups.Where(group => group != null))
        {
            AddAssetPath(paths, group);
            foreach (AddressableAssetGroupSchema schema in group.Schemas.Where(schema => schema != null))
            {
                AddAssetPath(paths, schema);
            }
        }

        return paths;
    }

    private static void AddAssetPath(ISet<string> paths, Object asset)
    {
        string path = AssetDatabase.GetAssetPath(asset);
        if (!string.IsNullOrEmpty(path))
        {
            paths.Add(path);
        }
    }

    private static string[] CreateQuotedReferenceStrings(ImageReferenceResult result)
    {
        string fileName = Path.GetFileName(result.AssetPath);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(result.AssetPath);
        return new[]
            {
                result.AssetPath,
                fileName,
                fileNameWithoutExtension,
                result.AddressableAddress
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(candidate => new[] { $"\"{candidate}\"", $"'{candidate}'" })
            .ToArray();
    }

    private static bool ContainsReferenceString(string content, IReadOnlyList<string> quotedCandidates)
    {
        // 只匹配被引号包裹的完整字符串，避免资源名恰好出现在类型名或注释中造成大量误报。
        return quotedCandidates.Any(candidate =>
            content.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private string BuildReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"图片引用检查：{results.Count} 张");

        foreach (ImageReferenceResult result in results)
        {
            string status = result.SerializedReferences.Count > 0
                ? "确定引用"
                : result.StringReferences.Count > 0
                    ? "仅疑似引用"
                    : lastScanIncludedStringReferences
                        ? "未发现引用"
                        : "未发现序列化引用";

            builder.AppendLine();
            builder.AppendLine($"[{status}] {result.AssetPath}");
            if (!string.IsNullOrEmpty(result.AddressableAddress))
            {
                builder.AppendLine($"  Addressable: {result.AddressableAddress}");
            }

            if (result.IsInResources)
            {
                builder.AppendLine("  Resources: 是");
            }

            foreach (string atlasPath in result.SpriteAtlasPaths)
            {
                builder.AppendLine($"  SpriteAtlas: {atlasPath}");
            }

            foreach (string reference in result.SerializedReferences)
            {
                builder.AppendLine($"  确定引用: {reference}");
            }

            foreach (string reference in result.StringReferences)
            {
                builder.AppendLine($"  疑似引用: {reference}");
            }
        }

        return builder.ToString();
    }

    private static bool IsImagePath(string assetPath)
    {
        return !string.IsNullOrEmpty(assetPath) &&
               assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
               ImageExtensions.Contains(Path.GetExtension(assetPath));
    }

    private static void PingAsset(string assetPath)
    {
        Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    private sealed class ImageReferenceResult
    {
        public string AssetPath { get; }
        public string Guid { get; }
        public bool IsInResources { get; set; }
        public string AddressableAddress { get; set; }
        public List<string> SpriteAtlasPaths { get; } = new();
        public List<string> SerializedReferences { get; } = new();
        public List<string> StringReferences { get; } = new();

        public ImageReferenceResult(string assetPath, string guid)
        {
            AssetPath = assetPath;
            Guid = guid;
        }
    }
}
