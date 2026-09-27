using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HoyoToon;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

internal static class CharacterPresentationAssetPipeline
{
    const string ManifestPath = "Tools/PresentationAssetManifest.json";
    const string ExtractionScriptPath = "Tools/ExtractPresentationAssets.ps1";
    const int ExtractionLogLineLimit = 30;
    const string BatchActiveSessionKey = "CharacterPresentationAssetPipeline.BatchActive";
    const string BatchIdsSessionKey = "CharacterPresentationAssetPipeline.BatchIds";
    const string BatchAssetMapSessionKey = "CharacterPresentationAssetPipeline.BatchAssetMap";
    const string BatchReportsSessionKey = "CharacterPresentationAssetPipeline.BatchReports";

    static readonly object ExtractionLogLock = new();
    static readonly Queue<string> ExtractionLog = new();
    static Process extractionProcess;
    static Action<bool, string> extractionCompleted;
    static string extractionCharacterId;
    static bool extractionForceRebuild;
    static Queue<string> batchCharacterIds;
    static List<string> batchReports;
    static string batchAssetMap;

    internal static bool IsCompleteFlowRunning => extractionProcess != null || batchCharacterIds != null;

    [InitializeOnLoadMethod]
    static void ResumeBatchAfterDomainReload()
    {
        if (!SessionState.GetBool(BatchActiveSessionKey, false)) return;

        string[] ids = SessionState.GetString(BatchIdsSessionKey, string.Empty)
            .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (ids.Length == 0)
        {
            ClearBatchSession();
            return;
        }

        batchCharacterIds = new Queue<string>(ids);
        batchAssetMap = SessionState.GetString(BatchAssetMapSessionKey, string.Empty);
        batchReports = SessionState.GetString(BatchReportsSessionKey, string.Empty)
            .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        WaitForEditorThenContinueBatch();
    }

    [Serializable]
    sealed class Manifest
    {
        public int schemaVersion;
        public AnimeStudioSettings animeStudio;
        public ProjectSettings project;
        public CharacterSettings[] characters;
    }

    [Serializable]
    sealed class AnimeStudioSettings
    {
        public string game;
        public string primaryCli;
        public string fallbackCli;
        public string assetMap;
    }

    [Serializable]
    sealed class ProjectSettings
    {
        public string animationExtractedRoot;
        public string animationRuntimeRoot;
        public string presentationStagingRoot;
        public string facePresetRoot;
        public string presentationProfileRoot;
        public string previewDefinitionRoot;
        public string modelPreviewDatabase;
        public string defaultCameraPreset;
    }

    [Serializable]
    sealed class CharacterSettings
    {
        public string id;
        public string slug;
        public string bodyType;
        public string weaponType;
        public string teamConfig;
        public string targetKey;
        public string modelAddress;
        public string sourceModel;
        public string runtimeClipSuffix;
        public string cameraPreset;
        public string[] specialIdle;
    }

    sealed class AnimationJob
    {
        public string SourcePath;
        public string OutputFolder;
        public string ExpectedOutputPath;
        public bool ExcludeUpperArmTwist;
    }

    sealed class FaceGroup
    {
        public string SourceFolder;
        public string OutputFolder;
        public readonly List<string> RequiredDatNames = new();
    }

    [MenuItem("Tools/MyUISystem/角色展示/自动化/已有资源/处理单个角色", false, 300)]
    static void PrepareAndGenerate()
    {
        CharacterPresentationPipelineWindow.Open();
    }

    [MenuItem("Tools/MyUISystem/角色展示/自动化/已有资源/强制重建全部运行时资源", false, 301)]
    static void RebuildAll()
    {
        PrepareAndGenerate(null, true, true, out _);
    }

    [MenuItem("Tools/MyUISystem/角色展示/自动化/完整接入/接入全部已登记模型", false, 302)]
    static void CompleteAllModels()
    {
        if (IsCompleteFlowRunning)
        {
            Debug.LogError("已有角色完整接入流程正在运行。");
            return;
        }

        var errors = new List<string>();
        Manifest manifest = LoadManifest(errors);
        if (manifest == null)
        {
            LogFailure(errors);
            return;
        }
        if (string.IsNullOrWhiteSpace(manifest.animeStudio.assetMap) ||
            !File.Exists(manifest.animeStudio.assetMap))
        {
            Debug.LogError("Manifest 中 animeStudio.assetMap 不是有效文件。");
            return;
        }

        var ready = new Queue<string>();
        var reports = new List<string>();
        foreach (CharacterSettings character in manifest.characters.Where(character =>
                     character != null && !string.IsNullOrWhiteSpace(character.sourceModel)))
        {
            try
            {
                string sourcePath = NormalizeAssetPath(character.sourceModel);
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                if (source == null)
                    throw new InvalidOperationException("找不到源模型：" + sourcePath);

                GameObject standardized = PrepareModelForPresentation(
                    source,
                    out string modelReport);
                string outputPath = Path.ChangeExtension(sourcePath, null) + "_Presentation.prefab";
                string prefabReport = CharacterPresentationPrefabStandardizer.Generate(standardized, outputPath);
                string addressableReport = EnsureModelAddressable(outputPath, character.modelAddress, true);
                reports.Add($"[{character.id}] 模型准备完成\n{modelReport}\n{prefabReport}\n{addressableReport}");
                ready.Enqueue(character.id);
            }
            catch (Exception exception)
            {
                reports.Add($"[{character.id}] 模型准备失败\n{exception.Message}");
            }
        }

        if (ready.Count == 0)
        {
            Debug.LogError("没有可继续接入的角色。\n" + string.Join("\n\n", reports));
            return;
        }

        batchCharacterIds = ready;
        batchReports = reports;
        batchAssetMap = manifest.animeStudio.assetMap;
        SaveBatchSession();
        StartNextBatchCharacter();
    }

    internal static string InspectHoyoToonSetup(GameObject source, out bool needsMaterials, out bool needsRig)
    {
        needsMaterials = false;
        needsRig = false;
        if (source == null)
            throw new InvalidOperationException("请选择待接入的模型。");
        string path = AssetDatabase.GetAssetPath(source);
        if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null || !path.StartsWith("Assets/", StringComparison.Ordinal))
            throw new InvalidOperationException("HoyoToon 自动配置仅支持项目 Assets 内的 FBX。");

        // 标准化副本已有专用骨架描述；HoyoToon 只处理源 FBX，不能重新覆盖副本的姿态。
        if (ModelRootStandardizerPostprocessor.ReadSettings(importer) != null)
            return string.Empty;
        Animator[] animators = source.GetComponentsInChildren<Animator>(true);
        needsRig = animators.Length != 1 || animators[0].avatar == null ||
            !animators[0].avatar.isValid || !animators[0].avatar.isHuman;
        needsMaterials = !source.GetComponentsInChildren<Renderer>(true)
            .SelectMany(renderer => renderer.sharedMaterials)
            .Any(material => material != null && material.shader != null &&
                material.shader.name.StartsWith("HoyoToon/", StringComparison.Ordinal));
        if (!needsMaterials && !needsRig)
            return "HoyoToon：已有 Humanoid Avatar 和 HoyoToon 材质，跳过 Setup。";

        string directory = Path.GetDirectoryName(path);
        string materialsFolder = new[] { "Materials", "Material", "Mat" }
            .Select(folder => Path.Combine(directory, folder))
            .FirstOrDefault(folder => Directory.Exists(folder) && Directory.GetFileSystemEntries(folder).Any());
        if (materialsFolder == null || Directory.GetFiles(materialsFolder, "*.json").Length == 0)
            throw new InvalidOperationException("HoyoToon Setup 缺少材质 JSON：" + directory);
        return "HoyoToon 待处理：" + (needsMaterials ? "生成材质/配置贴图；" : "") +
            (needsRig ? "配置 FBX/Humanoid；" : "") +
            "完成并验证后继续 ModelRoot 标准化。";
    }

    internal static GameObject PrepareModelForPresentation(GameObject source, out string report)
    {
        string setupReport = InspectHoyoToonSetup(source, out bool needsMaterials, out bool needsRig);
        if (needsMaterials || needsRig)
        {
            string path = AssetDatabase.GetAssetPath(source);
            var previousSelection = Selection.objects;
            var previousActive = Selection.activeObject;
            try
            {
                // 两个 HoyoToon API 内部仍读取 Selection；必须限定到本次模型并在结束后恢复。
                Selection.objects = new UnityEngine.Object[] { source };
                if (needsMaterials)
                    HoyoToonMaterialManager.GenerateMaterialsFromJson();
                source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Selection.objects = new UnityEngine.Object[] { source };
                if (needsRig)
                {
                    bool completed = false;
                    string message = string.Empty;
                    // 当前插件回调在 SetFBXImportSettings 返回后同步执行。
                    HoyoToonMeshManager.SetupFBXFromUI(source, (success, detail) =>
                    {
                        completed = success;
                        message = detail;
                    });
                    if (!completed)
                        throw new InvalidOperationException("HoyoToon FBX Setup 失败：" + message);
                }
                else
                {
                    // 仅补材质时只重新绑定，保留用户已经校正的 Humanoid 姿态。
                    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                    importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName,
                        ModelImporterMaterialSearch.Local);
                    importer.SaveAndReimport();
                }

                // 重导入会替换模型对象；重新加载并检查实际产物，不能只相信插件成功回调。
                source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                InspectHoyoToonSetup(source, out bool missingMaterials, out bool missingRig);
                if (missingMaterials || missingRig)
                    throw new InvalidOperationException("HoyoToon Setup 后验证失败：" +
                        (missingRig ? "缺少有效 Animator/Humanoid Avatar；" : "") +
                        (missingMaterials ? "未绑定 HoyoToon 材质；" : "") + path);
                setupReport = "HoyoToon：已执行所需 Setup，Animator/Humanoid Avatar 与材质绑定检查通过。";
            }
            finally
            {
                Selection.objects = previousSelection;
                Selection.activeObject = previousActive;
            }
        }
        GameObject standardized = ModelRootStandardizerWindow.GetOrCreateStandardizedModel(source, out string modelReport);
        report = string.IsNullOrEmpty(setupReport) ? modelReport : setupReport + "\n" + modelReport;
        return standardized;
    }

    static void StartNextBatchCharacter()
    {
        if (batchCharacterIds == null) return;
        if (batchCharacterIds.Count == 0)
        {
            EditorApplication.update -= ContinueBatchWhenEditorReady;
            string report = string.Join("\n\n", batchReports);
            batchCharacterIds = null;
            batchReports = null;
            batchAssetMap = null;
            ClearBatchSession();
            Debug.Log("角色批量完整接入结束。\n" + report);
            return;
        }

        string characterId = batchCharacterIds.Peek();
        try
        {
            StartCompleteCharacter(characterId, batchAssetMap, false, false, (succeeded, report) =>
            {
                batchCharacterIds.Dequeue();
                batchReports.Add($"[{characterId}] 资源流程{(succeeded ? "完成" : "失败")}\n{report}");
                SaveBatchSession();
                WaitForEditorThenContinueBatch();
            });
        }
        catch (Exception exception)
        {
            batchCharacterIds.Dequeue();
            batchReports.Add($"[{characterId}] 资源流程启动失败\n{exception.Message}");
            SaveBatchSession();
            WaitForEditorThenContinueBatch();
        }
    }

    static void SaveBatchSession()
    {
        SessionState.SetBool(BatchActiveSessionKey, true);
        SessionState.SetString(BatchIdsSessionKey, string.Join("\n", batchCharacterIds));
        SessionState.SetString(BatchAssetMapSessionKey, batchAssetMap ?? string.Empty);
        SessionState.SetString(BatchReportsSessionKey, string.Join("\n\n", batchReports));
    }

    static void ClearBatchSession()
    {
        SessionState.EraseBool(BatchActiveSessionKey);
        SessionState.EraseString(BatchIdsSessionKey);
        SessionState.EraseString(BatchAssetMapSessionKey);
        SessionState.EraseString(BatchReportsSessionKey);
    }

    static void WaitForEditorThenContinueBatch()
    {
        EditorApplication.update -= ContinueBatchWhenEditorReady;
        EditorApplication.update += ContinueBatchWhenEditorReady;
    }

    static void ContinueBatchWhenEditorReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EditorApplication.update -= ContinueBatchWhenEditorReady;
        StartNextBatchCharacter();
    }

    internal static string[] GetCharacterIds()
    {
        var errors = new List<string>();
        Manifest manifest = LoadManifest(errors);
        if (manifest == null)
        {
            LogFailure(errors);
            return Array.Empty<string>();
        }

        return manifest.characters
            .Where(character => character != null && !string.IsNullOrWhiteSpace(character.id))
            .Select(character => character.id)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
    }

    internal static void PrepareCharacter(string characterId, bool forceRebuild)
    {
        PrepareAndGenerate(characterId, forceRebuild, false, out _);
    }

    internal static void StartCompleteCharacter(
        string characterId,
        string mapName,
        bool useFallbackCli,
        bool forceRebuild,
        Action<bool, string> completed)
    {
        if (extractionProcess != null)
            throw new InvalidOperationException($"角色 {extractionCharacterId} 的完整接入流程仍在运行。");
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("PlayMode 或脚本编译期间不能启动角色接入流程。");
        if (string.IsNullOrWhiteSpace(characterId))
            throw new InvalidOperationException("角色 ID 不能为空。");
        if (string.IsNullOrWhiteSpace(mapName) || !File.Exists(mapName))
            throw new InvalidOperationException("请选择有效的 AssetMap 文件。");

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot))
            throw new InvalidOperationException("无法取得 Unity 项目根目录。");
        string scriptPath = Path.Combine(
            projectRoot,
            ExtractionScriptPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(scriptPath))
            throw new InvalidOperationException("找不到资源提取入口：" + scriptPath);

        string powershellPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        if (!File.Exists(powershellPath))
            throw new InvalidOperationException("找不到 Windows PowerShell：" + powershellPath);

        string manifestFilePath = Path.Combine(
            projectRoot,
            ManifestPath.Replace('/', Path.DirectorySeparatorChar));
        string command =
            "[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false); " +
            "$OutputEncoding = [Console]::OutputEncoding; " +
            "$source = [IO.File]::ReadAllText(" + QuotePowerShellLiteral(scriptPath) +
            ", [Text.Encoding]::UTF8); " +
            "try { & ([ScriptBlock]::Create($source))" +
            " -ManifestPath " + QuotePowerShellLiteral(manifestFilePath) +
            " -Mode 'All'" +
            " -CharacterId " + QuotePowerShellLiteral(characterId.Trim()) +
            " -MapName " + QuotePowerShellLiteral(Path.GetFullPath(mapName)) +
            " -Resume" +
            (useFallbackCli ? " -UseFallbackCli" : string.Empty) +
            " 6>&1 } catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }";
        string encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));

        lock (ExtractionLogLock)
        {
            ExtractionLog.Clear();
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = powershellPath,
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encodedCommand,
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };
        process.OutputDataReceived += (_, eventArgs) => AppendExtractionLog(eventArgs.Data);
        process.ErrorDataReceived += (_, eventArgs) => AppendExtractionLog(eventArgs.Data);

        extractionProcess = process;
        extractionCompleted = completed;
        extractionCharacterId = characterId.Trim();
        extractionForceRebuild = forceRebuild;
        try
        {
            if (!process.Start())
                throw new InvalidOperationException("Windows PowerShell 未能启动。");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            EditorApplication.update += PollExtractionProcess;
            Debug.Log($"角色 {extractionCharacterId} 的缺失资源提取已启动。");
        }
        catch
        {
            ResetExtractionProcess();
            throw;
        }
    }

    internal static string RegisterGeneratedModel(
        string prefabPath,
        string characterId,
        string slug,
        string bodyType,
        string weaponType,
        string teamConfig,
        string targetKey,
        string modelAddress,
        IReadOnlyList<string> specialIdle)
    {
        var errors = new List<string>();
        Manifest manifest = LoadManifest(errors);
        if (manifest == null)
            throw new InvalidOperationException(string.Join("\n", errors));

        prefabPath = NormalizeAssetPath(prefabPath);
        characterId = characterId?.Trim();
        slug = slug?.Trim();
        bodyType = bodyType?.Trim();
        weaponType = weaponType?.Trim();
        teamConfig = teamConfig?.Trim();
        targetKey = targetKey?.Trim();
        modelAddress = modelAddress?.Trim();
        ValidateRegistration(prefabPath, characterId, slug, bodyType, weaponType, targetKey, modelAddress);

        CharacterSettings existing = manifest.characters.FirstOrDefault(character =>
            character != null && string.Equals(character.id, characterId, StringComparison.Ordinal));
        foreach (CharacterSettings character in manifest.characters.Where(character => character != null))
        {
            if (character == existing) continue;
            string currentTargetKey = string.IsNullOrWhiteSpace(character.targetKey)
                ? character.slug
                : character.targetKey;
            if (string.Equals(currentTargetKey, targetKey, StringComparison.Ordinal))
                throw new InvalidOperationException($"Manifest 中 targetKey 已被 {character.id} 使用：{targetKey}");
            if (string.Equals(character.modelAddress, modelAddress, StringComparison.Ordinal))
                throw new InvalidOperationException($"Manifest 中 modelAddress 已被 {character.id} 使用：{modelAddress}");
        }

        bool added = existing == null;
        if (added)
        {
            existing = new CharacterSettings
            {
                id = characterId,
                slug = slug,
                bodyType = bodyType,
                weaponType = weaponType,
                teamConfig = teamConfig,
                targetKey = targetKey,
                modelAddress = modelAddress,
                runtimeClipSuffix = "_ModelRoot_NoArmTwist",
                specialIdle = specialIdle == null
                    ? Array.Empty<string>()
                    : specialIdle.Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value.Trim())
                        .Distinct(StringComparer.Ordinal)
                        .ToArray()
            };
            manifest.characters = manifest.characters.Append(existing).ToArray();
        }
        else
        {
            // 已有角色的动画选择可能经过人工确认；模型接入只验证身份，不覆盖这些语义配置。
            if (!string.Equals(existing.slug, slug, StringComparison.Ordinal) ||
                !string.Equals(existing.bodyType, bodyType, StringComparison.Ordinal) ||
                !string.Equals(existing.weaponType, weaponType, StringComparison.Ordinal) ||
                !string.Equals(existing.targetKey, targetKey, StringComparison.Ordinal) ||
                !string.Equals(existing.modelAddress, modelAddress, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Manifest 已存在角色 {characterId}，但模型推断结果与现有配置不一致。请先确认窗口字段或手工调整 Manifest。");
            }
        }

        string addressableResult = EnsureModelAddressable(prefabPath, modelAddress);
        SaveManifest(manifest);
        return $"Manifest：{(added ? "已新增" : "已有配置保持不变")} {characterId}\n" +
            $"Addressable：{addressableResult}\n" +
            "模型登记已完成，正在等待资源提取与 Profile 生成。";
    }

    static bool PrepareAndGenerate(
        string characterId,
        bool forceRebuild,
        bool allCharacters,
        out string result)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        Manifest manifest = LoadManifest(errors);
        if (manifest == null)
        {
            result = string.Join("\n", errors);
            LogFailure(errors);
            return false;
        }

        List<CharacterSettings> characters = SelectCharacters(manifest, characterId, allCharacters, errors);
        List<AnimationJob> animationJobs = BuildAnimationJobs(manifest, characters, errors);
        List<FaceGroup> faceGroups = BuildMissingFaceGroups(manifest, characters, errors, warnings);
        FilterUnavailableInputs(animationJobs, faceGroups, warnings);
        if (errors.Count > 0)
        {
            result = string.Join("\n", errors);
            LogFailure(errors);
            return false;
        }

        List<AnimationJob> pendingAnimationJobs = forceRebuild
            ? animationJobs
            : animationJobs.Where(NeedsConversion).ToList();

        try
        {
            foreach (AnimationJob job in pendingAnimationJobs)
            {
                EnsureAssetFolder(job.OutputFolder);
                AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(job.SourcePath);
                string output = AnimationBindingConverterWindow.ConvertClip(
                    source,
                    job.OutputFolder,
                    job.ExcludeUpperArmTwist);
                if (!string.Equals(output, job.ExpectedOutputPath, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"动画输出路径与 Manifest 约定不一致：{output}，预期 {job.ExpectedOutputPath}");
                }
            }

            foreach (FaceGroup group in faceGroups)
            {
                EnsureAssetFolder(group.OutputFolder);
                CsEmoDatImporter.ImportResult importResult = CsEmoDatImporter.ImportFolder(
                    group.SourceFolder,
                    group.OutputFolder);
                if (importResult.FailedCount > 0)
                {
                    warnings.Add(
                        $"表情 DAT 部分导入失败：{group.SourceFolder}，失败 {importResult.FailedCount} 个；对应 Profile 字段将保持为空。");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (allCharacters)
                CharacterPresentationProfileGenerator.GenerateProfiles();
            else
                CharacterPresentationProfileGenerator.GenerateProfile(characterId);
            result =
                $"角色展示资源流水线完成。角色 {(allCharacters ? "全部" : characterId)}，" +
                $"转换动画 {pendingAnimationJobs.Count} 个，" +
                $"跳过未变化动画 {animationJobs.Count - pendingAnimationJobs.Count} 个，" +
                $"导入表情分组 {faceGroups.Count} 个，缺失或失败资源 {warnings.Count} 项。";
            if (warnings.Count > 0)
            {
                result += "\n" + string.Join("\n", warnings.Select(warning => "[MISSING] " + warning));
                Debug.LogWarning(result);
            }
            else
            {
                Debug.Log(result);
            }
            return true;
        }
        catch (Exception exception)
        {
            result = exception.ToString();
            Debug.LogError("角色展示资源流水线失败：\n" + result);
            return false;
        }
    }

    static void AppendExtractionLog(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        lock (ExtractionLogLock)
        {
            ExtractionLog.Enqueue(line);
            while (ExtractionLog.Count > ExtractionLogLineLimit)
                ExtractionLog.Dequeue();
        }
    }

    static void PollExtractionProcess()
    {
        Process process = extractionProcess;
        if (process == null || !process.HasExited) return;

        EditorApplication.update -= PollExtractionProcess;
        process.WaitForExit();
        int exitCode = process.ExitCode;
        string characterId = extractionCharacterId;
        bool forceRebuild = extractionForceRebuild;
        Action<bool, string> completed = extractionCompleted;
        string extractionReport;
        lock (ExtractionLogLock)
        {
            extractionReport = string.Join("\n", ExtractionLog);
        }
        ResetExtractionProcess();

        if (exitCode != 0)
        {
            string failure = $"角色 {characterId} 的缺失资源提取失败，退出码 {exitCode}。";
            if (!string.IsNullOrWhiteSpace(extractionReport))
                failure += "\n" + extractionReport;
            Debug.LogError(failure);
            completed?.Invoke(false, failure);
            return;
        }

        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            bool succeeded = PrepareAndGenerate(characterId, forceRebuild, false, out string prepareReport);
            string report = $"角色 {characterId} 的 CLI 提取、审计与同步已完成。\n{prepareReport}";
            if (!string.IsNullOrWhiteSpace(extractionReport))
                report += "\n\nPowerShell 输出末尾：\n" + extractionReport;
            completed?.Invoke(succeeded, report);
        }
        catch (Exception exception)
        {
            string failure = $"角色 {characterId} 的 Unity 资源生成失败：\n{exception}";
            Debug.LogError(failure);
            completed?.Invoke(false, failure);
        }
    }

    static void ResetExtractionProcess()
    {
        EditorApplication.update -= PollExtractionProcess;
        extractionProcess?.Dispose();
        extractionProcess = null;
        extractionCompleted = null;
        extractionCharacterId = null;
        extractionForceRebuild = false;
    }

    static string QuotePowerShellLiteral(string value)
    {
        return "'" + (value ?? string.Empty).Replace("'", "''") + "'";
    }

    static List<CharacterSettings> SelectCharacters(
        Manifest manifest,
        string characterId,
        bool allCharacters,
        List<string> errors)
    {
        if (allCharacters)
            return manifest.characters.Where(character => character != null).ToList();

        if (string.IsNullOrWhiteSpace(characterId))
        {
            errors.Add("必须指定一个角色。");
            return new List<CharacterSettings>();
        }

        List<CharacterSettings> matches = manifest.characters
            .Where(character => character != null &&
                string.Equals(character.id, characterId, StringComparison.Ordinal))
            .ToList();
        if (matches.Count != 1)
            errors.Add($"角色 ID 必须唯一匹配一个角色：{characterId}，当前匹配数 {matches.Count}。");
        return matches;
    }

    static bool NeedsConversion(AnimationJob job)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(job.ExpectedOutputPath) == null)
            return true;

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot))
            return true;

        string sourcePath = Path.Combine(projectRoot, job.SourcePath.Replace('/', Path.DirectorySeparatorChar));
        string outputPath = Path.Combine(projectRoot, job.ExpectedOutputPath.Replace('/', Path.DirectorySeparatorChar));
        return !File.Exists(outputPath) || File.GetLastWriteTimeUtc(sourcePath) > File.GetLastWriteTimeUtc(outputPath);
    }

    static Manifest LoadManifest(List<string> errors)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        string fullPath = string.IsNullOrEmpty(projectRoot)
            ? string.Empty
            : Path.Combine(projectRoot, ManifestPath.Replace('/', Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
        {
            errors.Add($"找不到 Manifest：{ManifestPath}");
            return null;
        }

        try
        {
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(fullPath));
            if (manifest == null || manifest.schemaVersion != 1)
            {
                errors.Add("PresentationAssetManifest 的 schemaVersion 必须为 1。");
                return null;
            }
            if (manifest.project == null)
            {
                errors.Add("Manifest 缺少 project 配置。");
                return null;
            }
            if (manifest.characters == null || manifest.characters.Length == 0)
            {
                errors.Add("Manifest 没有配置任何角色。");
                return null;
            }

            return manifest;
        }
        catch (Exception exception)
        {
            errors.Add("读取 Manifest 失败：" + exception.Message);
            return null;
        }
    }

    static void ValidateRegistration(
        string prefabPath,
        string characterId,
        string slug,
        string bodyType,
        string weaponType,
        string targetKey,
        string modelAddress)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null ||
            !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("角色展示资源必须是 Assets 内已经生成的 Prefab：" + prefabPath);
        if (string.IsNullOrWhiteSpace(characterId))
            throw new InvalidOperationException("角色 ID 不能为空。");
        if (string.IsNullOrWhiteSpace(slug))
            throw new InvalidOperationException("资源短名不能为空。");
        if (string.IsNullOrWhiteSpace(bodyType))
            throw new InvalidOperationException("体型不能为空。");
        if (string.IsNullOrWhiteSpace(weaponType))
            throw new InvalidOperationException("武器类型不能为空。");
        if (string.IsNullOrWhiteSpace(targetKey))
            throw new InvalidOperationException("Target Key 不能为空。");
        if (string.IsNullOrWhiteSpace(modelAddress))
            throw new InvalidOperationException("Model Address 不能为空。");
    }

    static string EnsureModelAddressable(
        string prefabPath,
        string modelAddress,
        bool replaceExistingAddress = false)
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
            throw new InvalidOperationException("项目中找不到 Addressables Settings。");
        if (settings.DefaultGroup == null)
            throw new InvalidOperationException("Addressables 没有可用的 Default Group。");

        string guid = AssetDatabase.AssetPathToGUID(prefabPath);
        if (string.IsNullOrEmpty(guid))
            throw new InvalidOperationException("无法取得展示 Prefab 的 GUID：" + prefabPath);

        AddressableAssetEntry conflictingEntry = settings.groups
            .Where(group => group != null)
            .SelectMany(group => group.entries)
            .FirstOrDefault(entry => entry.guid != guid &&
                string.Equals(entry.address, modelAddress, StringComparison.Ordinal));
        string replacedPath = string.Empty;
        if (conflictingEntry != null)
        {
            string conflictingPath = AssetDatabase.GUIDToAssetPath(conflictingEntry.guid);
            if (!replaceExistingAddress)
            {
                throw new InvalidOperationException(
                    $"Addressable 地址已指向其他资源：{modelAddress}\n{conflictingPath}");
            }
            if (!settings.RemoveAssetEntry(conflictingEntry.guid, false))
                throw new InvalidOperationException("无法移除旧的 Addressable Entry：" + conflictingPath);
            replacedPath = conflictingPath;
        }

        AddressableAssetEntry entry = settings.FindAssetEntry(guid);
        bool created = entry == null;
        if (created)
            entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup, false, false);
        if (entry == null)
            throw new InvalidOperationException("无法创建展示 Prefab 的 Addressable Entry。");

        bool addressChanged = !string.Equals(entry.address, modelAddress, StringComparison.Ordinal);
        if (addressChanged)
            entry.address = modelAddress;
        if (created || addressChanged)
        {
            settings.SetDirty(
                AddressableAssetSettings.ModificationEvent.EntryModified,
                entry,
                true,
                true);
            AssetDatabase.SaveAssets();
        }

        if (!string.IsNullOrEmpty(replacedPath))
            return $"已将 {modelAddress} 从 {replacedPath} 迁移到 {prefabPath}";
        if (created) return $"已在 {settings.DefaultGroup.Name} 中创建 {modelAddress}";
        return addressChanged ? $"已更新为 {modelAddress}" : $"已存在 {modelAddress}，无需修改";
    }

    static void SaveManifest(Manifest manifest)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        string fullPath = string.IsNullOrEmpty(projectRoot)
            ? string.Empty
            : Path.Combine(projectRoot, ManifestPath.Replace('/', Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            throw new InvalidOperationException($"找不到 Manifest：{ManifestPath}");

        string json = JsonUtility.ToJson(manifest, true) + Environment.NewLine;
        File.WriteAllText(fullPath, json, new UTF8Encoding(false));
    }

    static List<AnimationJob> BuildAnimationJobs(
        Manifest manifest,
        IReadOnlyList<CharacterSettings> characters,
        List<string> errors)
    {
        var jobs = new List<AnimationJob>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        string extractedRoot = NormalizeAssetPath(manifest.project.animationExtractedRoot);
        string runtimeRoot = NormalizeAssetPath(manifest.project.animationRuntimeRoot);
        if (!IsAssetPath(extractedRoot)) errors.Add("animationExtractedRoot 必须位于 Assets 下。");
        if (!IsAssetPath(runtimeRoot)) errors.Add("animationRuntimeRoot 必须位于 Assets 下。");

        foreach (CharacterSettings character in characters)
        {
            if (character == null)
            {
                errors.Add("Manifest 包含空角色配置。");
                continue;
            }

            bool excludeUpperArmTwist;
            if (character.runtimeClipSuffix == "_ModelRoot_NoArmTwist")
                excludeUpperArmTwist = true;
            else if (character.runtimeClipSuffix == "_ModelRoot")
                excludeUpperArmTwist = false;
            else
            {
                errors.Add($"{character.id}.runtimeClipSuffix 仅支持 _ModelRoot 或 _ModelRoot_NoArmTwist。");
                continue;
            }

            string prefix = $"Ani_Avatar_{character.bodyType}_{character.weaponType}_{character.id}";
            AddJob(
                Join(extractedRoot, "Animations", $"Ani_Avatar_{character.bodyType}_Standby.anim"),
                Join(runtimeRoot, "Common", character.bodyType, "Default"),
                excludeUpperArmTwist,
                character.runtimeClipSuffix);

            AddJob(
                Join(extractedRoot, character.slug, "AnimationClip", "AuxiliaryOnly", $"{prefix}_Standby.anim"),
                Join(runtimeRoot, "Character", character.id, "Default", "Auxiliary"),
                excludeUpperArmTwist,
                character.runtimeClipSuffix);

            foreach (string phase in new[] { "AS", "Loop", "BS" })
            {
                AddJob(
                    Join(extractedRoot, "Animations", "Common", "EquipShow", character.weaponType,
                        $"Ani_Avatar_{character.bodyType}_{character.weaponType}_UI_WeaponShow{phase}.anim"),
                    Join(runtimeRoot, "Common", character.bodyType, "Weapon", character.weaponType),
                    excludeUpperArmTwist,
                    character.runtimeClipSuffix);
                AddJob(
                    Join(extractedRoot, character.slug, "AnimationClip", "AuxiliaryOnly",
                        $"{prefix}_UI_WeaponShow{phase}.anim"),
                    Join(runtimeRoot, "Character", character.id, "Weapon", "Auxiliary"),
                    excludeUpperArmTwist,
                    character.runtimeClipSuffix);
                AddJob(
                    Join(extractedRoot, "Animations", "Common", "Relic",
                        $"Ani_Avatar_{character.bodyType}_RelicShow{phase}.anim"),
                    Join(runtimeRoot, "Common", character.bodyType, "Relic"),
                    excludeUpperArmTwist,
                    character.runtimeClipSuffix);
                AddJob(
                    Join(extractedRoot, character.slug, "AnimationClip", "AuxiliaryOnly",
                        $"{prefix}_RelicShow{phase}.anim"),
                    Join(runtimeRoot, "Character", character.id, "Relic", "Auxiliary"),
                    excludeUpperArmTwist,
                    character.runtimeClipSuffix);

                if (!string.IsNullOrWhiteSpace(character.teamConfig))
                {
                    AddJob(
                        Join(extractedRoot, character.slug, "AnimationClip", "FullBodyHumanoid",
                            $"{prefix}_{character.teamConfig}_{phase}.anim"),
                        Join(runtimeRoot, "Character", character.id, "TeamSwitch", "FullBody"),
                        excludeUpperArmTwist,
                        character.runtimeClipSuffix);
                }
            }
        }

        return jobs;

        void AddJob(string sourcePath, string outputFolder, bool excludeTwist, string suffix)
        {
            string key = sourcePath + "|" + outputFolder;
            if (!keys.Add(key)) return;
            string sourceName = Path.GetFileNameWithoutExtension(sourcePath);
            jobs.Add(new AnimationJob
            {
                SourcePath = sourcePath,
                OutputFolder = outputFolder,
                ExpectedOutputPath = Join(outputFolder, sourceName + suffix + ".anim"),
                ExcludeUpperArmTwist = excludeTwist
            });
        }
    }

    static List<FaceGroup> BuildMissingFaceGroups(
        Manifest manifest,
        IReadOnlyList<CharacterSettings> characters,
        List<string> errors,
        List<string> warnings)
    {
        var groups = new Dictionary<string, FaceGroup>(StringComparer.Ordinal);
        string faceRoot = NormalizeAssetPath(manifest.project.facePresetRoot);
        if (!IsAssetPath(faceRoot))
        {
            errors.Add("facePresetRoot 必须位于 Assets 下。");
            return groups.Values.ToList();
        }

        string lastRun = string.Empty;
        bool lastRunResolved = false;
        foreach (CharacterSettings character in characters)
        {
            if (character == null) continue;
            foreach (string phase in new[] { "AS", "Loop", "BS" })
            {
                AddFace(
                    Join(faceRoot, "Equip", character.weaponType),
                    $"Face_Common_{character.bodyType}_{character.weaponType}_UI_ShowWeapon{phase}.asset",
                    Path.Combine("Equip", character.weaponType),
                    $"Cs_Emo_Avatar_Common_{character.bodyType}_{character.weaponType}_UI_ShowWeapon{phase}.dat");

                if (!string.IsNullOrWhiteSpace(character.teamConfig))
                {
                    AddFace(
                        Join(faceRoot, character.id),
                        $"Face_{character.id}_{character.teamConfig}{phase}.asset",
                        character.id,
                        $"Cs_Emo_Avatar_{character.id}_{character.teamConfig}{phase}.dat");
                }
            }
        }

        return groups.Values.Where(group => group.RequiredDatNames.Count > 0).ToList();

        void AddFace(string outputFolder, string assetName, string stagingGroup, string datName)
        {
            if (AssetDatabase.LoadAssetAtPath<FaceExpressionPreset>(Join(outputFolder, assetName)) != null)
                return;
            if (!lastRunResolved)
            {
                lastRun = GetLastRunPath(manifest.project.presentationStagingRoot, warnings);
                lastRunResolved = true;
            }
            if (string.IsNullOrEmpty(lastRun)) return;

            string sourceFolder = Path.Combine(lastRun, "AcceptedFaceDat", stagingGroup);
            string key = sourceFolder + "|" + outputFolder;
            if (!groups.TryGetValue(key, out FaceGroup group))
            {
                group = new FaceGroup { SourceFolder = sourceFolder, OutputFolder = outputFolder };
                groups.Add(key, group);
            }
            if (!group.RequiredDatNames.Contains(datName)) group.RequiredDatNames.Add(datName);
        }
    }

    static void FilterUnavailableInputs(
        List<AnimationJob> animationJobs,
        List<FaceGroup> faceGroups,
        List<string> warnings)
    {
        for (int index = animationJobs.Count - 1; index >= 0; index--)
        {
            AnimationJob job = animationJobs[index];
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(job.SourcePath) == null)
            {
                warnings.Add("跳过缺失的源动画：" + job.SourcePath);
                animationJobs.RemoveAt(index);
            }
        }

        for (int groupIndex = faceGroups.Count - 1; groupIndex >= 0; groupIndex--)
        {
            FaceGroup group = faceGroups[groupIndex];
            if (!Directory.Exists(group.SourceFolder))
            {
                warnings.Add("跳过缺失的表情 DAT 分组：" + group.SourceFolder);
                faceGroups.RemoveAt(groupIndex);
                continue;
            }

            for (int datIndex = group.RequiredDatNames.Count - 1; datIndex >= 0; datIndex--)
            {
                string datName = group.RequiredDatNames[datIndex];
                if (!File.Exists(Path.Combine(group.SourceFolder, datName)))
                {
                    warnings.Add("跳过缺失的表情 DAT：" + Path.Combine(group.SourceFolder, datName));
                    group.RequiredDatNames.RemoveAt(datIndex);
                }
            }

            if (group.RequiredDatNames.Count == 0)
                faceGroups.RemoveAt(groupIndex);
        }
    }

    static string GetLastRunPath(string stagingRoot, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(stagingRoot))
        {
            errors.Add("Manifest 缺少 presentationStagingRoot。");
            return string.Empty;
        }

        string root = Path.GetFullPath(stagingRoot);
        string lastRunFile = Path.Combine(root, "last-run.txt");
        if (!File.Exists(lastRunFile))
        {
            errors.Add("找不到最近一次提取记录：" + lastRunFile);
            return string.Empty;
        }

        string lastRun = File.ReadAllText(lastRunFile).Trim();
        if (!Directory.Exists(lastRun))
        {
            errors.Add("最近一次提取目录不存在：" + lastRun);
            return string.Empty;
        }
        return lastRun;
    }

    static void EnsureAssetFolder(string path)
    {
        if (!IsAssetPath(path))
            throw new InvalidOperationException("输出目录必须位于 Assets 下：" + path);

        string current = "Assets";
        foreach (string part in path.Substring("Assets/".Length).Split('/'))
        {
            string next = current + "/" + part;
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
            current = next;
        }
    }

    static bool IsAssetPath(string path)
    {
        return path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal);
    }

    static string NormalizeAssetPath(string path)
    {
        return path?.Trim().Replace('\\', '/').TrimEnd('/') ?? string.Empty;
    }

    static string Join(params string[] parts)
    {
        var result = new StringBuilder();
        foreach (string rawPart in parts)
        {
            string part = rawPart?.Trim().Replace('\\', '/').Trim('/');
            if (string.IsNullOrEmpty(part)) continue;
            if (result.Length > 0) result.Append('/');
            result.Append(part);
        }
        return result.ToString();
    }

    static void LogFailure(IEnumerable<string> errors)
    {
        Debug.LogError("角色展示资源流水线已取消：\n" + string.Join("\n", errors));
    }
}

internal sealed class CharacterPresentationPipelineWindow : EditorWindow
{
    const string LastCharacterKey = "CharacterPresentationPipeline.LastCharacter";

    string[] characterIds = Array.Empty<string>();
    int selectedIndex;

    internal static void Open()
    {
        CharacterPresentationPipelineWindow window = GetWindow<CharacterPresentationPipelineWindow>();
        window.titleContent = new GUIContent("角色展示资源");
        window.minSize = new Vector2(360f, 130f);
        window.ReloadCharacters();
        window.Show();
    }

    void OnEnable()
    {
        ReloadCharacters();
    }

    void OnGUI()
    {
        if (characterIds.Length == 0)
        {
            EditorGUILayout.HelpBox("Manifest 中没有可用角色。", MessageType.Warning);
            return;
        }

        int nextIndex = EditorGUILayout.Popup("角色", selectedIndex, characterIds);
        if (nextIndex != selectedIndex)
        {
            selectedIndex = nextIndex;
            EditorPrefs.SetString(LastCharacterKey, characterIds[selectedIndex]);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("准备所选角色（仅新增或已更新资源）"))
            CharacterPresentationAssetPipeline.PrepareCharacter(characterIds[selectedIndex], false);
        if (GUILayout.Button("强制重建所选角色"))
            CharacterPresentationAssetPipeline.PrepareCharacter(characterIds[selectedIndex], true);
    }

    void ReloadCharacters()
    {
        characterIds = CharacterPresentationAssetPipeline.GetCharacterIds();
        string lastCharacter = EditorPrefs.GetString(LastCharacterKey, string.Empty);
        selectedIndex = Math.Max(0, Array.IndexOf(characterIds, lastCharacter));
        Repaint();
    }
}
