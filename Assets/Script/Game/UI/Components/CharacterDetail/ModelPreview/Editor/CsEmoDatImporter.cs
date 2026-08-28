using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class CsEmoDatImporter
{
    internal const string DefaultOutputFolder = "Assets/GameData/ModelView/FacePreset/Extracted";
    const string SourceNamePrefix = "Cs_Emo_Avatar_";
    const int CompositeMouthCurveCount = 32;
    const int CompositeUpperFaceCurveCount = 42;
    const int CompositeTailRecordSize = 92;

    readonly struct ChannelBinding
    {
        public readonly int ChannelId;
        public readonly string BlendShapeName;

        public ChannelBinding(int channelId, string blendShapeName)
        {
            ChannelId = channelId;
            BlendShapeName = blendShapeName;
        }
    }

    sealed class ParsedCurve
    {
        public int ChannelId;
        public int ControllerIndex;
        public bool BindByControllerIndex;
        public AnimationCurve Curve;
        public int PreInfinity;
        public int PostInfinity;
        public int RotationOrder;
    }

    sealed class ParsedDat
    {
        public string ExpressionName;
        public List<ParsedCurve> Curves;
    }

    enum CompositeMouthEncoding
    {
        StandardChannelId,
        ControllerIndexOnly
    }

    internal readonly struct ImportResult
    {
        public readonly int CreatedCount;
        public readonly int UpdatedCount;
        public readonly int FailedCount;

        public ImportResult(int createdCount, int updatedCount, int failedCount)
        {
            CreatedCount = createdCount;
            UpdatedCount = updatedCount;
            FailedCount = failedCount;
        }
    }

    static readonly Dictionary<int, ChannelBinding> KnownBindings = new()
    {
        { 0, new ChannelBinding(65, "Mouth_Default") },
        { 1, new ChannelBinding(0, "Mouth_A01") },
        { 2, new ChannelBinding(6, "Mouth_Open01") },
        { 3, new ChannelBinding(7, "Mouth_Smile01") },
        { 4, new ChannelBinding(16, "Mouth_Smile02") },
        { 5, new ChannelBinding(17, "Mouth_Angry01") },
        { 6, new ChannelBinding(19, "Mouth_Angry02") },
        { 7, new ChannelBinding(21, "Mouth_Angry03") },
        { 8, new ChannelBinding(12, "Mouth_Fury01") },
        { 9, new ChannelBinding(23, "Mouth_Doya01") },
        { 10, new ChannelBinding(24, "Mouth_Doya02") },
        { 11, new ChannelBinding(66, "Mouth_Neko01") },
        { 12, new ChannelBinding(22, "Mouth_Pero01") },
        { 13, new ChannelBinding(67, "Mouth_Pero02") },
        { 14, new ChannelBinding(68, "Mouth_Line01") },
        { 30, new ChannelBinding(18, "Mouth_Line02") },
        { 31, new ChannelBinding(84, "Mouth_BigTongue01") },
        { 44, new ChannelBinding(4028, "Eye_Default") },
        { 45, new ChannelBinding(4029, "Eye_WinkA_L") },
        { 46, new ChannelBinding(4085, "Eye_WinkA_R") },
        { 47, new ChannelBinding(4030, "Eye_WinkB_L") },
        { 48, new ChannelBinding(4031, "Eye_WinkB_R") },
        { 49, new ChannelBinding(4086, "Eye_WinkC_L") },
        { 50, new ChannelBinding(4087, "Eye_WinkC_R") },
        { 51, new ChannelBinding(4032, "Eye_Ha") },
        { 52, new ChannelBinding(4033, "Eye_Jito") },
        { 53, new ChannelBinding(4034, "Eye_Wail") },
        { 54, new ChannelBinding(4035, "Eye_Hostility") },
        { 55, new ChannelBinding(4036, "Eye_Tired") },
        { 56, new ChannelBinding(4088, "Eye_WUp") },
        { 57, new ChannelBinding(4089, "Eye_WDown") },
        { 58, new ChannelBinding(4090, "Eye_Lowereyelid") },
        { 59, new ChannelBinding(5091, "Brow_Default") },
        { 60, new ChannelBinding(5039, "Brow_Trouble_L") },
        { 61, new ChannelBinding(5040, "Brow_Trouble_R") },
        { 62, new ChannelBinding(5041, "Brow_Smily_L") },
        { 63, new ChannelBinding(5042, "Brow_Smily_R") },
        { 64, new ChannelBinding(5043, "Brow_Angry_L") },
        { 65, new ChannelBinding(5044, "Brow_Angry_R") },
        { 66, new ChannelBinding(5045, "Brow_Shy_L") },
        { 67, new ChannelBinding(5046, "Brow_Shy_R") },
        { 68, new ChannelBinding(5047, "Brow_Up_L") },
        { 69, new ChannelBinding(5048, "Brow_Up_R") },
        { 70, new ChannelBinding(5049, "Brow_Down_L") },
        { 71, new ChannelBinding(5050, "Brow_Down_R") },
        { 72, new ChannelBinding(5092, "Brow_Squeeze_L") },
        { 73, new ChannelBinding(5093, "Brow_Squeeze_R") }
    };

    [MenuItem("Tools/Character/Import Cs Emo DAT Folder")]
    static void SelectAndImportFolder()
    {
        CsEmoDatImporterWindow.ShowWindow();
    }

    [MenuItem("Tools/Character/Migrate Cs Emo Preset Names")]
    static void MigratePresetNames()
    {
        string[] presetGuids = AssetDatabase.FindAssets("t:FaceExpressionPreset", new[] { DefaultOutputFolder });
        int migratedCount = 0;
        int skippedCount = 0;
        int failedCount = 0;

        foreach (string guid in presetGuids)
        {
            string sourcePath = AssetDatabase.GUIDToAssetPath(guid);
            string sourceAssetName = Path.GetFileNameWithoutExtension(sourcePath);
            if (!sourceAssetName.StartsWith(SourceNamePrefix, StringComparison.Ordinal))
            {
                skippedCount++;
                continue;
            }

            string targetAssetName = GetPresetAssetName(sourceAssetName);
            string targetPath = $"{DefaultOutputFolder}/{targetAssetName}.asset";
            if (AssetDatabase.LoadMainAssetAtPath(targetPath) != null)
            {
                failedCount++;
                Debug.LogError($"表情预设重命名失败，目标资源已存在: {targetPath}");
                continue;
            }

            string error = AssetDatabase.MoveAsset(sourcePath, targetPath);
            if (string.IsNullOrEmpty(error))
            {
                migratedCount++;
            }
            else
            {
                failedCount++;
                Debug.LogError($"表情预设重命名失败: {sourcePath}\n{error}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Cs Emo 表情预设命名迁移完成。迁移 {migratedCount}，跳过 {skippedCount}，失败 {failedCount}。");
    }

    internal static ImportResult ImportFolder(string sourceFolder, string outputFolder)
    {
        string[] datFiles = Directory.GetFiles(sourceFolder, "*.dat", SearchOption.TopDirectoryOnly);
        if (datFiles.Length == 0)
        {
            Debug.LogWarning($"未在目录中找到 DAT 文件: {sourceFolder}");
            return new ImportResult(0, 0, 0);
        }

        EnsureOutputFolder(outputFolder);

        int createdCount = 0;
        int updatedCount = 0;
        int failedCount = 0;

        foreach (string datFile in datFiles.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                bool created = ImportFile(datFile, outputFolder);
                if (created)
                {
                    createdCount++;
                }
                else
                {
                    updatedCount++;
                }
            }
            catch (Exception exception)
            {
                failedCount++;
                Debug.LogError($"导入 Cs Emo DAT 失败: {datFile}\n{exception.Message}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Cs Emo DAT 导入完成。新建 {createdCount}，更新 {updatedCount}，失败 {failedCount}。");
        return new ImportResult(createdCount, updatedCount, failedCount);
    }

    static bool ImportFile(string datFile, string outputFolder)
    {
        ParsedDat parsed = ParseDat(datFile);
        string assetName = GetPresetAssetName(Path.GetFileNameWithoutExtension(datFile));
        string assetPath = $"{outputFolder}/{assetName}.asset";
        FaceExpressionPreset preset = AssetDatabase.LoadAssetAtPath<FaceExpressionPreset>(assetPath);
        bool created = preset == null;

        if (created)
        {
            preset = ScriptableObject.CreateInstance<FaceExpressionPreset>();
        }

        // 重导入只更新原始曲线，保留项目中人工确认过的绑定或忽略配置。
        Dictionary<(int channelId, int controllerIndex), FaceExpressionPreset.CurveData> previousBindings =
            preset.curves?.ToDictionary(curve => (curve.channelId, curve.controllerIndex))
            ?? new Dictionary<(int, int), FaceExpressionPreset.CurveData>();

        List<FaceExpressionPreset.CurveData> importedCurves = new(parsed.Curves.Count);
        int resolvedCount = 0;
        int unresolvedCount = 0;
        int ignoredCount = 0;

        foreach (ParsedCurve parsedCurve in parsed.Curves)
        {
            FaceExpressionPreset.CurveData importedCurve = CreateCurveData(parsedCurve, previousBindings);
            importedCurves.Add(importedCurve);

            switch (importedCurve.bindingType)
            {
                case FaceCurveBindingType.BlendShape:
                    resolvedCount++;
                    break;
                case FaceCurveBindingType.Ignored:
                    ignoredCount++;
                    break;
                default:
                    unresolvedCount++;
                    break;
            }
        }

        preset.playbackMode = FacePresetPlaybackMode.CurveAnimation;
        preset.expressionName = string.IsNullOrEmpty(parsed.ExpressionName)
            ? assetName.Replace(SourceNamePrefix, string.Empty)
            : parsed.ExpressionName;
        preset.regions = GetRegions(importedCurves);
        preset.duration = importedCurves.Count == 0
            ? 0f
            : importedCurves.Max(curve => curve.curve.length == 0 ? 0f : curve.curve.keys[^1].time);
        preset.containsBlink = HasCompleteBlinkCycle(importedCurves);
        preset.curves = importedCurves;

        if (created)
        {
            AssetDatabase.CreateAsset(preset, assetPath);
        }

        EditorUtility.SetDirty(preset);
        Debug.Log($"{assetName}: 已解析 {resolvedCount}，未解析 {unresolvedCount}，忽略 {ignoredCount}。");
        return created;
    }

    static string GetPresetAssetName(string sourceAssetName)
    {
        if (!sourceAssetName.StartsWith(SourceNamePrefix, StringComparison.Ordinal))
        {
            return SanitizeFileName(sourceAssetName);
        }

        string shortName = sourceAssetName[SourceNamePrefix.Length..];
        int numberStart = shortName.IndexOfAny("0123456789".ToCharArray());
        if (numberStart <= 0)
        {
            return SanitizeFileName($"Face_{shortName}");
        }

        int numberEnd = numberStart;
        while (numberEnd < shortName.Length && char.IsDigit(shortName[numberEnd]))
        {
            numberEnd++;
        }

        string category = shortName[..numberStart].TrimEnd('_');
        string number = shortName[numberStart..numberEnd];
        string variant = shortName[numberEnd..];
        return SanitizeFileName($"Face_{category}_{number}{variant}");
    }

    static FaceExpressionPreset.CurveData CreateCurveData(
        ParsedCurve parsedCurve,
        IReadOnlyDictionary<(int channelId, int controllerIndex), FaceExpressionPreset.CurveData> previousBindings)
    {
        FaceCurveBindingType bindingType = FaceCurveBindingType.Unresolved;
        string blendShapeName = string.Empty;

        if (previousBindings.TryGetValue((parsedCurve.ChannelId, parsedCurve.ControllerIndex), out var previous))
        {
            bindingType = previous.bindingType;
            blendShapeName = previous.blendShapeName;
        }
        else if (KnownBindings.TryGetValue(parsedCurve.ControllerIndex, out ChannelBinding binding)
                 && (binding.ChannelId == parsedCurve.ChannelId || parsedCurve.BindByControllerIndex))
        {
            bindingType = FaceCurveBindingType.BlendShape;
            blendShapeName = binding.BlendShapeName;
        }

        return new FaceExpressionPreset.CurveData
        {
            channelId = parsedCurve.ChannelId,
            controllerIndex = parsedCurve.ControllerIndex,
            bindingType = bindingType,
            blendShapeName = blendShapeName,
            curve = parsedCurve.Curve,
            preInfinity = parsedCurve.PreInfinity,
            postInfinity = parsedCurve.PostInfinity,
            rotationOrder = parsedCurve.RotationOrder
        };
    }

    static ParsedDat ParseDat(string datFile)
    {
        using FileStream stream = File.OpenRead(datFile);
        using BinaryReader reader = new(stream, Encoding.UTF8, false);

        ReadPPtr(reader);
        reader.ReadByte();
        AlignFourBytes(reader);
        ReadPPtr(reader);
        ReadAlignedString(reader);

        reader.ReadSingle();
        reader.ReadInt32();
        string expressionName = ReadAlignedString(reader);
        ReadPPtr(reader);

        int curveCount = ReadCurveCount(reader);
        List<ParsedCurve> curves = ReadCurves(reader, curveCount);

        if (reader.BaseStream.Position + sizeof(int) == reader.BaseStream.Length)
        {
            reader.ReadInt32();
        }

        if (reader.BaseStream.Position == reader.BaseStream.Length)
        {
            return new ParsedDat
            {
                ExpressionName = expressionName,
                Curves = curves
            };
        }

        CompositeMouthEncoding mouthEncoding = DetectCompositeMouthEncoding(curves);
        if (mouthEncoding == CompositeMouthEncoding.ControllerIndexOnly)
        {
            foreach (ParsedCurve curve in curves)
            {
                // 角色动作复合 DAT 的嘴部 ChannelId 均为 0，只能使用稳定的 ControllerIndex 顺序绑定。
                curve.BindByControllerIndex = true;
            }
        }

        int reserved = reader.ReadInt32();
        if (reserved != 0)
        {
            throw new InvalidDataException($"复合 DAT 的眼眉段前缀异常: {reserved}");
        }

        reader.ReadSingle();
        reader.ReadInt32();
        string upperFaceExpressionName = ReadAlignedString(reader);
        ReadPPtr(reader);

        int upperFaceCurveCount = ReadCurveCount(reader);
        List<ParsedCurve> upperFaceCurves = ReadCurves(reader, upperFaceCurveCount);
        ValidateCompositeUpperFaceCurves(upperFaceCurves);
        curves.AddRange(upperFaceCurves);
        ValidateCompositeTail(reader);

        return new ParsedDat
        {
            ExpressionName = string.IsNullOrEmpty(upperFaceExpressionName)
                ? expressionName
                : upperFaceExpressionName,
            Curves = curves
        };
    }

    static int ReadCurveCount(BinaryReader reader)
    {
        int curveCount = reader.ReadInt32();
        if (curveCount < 0 || curveCount > 512)
        {
            throw new InvalidDataException($"曲线数量异常: {curveCount}");
        }

        return curveCount;
    }

    static List<ParsedCurve> ReadCurves(BinaryReader reader, int curveCount)
    {
        List<ParsedCurve> curves = new(curveCount);
        for (int curveIndex = 0; curveIndex < curveCount; curveIndex++)
        {
            int channelId = reader.ReadInt32();
            int keyCount = reader.ReadInt32();
            if (keyCount < 0 || keyCount > 10000)
            {
                throw new InvalidDataException($"通道 {channelId} 的关键帧数量异常: {keyCount}");
            }

            Keyframe[] keys = new Keyframe[keyCount];
            for (int keyIndex = 0; keyIndex < keyCount; keyIndex++)
            {
                keys[keyIndex] = new Keyframe(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle());
            }

            curves.Add(new ParsedCurve
            {
                ChannelId = channelId,
                Curve = new AnimationCurve(keys),
                PreInfinity = reader.ReadInt32(),
                PostInfinity = reader.ReadInt32(),
                RotationOrder = reader.ReadInt32(),
                ControllerIndex = reader.ReadInt32()
            });
        }

        return curves;
    }

    static CompositeMouthEncoding DetectCompositeMouthEncoding(IReadOnlyList<ParsedCurve> curves)
    {
        if (curves.Count != CompositeMouthCurveCount
            || !HasControllerRange(curves, 0, 31))
        {
            throw new InvalidDataException("DAT 包含额外数据，但首段不符合复合嘴部曲线格式。");
        }

        if (curves.All(curve => curve.ChannelId == 0))
        {
            return CompositeMouthEncoding.ControllerIndexOnly;
        }

        if (HasStandardMouthChannels(curves))
        {
            return CompositeMouthEncoding.StandardChannelId;
        }

        throw new InvalidDataException("复合 DAT 的嘴部 ChannelId 既不是标准通道，也不是全零编码。");
    }

    static bool HasStandardMouthChannels(IEnumerable<ParsedCurve> curves)
    {
        foreach (ParsedCurve curve in curves)
        {
            if (!TryGetStandardMouthChannelId(curve.ControllerIndex, out int expectedChannelId)
                || curve.ChannelId != expectedChannelId)
            {
                return false;
            }
        }

        return true;
    }

    static bool TryGetStandardMouthChannelId(int controllerIndex, out int channelId)
    {
        if (KnownBindings.TryGetValue(controllerIndex, out ChannelBinding binding))
        {
            channelId = binding.ChannelId;
            return true;
        }

        if (controllerIndex is >= 15 and <= 29)
        {
            channelId = 3069 + controllerIndex - 15;
            return true;
        }

        channelId = default;
        return false;
    }

    static void ValidateCompositeUpperFaceCurves(IReadOnlyList<ParsedCurve> curves)
    {
        if (curves.Count != CompositeUpperFaceCurveCount || !HasControllerRange(curves, 32, 73))
        {
            throw new InvalidDataException("复合 DAT 的眼眉曲线格式异常。");
        }
    }

    static bool HasControllerRange(IReadOnlyList<ParsedCurve> curves, int first, int last)
    {
        return curves.Select(curve => curve.ControllerIndex)
            .OrderBy(index => index)
            .SequenceEqual(Enumerable.Range(first, last - first + 1));
    }

    static void ValidateCompositeTail(BinaryReader reader)
    {
        long remainingBytes = reader.BaseStream.Length - reader.BaseStream.Position;
        if (remainingBytes < sizeof(int) * 4)
        {
            throw new InvalidDataException($"复合 DAT 的附加数据长度异常: {remainingBytes}");
        }

        int reserved = reader.ReadInt32();
        int recordCount = reader.ReadInt32();
        if (reserved != 0 || recordCount < 0)
        {
            throw new InvalidDataException($"复合 DAT 的附加记录头异常: {reserved}, {recordCount}");
        }

        long expectedBytes = (long)recordCount * CompositeTailRecordSize + sizeof(int) * 2;
        long availableBytes = reader.BaseStream.Length - reader.BaseStream.Position;
        if (availableBytes != expectedBytes)
        {
            throw new InvalidDataException(
                $"复合 DAT 的附加记录长度异常: 记录 {recordCount}，剩余 {availableBytes} 字节。");
        }

        // 这部分记录的语义尚未确认；先严格验证格式并跳过，避免把未知参数误写入表情通道。
        reader.BaseStream.Position += (long)recordCount * CompositeTailRecordSize;
        reader.ReadInt32();
        reader.ReadInt32();
    }

    static void ReadPPtr(BinaryReader reader)
    {
        reader.ReadInt32();
        reader.ReadInt64();
    }

    static string ReadAlignedString(BinaryReader reader)
    {
        int byteCount = reader.ReadInt32();
        if (byteCount < 0 || byteCount > reader.BaseStream.Length - reader.BaseStream.Position)
        {
            throw new InvalidDataException($"字符串长度异常: {byteCount}");
        }

        string value = Encoding.UTF8.GetString(reader.ReadBytes(byteCount));
        AlignFourBytes(reader);
        return value;
    }

    static void AlignFourBytes(BinaryReader reader)
    {
        long alignedPosition = (reader.BaseStream.Position + 3L) & ~3L;
        reader.BaseStream.Position = alignedPosition;
    }

    static FaceRegion GetRegions(IEnumerable<FaceExpressionPreset.CurveData> curves)
    {
        FaceRegion regions = FaceRegion.None;
        foreach (FaceExpressionPreset.CurveData curve in curves)
        {
            regions |= curve.controllerIndex <= 31 ? FaceRegion.Mouth : FaceRegion.EyesAndBrows;
        }

        return regions;
    }

    static bool HasCompleteBlinkCycle(IEnumerable<FaceExpressionPreset.CurveData> curves)
    {
        FaceExpressionPreset.CurveData leftCurve = curves.FirstOrDefault(curve => curve.controllerIndex == 47);
        FaceExpressionPreset.CurveData rightCurve = curves.FirstOrDefault(curve => curve.controllerIndex == 48);
        return HasCloseAndReopen(leftCurve) && HasCloseAndReopen(rightCurve);
    }

    static bool HasCloseAndReopen(FaceExpressionPreset.CurveData curve)
    {
        if (curve?.curve == null)
        {
            return false;
        }

        bool reachedClosedState = false;
        foreach (Keyframe key in curve.curve.keys)
        {
            if (key.value >= 99f)
            {
                reachedClosedState = true;
            }
            else if (reachedClosedState && key.value <= 5f)
            {
                return true;
            }
        }

        return false;
    }

    static void EnsureOutputFolder(string outputFolder)
    {
        string currentPath = "Assets";
        foreach (string folderName in outputFolder.Split('/').Skip(1))
        {
            string nextPath = $"{currentPath}/{folderName}";
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, folderName);
            }

            currentPath = nextPath;
        }
    }

    static string SanitizeFileName(string fileName)
    {
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidCharacter, '_');
        }

        return fileName;
    }
}

sealed class CsEmoDatImporterWindow : EditorWindow
{
    const string SourceFolderPreferenceKey = "CsEmoDatImporter.SourceFolder";
    const string OutputFolderPreferenceKey = "CsEmoDatImporter.OutputFolder";

    string sourceFolder;
    DefaultAsset outputFolder;
    string importResult;

    internal static void ShowWindow()
    {
        CsEmoDatImporterWindow window = GetWindow<CsEmoDatImporterWindow>();
        window.titleContent = new GUIContent("Cs Emo DAT 导入");
        window.minSize = new Vector2(520f, 190f);
        window.Show();
    }

    void OnEnable()
    {
        sourceFolder = EditorPrefs.GetString(SourceFolderPreferenceKey, string.Empty);
        string outputPath = EditorPrefs.GetString(
            OutputFolderPreferenceKey,
            CsEmoDatImporter.DefaultOutputFolder);
        outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(outputPath);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Cs Emo DAT 导入", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();
            sourceFolder = EditorGUILayout.TextField("DAT 来源文件夹", sourceFolder);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(SourceFolderPreferenceKey, sourceFolder);
            }

            if (GUILayout.Button("选择", GUILayout.Width(60f)))
            {
                SelectSourceFolder();
            }
        }

        EditorGUI.BeginChangeCheck();
        outputFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "Preset 输出文件夹",
            outputFolder,
            typeof(DefaultAsset),
            false);
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetString(OutputFolderPreferenceKey, GetOutputPath());
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("格式", "自动识别");
        }

        bool sourceIsValid = Directory.Exists(sourceFolder);
        string outputPath = GetOutputPath();
        bool outputIsValid = AssetDatabase.IsValidFolder(outputPath);

        if (!string.IsNullOrEmpty(sourceFolder) && !sourceIsValid)
        {
            EditorGUILayout.HelpBox("DAT 来源文件夹不存在。", MessageType.Warning);
        }

        if (outputFolder != null && !outputIsValid)
        {
            EditorGUILayout.HelpBox("Preset 输出位置必须是 Assets 内的文件夹。", MessageType.Warning);
        }

        using (new EditorGUI.DisabledScope(!sourceIsValid || !outputIsValid))
        {
            if (GUILayout.Button("开始导入"))
            {
                Import(sourceFolder, outputPath);
            }
        }

        if (!string.IsNullOrEmpty(importResult))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(importResult, MessageType.Info);
        }
    }

    void SelectSourceFolder()
    {
        string selectedFolder = EditorUtility.OpenFolderPanel(
            "选择 Cs Emo DAT 文件夹",
            sourceFolder,
            string.Empty);
        if (string.IsNullOrEmpty(selectedFolder))
        {
            return;
        }

        sourceFolder = selectedFolder;
        EditorPrefs.SetString(SourceFolderPreferenceKey, sourceFolder);
    }

    string GetOutputPath()
    {
        return outputFolder == null ? string.Empty : AssetDatabase.GetAssetPath(outputFolder);
    }

    void Import(string sourcePath, string outputPath)
    {
        CsEmoDatImporter.ImportResult result = CsEmoDatImporter.ImportFolder(sourcePath, outputPath);
        importResult = $"新建 {result.CreatedCount}，更新 {result.UpdatedCount}，失败 {result.FailedCount}。";
    }
}
