[CmdletBinding()]
param(
    [ValidateSet('Plan', 'Extract', 'Audit', 'Sync', 'All')]
    [string]$Mode = 'Plan',

    [string]$ManifestPath = (Join-Path $PSScriptRoot 'PresentationAssetManifest.json'),

    [string]$CharacterId,

    [switch]$AllCharacters,

    [string]$GamePath,

    [string]$MapName,

    [string]$StagingRoot = 'F:\资源文件\PresentationExtraction',

    [string]$RunPath,

    [switch]$UseFallbackCli,

    [switch]$Resume,

    [switch]$Overwrite
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function New-ExpectedResource {
    param(
        [string]$Kind,
        [string]$Role,
        [string]$SourceName,
        [string]$ProjectPath,
        [string[]]$ExpectedClassifications = @(),
        [string]$FaceImportGroup = ''
    )

    [PSCustomObject]@{
        Kind                    = $Kind
        Role                    = $Role
        SourceName              = $SourceName
        ProjectPath             = $ProjectPath
        ExpectedClassifications = @($ExpectedClassifications)
        FaceImportGroup         = $FaceImportGroup
    }
}

function Get-ExpectedResources {
    param(
        [object]$Manifest,
        [object[]]$Characters
    )

    $resources = [System.Collections.Generic.List[object]]::new()
    $known = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $animationRoot = $Manifest.project.animationExtractedRoot.TrimEnd('/')
    $faceRoot = $Manifest.project.facePresetRoot.TrimEnd('/')

    function Add-Resource {
        param([object]$Resource)

        $key = "$($Resource.Kind)|$($Resource.SourceName)"
        if ($known.Add($key)) {
            $resources.Add($Resource)
        }
    }

    foreach ($character in $Characters) {
        $id = [string]$character.id
        $slug = [string]$character.slug
        $body = [string]$character.bodyType
        $weapon = [string]$character.weaponType
        $characterPrefix = "Ani_Avatar_${body}_${weapon}_${id}"

        $defaultBody = "Ani_Avatar_${body}_Standby"
        Add-Resource (New-ExpectedResource 'AnimationClip' 'Default.Body' $defaultBody `
            "$animationRoot/Animations/$defaultBody.anim" @('FullBodyHumanoid'))

        $defaultAuxiliary = "${characterPrefix}_Standby"
        Add-Resource (New-ExpectedResource 'AnimationClip' "Default.Auxiliary.$id" $defaultAuxiliary `
            "$animationRoot/$slug/AnimationClip/AuxiliaryOnly/$defaultAuxiliary.anim" @('AuxiliaryOnly', 'AuxiliaryWithAttachments'))

        foreach ($phase in @('AS', 'Loop', 'BS')) {
            $weaponBody = "Ani_Avatar_${body}_${weapon}_UI_WeaponShow$phase"
            Add-Resource (New-ExpectedResource 'AnimationClip' "Weapon.Body.$phase" $weaponBody `
                "$animationRoot/Animations/Common/EquipShow/$weapon/$weaponBody.anim" @('FullBodyHumanoid'))

            $weaponAuxiliary = "${characterPrefix}_UI_WeaponShow$phase"
            Add-Resource (New-ExpectedResource 'AnimationClip' "Weapon.Auxiliary.$id.$phase" $weaponAuxiliary `
                "$animationRoot/$slug/AnimationClip/AuxiliaryOnly/$weaponAuxiliary.anim" @('AuxiliaryOnly', 'AuxiliaryWithAttachments'))

            $weaponFace = "Cs_Emo_Avatar_Common_${body}_${weapon}_UI_ShowWeapon$phase"
            Add-Resource (New-ExpectedResource 'MonoBehaviour' "Weapon.Face.$body.$weapon.$phase" $weaponFace `
                "$faceRoot/Equip/$weapon/Face_Common_${body}_${weapon}_UI_ShowWeapon$phase.asset" @() "Equip/$weapon")

            $relicBody = "Ani_Avatar_${body}_RelicShow$phase"
            Add-Resource (New-ExpectedResource 'AnimationClip' "Relic.Body.$phase" $relicBody `
                "$animationRoot/Animations/Common/Relic/$relicBody.anim" @('FullBodyHumanoid'))

            $relicAuxiliary = "${characterPrefix}_RelicShow$phase"
            Add-Resource (New-ExpectedResource 'AnimationClip' "Relic.Auxiliary.$id.$phase" $relicAuxiliary `
                "$animationRoot/$slug/AnimationClip/AuxiliaryOnly/$relicAuxiliary.anim" @('AuxiliaryOnly', 'AuxiliaryWithAttachments'))
        }

        if (-not [string]::IsNullOrWhiteSpace([string]$character.teamConfig)) {
            foreach ($phase in @('AS', 'Loop', 'BS')) {
                $teamClip = "${characterPrefix}_$($character.teamConfig)_$phase"
                Add-Resource (New-ExpectedResource 'AnimationClip' "Team.$id.$phase" $teamClip `
                    "$animationRoot/$slug/AnimationClip/FullBodyHumanoid/$teamClip.anim" @('FullBodyHumanoid'))

                $teamFace = "Cs_Emo_Avatar_${id}_$($character.teamConfig)$phase"
                Add-Resource (New-ExpectedResource 'MonoBehaviour' "Team.Face.$id.$phase" $teamFace `
                    "$faceRoot/$id/Face_$($id)_$($character.teamConfig)$phase.asset" @() $id)
            }
        }

        foreach ($idle in @($character.specialIdle)) {
            $idleClip = "${characterPrefix}_$idle"
            Add-Resource (New-ExpectedResource 'AnimationClip' "SpecialIdle.$id.$idle" $idleClip `
                "$animationRoot/$slug/AnimationClip/FullBodyHumanoid/$idleClip.anim" @('FullBodyHumanoid'))

            $idleFace = "Cs_Emo_Avatar_${id}_$idle"
            Add-Resource (New-ExpectedResource 'MonoBehaviour' "SpecialIdle.Face.$id.$idle" $idleFace `
                "$faceRoot/$id/Face_$($id)_$idle.asset" @() $id)
        }
    }

    Add-Resource (New-ExpectedResource 'MonoBehaviour' 'Default.Face.Normal01' 'Cs_Emo_Avatar_Normal01' `
        "$faceRoot/Common/Face_Normal_01.asset" @() 'Common')
    foreach ($phase in @('AS', 'Loop', 'BS')) {
        $sourceName = "Cs_Emo_Avatar_Common_RelicShow_$phase"
        Add-Resource (New-ExpectedResource 'MonoBehaviour' "Relic.Face.$phase" $sourceName `
            "$faceRoot/Relic/Face_Common_RelicShow_$phase.asset" @() 'Relic')
    }

    return $resources.ToArray()
}

function Get-ProjectFilePath {
    param(
        [string]$ProjectRoot,
        [string]$ProjectPath
    )

    Join-Path $ProjectRoot ($ProjectPath.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
}

function Get-ResourcePlan {
    param(
        [object[]]$Resources,
        [string]$ProjectRoot
    )

    foreach ($resource in $Resources) {
        $exists = Test-Path -LiteralPath (Get-ProjectFilePath $ProjectRoot $resource.ProjectPath) -PathType Leaf
        [PSCustomObject]@{
            Status      = if ($exists) { 'Present' } else { 'Missing' }
            Kind        = $resource.Kind
            Role        = $resource.Role
            SourceName  = $resource.SourceName
            ProjectPath = $resource.ProjectPath
            Resource    = $resource
        }
    }
}

function Show-Plan {
    param([object[]]$Plan)

    $planRows = @($Plan | Select-Object Status, Kind, Role, SourceName, ProjectPath)
    $planRows | Format-Table -AutoSize
    $presentCount = @($Plan | Where-Object Status -eq 'Present').Count
    $missingCount = @($Plan | Where-Object Status -eq 'Missing').Count
    Write-Host "资源总数: $($Plan.Count)，已存在: $presentCount，缺少: $missingCount"
}

function Get-EffectiveRunPath {
    param(
        [string]$RequestedRunPath,
        [string]$Root,
        [switch]$Create
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedRunPath)) {
        return [System.IO.Path]::GetFullPath($RequestedRunPath)
    }

    $lastRunFile = Join-Path $Root 'last-run.txt'
    if ($Create) {
        $newRunPath = Join-Path $Root (Join-Path 'runs' (Get-Date -Format 'yyyyMMdd-HHmmss'))
        [void][System.IO.Directory]::CreateDirectory($newRunPath)
        [void][System.IO.Directory]::CreateDirectory($Root)
        [System.IO.File]::WriteAllText($lastRunFile, $newRunPath, [System.Text.UTF8Encoding]::new($false))
        return $newRunPath
    }

    if (-not (Test-Path -LiteralPath $lastRunFile -PathType Leaf)) {
        throw "找不到上一次提取记录，请传入 -RunPath，或先执行 Extract。"
    }

    return [System.IO.File]::ReadAllText($lastRunFile).Trim()
}

function Get-ResumableRunPath {
    param(
        [string]$Root,
        [object[]]$MissingResources
    )

    $lastRunFile = Join-Path $Root 'last-run.txt'
    if (-not (Test-Path -LiteralPath $lastRunFile -PathType Leaf)) {
        return $null
    }

    $candidate = [System.IO.File]::ReadAllText($lastRunFile).Trim()
    $requestPath = Join-Path $candidate 'request.json'
    if (-not (Test-Path -LiteralPath $candidate -PathType Container) -or
        -not (Test-Path -LiteralPath $requestPath -PathType Leaf) -or
        (Test-Path -LiteralPath (Join-Path $candidate 'audit-result.json') -PathType Leaf)) {
        return $null
    }

    try {
        $previousRequest = @((Get-Content -Raw -Encoding UTF8 -LiteralPath $requestPath | ConvertFrom-Json))
    }
    catch {
        return $null
    }

    [string[]]$previousKeys = @($previousRequest | ForEach-Object {
        "$($_.Kind)|$($_.SourceName)|$($_.ProjectPath)"
    } | Sort-Object -Unique)
    [string[]]$currentKeys = @($MissingResources | ForEach-Object {
        "$($_.Kind)|$($_.SourceName)|$($_.ProjectPath)"
    } | Sort-Object -Unique)
    $differences = @(Compare-Object -ReferenceObject $previousKeys -DifferenceObject $currentKeys)
    if ($previousKeys.Count -ne $currentKeys.Count -or
        $differences.Count -ne 0) {
        return $null
    }

    foreach ($group in @($previousRequest | Group-Object Kind)) {
        $completionPath = Join-Path $candidate "$($group.Name).complete.json"
        if (-not (Test-Path -LiteralPath $completionPath -PathType Leaf)) {
            continue
        }
        foreach ($resource in $group.Group) {
            $extension = if ($resource.Kind -eq 'AnimationClip') { '.anim' } else { '.dat' }
            $kindOutput = Join-Path $candidate $resource.Kind
            [object[]]$matches = @()
            if (Test-Path -LiteralPath $kindOutput -PathType Container) {
                $matches = @(Get-ChildItem -LiteralPath $kindOutput -File -Recurse `
                    -Filter ($resource.SourceName + $extension))
            }
            if ($matches.Count -ne 1 -or $matches[0].Length -le 0) {
                return $null
            }
        }
    }

    return $candidate
}

function Invoke-AnimeStudioExtraction {
    param(
        [object[]]$MissingResources,
        [object]$Manifest,
        [string]$CliPath,
        [string]$AssetMapName,
        [string]$OutputRunPath
    )

    if (-not (Test-Path -LiteralPath $CliPath -PathType Leaf)) {
        throw "AnimeStudio CLI 不存在: $CliPath"
    }
    if ([string]::IsNullOrWhiteSpace($AssetMapName)) {
        throw 'Extract/All 模式必须通过 -MapName 传入 AssetMap 文件路径。'
    }
    if (-not (Test-Path -LiteralPath $AssetMapName -PathType Leaf)) {
        throw "AssetMap 文件不存在: $AssetMapName"
    }
    $assetMapPath = (Resolve-Path -LiteralPath $AssetMapName).Path

    [void][System.IO.Directory]::CreateDirectory($OutputRunPath)
    $requestPath = Join-Path $OutputRunPath 'request.json'
    $MissingResources | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $requestPath -Encoding UTF8

    foreach ($group in @($MissingResources | Group-Object Kind)) {
        $kind = [string]$group.Name
        $assetType = if ($kind -eq 'AnimationClip') { 'AnimationClip' } else { 'MonoBehaviour' }
        $exportType = if ($kind -eq 'AnimationClip') { 'Convert' } else { 'Raw' }
        $kindOutput = Join-Path $OutputRunPath $kind
        $completionPath = Join-Path $OutputRunPath "$kind.complete.json"
        $extension = if ($kind -eq 'AnimationClip') { '.anim' } else { '.dat' }
        if (Test-Path -LiteralPath $completionPath -PathType Leaf) {
            Write-Host "暂存区内 $kind 的 $($group.Count) 项均已完成，断点续跑时跳过。"
            continue
        }

        if (Test-Path -LiteralPath $kindOutput -PathType Container) {
            $resolvedRun = [System.IO.Path]::GetFullPath($OutputRunPath).TrimEnd(
                [System.IO.Path]::DirectorySeparatorChar,
                [System.IO.Path]::AltDirectorySeparatorChar)
            $resolvedKindOutput = [System.IO.Path]::GetFullPath($kindOutput)
            if (-not $resolvedKindOutput.StartsWith(
                $resolvedRun + [System.IO.Path]::DirectorySeparatorChar,
                [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "拒绝清理不在当前 run 内的暂存目录: $resolvedKindOutput"
            }
            Remove-Item -LiteralPath $resolvedKindOutput -Recurse -Force
        }
        [void][System.IO.Directory]::CreateDirectory($kindOutput)
        $escapedNames = @($group.Group | ForEach-Object { [regex]::Escape([string]$_.SourceName) })
        $namePattern = '^(?:' + ($escapedNames -join '|') + ')$'
        $logPath = Join-Path $OutputRunPath "$kind.log"
        $arguments = @(
            $assetMapPath,
            $kindOutput,
            '--game', [string]$Manifest.animeStudio.game,
            '--map_op', 'AssetMap, Load',
            '--map_type', 'MessagePack',
            '--map_name', $assetMapPath,
            '--types', $assetType,
            '--names', $namePattern,
            '--group_assets', 'ByType',
            '--export_type', $exportType
        )

        Write-Host "正在提取 $kind，共 $($group.Count) 项。"
        Push-Location ([System.IO.Path]::GetDirectoryName($CliPath))
        try {
            & $CliPath @arguments 2>&1 | Out-File -LiteralPath $logPath -Encoding utf8
            $exitCode = $LASTEXITCODE
        }
        finally {
            Pop-Location
        }
        if ($exitCode -ne 0) {
            $logText = Get-Content -Raw -Encoding UTF8 -LiteralPath $logPath
            if ($logText.Contains('System.IndexOutOfRangeException') -and
                $logText.Contains('AnimeStudio.CLI.Program.Run')) {
                Write-Warning "$kind 没有取得匹配资源；逐项审计会记录缺失项。完整日志: $logPath"
                continue
            }
            Write-Host "AnimeStudio CLI 提取 $kind 失败，日志末尾："
            Get-Content -Encoding UTF8 -LiteralPath $logPath -Tail 40 | Write-Host
            throw "AnimeStudio CLI 提取 $kind 失败，退出码 $exitCode。日志: $logPath"
        }

        $invalidKindOutputs = [System.Collections.Generic.List[string]]::new()
        foreach ($resource in $group.Group) {
            $matches = @(Get-ChildItem -LiteralPath $kindOutput -File -Recurse `
                -Filter ($resource.SourceName + $extension))
            if ($matches.Count -ne 1 -or $matches[0].Length -le 0) {
                $invalidKindOutputs.Add("$($resource.SourceName): 找到 $($matches.Count) 个")
            }
        }
        if ($invalidKindOutputs.Count -gt 0) {
            Write-Warning "$kind 返回后仍有资源不满足唯一且非空条件:`n$($invalidKindOutputs -join "`n")"
            continue
        }
        [PSCustomObject]@{
            kind        = $kind
            completedAt = (Get-Date).ToString('o')
            resources   = @($group.Group | ForEach-Object SourceName)
        } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $completionPath -Encoding UTF8
        Write-Host "AnimeStudio CLI 提取 $kind 完成。完整日志: $logPath"
    }

    $missingOutputs = [System.Collections.Generic.List[string]]::new()
    foreach ($resource in $MissingResources) {
        $extension = if ($resource.Kind -eq 'AnimationClip') { '.anim' } else { '.dat' }
        $matches = @(Get-ChildItem -LiteralPath (Join-Path $OutputRunPath $resource.Kind) `
            -File -Recurse -Filter ($resource.SourceName + $extension))
        if ($matches.Count -ne 1 -or $matches[0].Length -le 0) {
            $missingOutputs.Add("$($resource.SourceName): 找到 $($matches.Count) 个")
        }
    }
    if ($missingOutputs.Count -gt 0) {
        Write-Warning "CLI 返回后仍有资源不满足唯一匹配条件:`n$($missingOutputs -join "`n")"
    }

    Write-Host "提取完成: $OutputRunPath"
}

function Invoke-RunAudit {
    param(
        [string]$OutputRunPath,
        [string]$ProjectRoot
    )

    $requestPath = Join-Path $OutputRunPath 'request.json'
    if (-not (Test-Path -LiteralPath $requestPath -PathType Leaf)) {
        throw "缺少提取请求记录: $requestPath"
    }
    $requested = @((Get-Content -Raw -Encoding UTF8 -LiteralPath $requestPath | ConvertFrom-Json))
    $errors = [System.Collections.Generic.List[string]]::new()
    $accepted = [System.Collections.Generic.List[string]]::new()
    $animationRequests = @($requested | Where-Object Kind -eq 'AnimationClip')

    if ($animationRequests.Count -gt 0) {
        $animationRoot = Join-Path $OutputRunPath 'AnimationClip'
        $auditCsv = Join-Path $OutputRunPath 'animation-audit.csv'
        $auditRows = @()
        if (Test-Path -LiteralPath $animationRoot -PathType Container) {
            $clipFiles = @(Get-ChildItem -LiteralPath $animationRoot -File -Recurse -Filter '*.anim')
            if ($clipFiles.Count -gt 0) {
                & (Join-Path $ProjectRoot 'Tools/AnimationClipAudit.ps1') `
                    -InputPath $animationRoot -CompanionRoot $animationRoot -OutputPath $auditCsv -Recurse
                $auditRows = @(Import-Csv -LiteralPath $auditCsv -Encoding UTF8)
            }
        }

        foreach ($resource in $animationRequests) {
            $issueCount = $errors.Count
            $rows = @($auditRows | Where-Object ClipName -eq $resource.SourceName)
            if ($rows.Count -ne 1) {
                $errors.Add("$($resource.SourceName): 审计记录数量为 $($rows.Count)")
                continue
            }
            $row = $rows[0]
            if ($row.InvalidNumericValues -ne 'False') {
                $errors.Add("$($resource.SourceName): 包含无效数值")
            }
            if ([double]$row.DurationSeconds -le 0 -or [double]$row.SampleRate -le 0) {
                $errors.Add("$($resource.SourceName): 时长或采样率无效")
            }
            if (@($resource.ExpectedClassifications) -notcontains $row.Classification) {
                $errors.Add("$($resource.SourceName): 分类为 $($row.Classification)，预期为 $($resource.ExpectedClassifications -join '/')")
            }
            if ($row.Classification -like 'Auxiliary*' -and [int]$row.UnresolvedPaths -gt 0) {
                $errors.Add("$($resource.SourceName): Auxiliary 动画包含未解析 path_* 路径")
            }
            if ($errors.Count -eq $issueCount) {
                $accepted.Add("AnimationClip|$($resource.SourceName)")
            }
        }
    }

    foreach ($resource in @($requested | Where-Object Kind -eq 'MonoBehaviour')) {
        $faceOutput = Join-Path $OutputRunPath 'MonoBehaviour'
        $matches = @()
        if (Test-Path -LiteralPath $faceOutput -PathType Container) {
            $matches = @(Get-ChildItem -LiteralPath $faceOutput -File -Recurse -Filter ($resource.SourceName + '.dat'))
        }
        if ($matches.Count -ne 1) {
            $errors.Add("$($resource.SourceName): DAT 数量为 $($matches.Count)")
        }
        elseif ($matches[0].Length -le 0) {
            $errors.Add("$($resource.SourceName): DAT 文件为空")
        }
        else {
            $accepted.Add("MonoBehaviour|$($resource.SourceName)")
        }
    }

    $auditResult = [PSCustomObject]@{
        passed    = $true
        auditedAt = (Get-Date).ToString('o')
        accepted  = $accepted.ToArray()
        warnings  = $errors.ToArray()
        errors    = @()
    }
    $auditResult | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputRunPath 'audit-result.json') -Encoding UTF8
    if ($errors.Count -gt 0) {
        if ($accepted.Count -eq 0) {
            Write-Warning "资源审计失败:`n$($errors -join "`n")"
        }
        else {
            Write-Warning ($errors -join "`n")
        }
    }
    if ($accepted.Count -gt 0) {
        Write-Host "资源审计通过: $OutputRunPath"
    }
}

function Copy-CheckedFile {
    param(
        [string]$Source,
        [string]$Destination,
        [switch]$AllowOverwrite
    )

    $destinationDirectory = [System.IO.Path]::GetDirectoryName($Destination)
    [void][System.IO.Directory]::CreateDirectory($destinationDirectory)
    if (Test-Path -LiteralPath $Destination -PathType Leaf) {
        $sourceHash = (Get-FileHash -LiteralPath $Source -Algorithm SHA256).Hash
        $destinationHash = (Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash
        if ($sourceHash -eq $destinationHash) {
            Write-Host "内容相同，跳过: $Destination"
            return
        }
        if (-not $AllowOverwrite) {
            throw "目标已存在且内容不同；如确认覆盖，请使用 -Overwrite：$Destination"
        }
    }
    Copy-Item -LiteralPath $Source -Destination $Destination -Force:$AllowOverwrite
    Write-Host "已同步: $Destination"
}

function Sync-RunAssets {
    param(
        [string]$OutputRunPath,
        [string]$ProjectRoot,
        [switch]$AllowOverwrite
    )

    $auditResultPath = Join-Path $OutputRunPath 'audit-result.json'
    if (-not (Test-Path -LiteralPath $auditResultPath -PathType Leaf)) {
        throw '同步前必须先执行 Audit。'
    }
    $auditResult = Get-Content -Raw -Encoding UTF8 -LiteralPath $auditResultPath | ConvertFrom-Json
    if (-not $auditResult.passed) {
        throw '审计未通过，禁止同步。'
    }

    $requested = @((Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $OutputRunPath 'request.json') | ConvertFrom-Json))
    $hasAcceptedList = $null -ne $auditResult.PSObject.Properties['accepted']
    $acceptedKeys = if ($hasAcceptedList) { @($auditResult.accepted) } else { @() }
    foreach ($resource in @($requested | Where-Object Kind -eq 'AnimationClip')) {
        if ($hasAcceptedList -and $acceptedKeys -notcontains "AnimationClip|$($resource.SourceName)") { continue }
        $source = @(Get-ChildItem -LiteralPath (Join-Path $OutputRunPath 'AnimationClip') `
            -File -Recurse -Filter ($resource.SourceName + '.anim'))[0].FullName
        $destination = Get-ProjectFilePath $ProjectRoot $resource.ProjectPath
        Copy-CheckedFile $source $destination -AllowOverwrite:$AllowOverwrite
    }

    foreach ($group in @($requested | Where-Object Kind -eq 'MonoBehaviour' | Group-Object FaceImportGroup)) {
        $acceptedFolder = Join-Path $OutputRunPath (Join-Path 'AcceptedFaceDat' $group.Name)
        foreach ($resource in $group.Group) {
            if ($hasAcceptedList -and $acceptedKeys -notcontains "MonoBehaviour|$($resource.SourceName)") { continue }
            $source = @(Get-ChildItem -LiteralPath (Join-Path $OutputRunPath 'MonoBehaviour') `
                -File -Recurse -Filter ($resource.SourceName + '.dat'))[0].FullName
            Copy-CheckedFile $source (Join-Path $acceptedFolder ($resource.SourceName + '.dat')) `
                -AllowOverwrite:$AllowOverwrite
        }
        if (-not (Test-Path -LiteralPath $acceptedFolder -PathType Container)) { continue }
        Write-Host "表情 DAT 已整理到: $acceptedFolder"
        Write-Host "请在 Unity 使用 Tools/MyUISystem/角色展示/表情/导入 CsEmo DAT 文件夹，将其导入到 FacePreset/Extracted/$($group.Name)。"
    }
}

$resolvedManifestPath = (Resolve-Path -LiteralPath $ManifestPath).Path
$manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath $resolvedManifestPath | ConvertFrom-Json
if ([int]$manifest.schemaVersion -ne 1) {
    throw "不支持的清单版本: $($manifest.schemaVersion)"
}
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path ([System.IO.Path]::GetDirectoryName($resolvedManifestPath)) '..'))
$plan = @()
if ($Mode -eq 'Plan' -or $Mode -eq 'Extract' -or $Mode -eq 'All') {
    if ($AllCharacters -and -not [string]::IsNullOrWhiteSpace($CharacterId)) {
        throw 'CharacterId 和 AllCharacters 不能同时使用。'
    }

    if ($AllCharacters) {
        $selectedCharacters = @($manifest.characters)
    }
    else {
        if ([string]::IsNullOrWhiteSpace($CharacterId)) {
            throw '请使用 -CharacterId 指定角色，或显式使用 -AllCharacters。'
        }
        $selectedCharacters = @($manifest.characters | Where-Object id -eq $CharacterId)
        if ($selectedCharacters.Count -ne 1) {
            throw "CharacterId 必须唯一匹配一个角色，当前匹配数: $($selectedCharacters.Count)"
        }
    }

    $resources = @(Get-ExpectedResources $manifest $selectedCharacters)
    $plan = @(Get-ResourcePlan $resources $projectRoot)
}

if ($Mode -eq 'Plan') {
    Show-Plan $plan
    return
}

if ($Mode -eq 'Extract' -or $Mode -eq 'All') {
    $missingResources = @($plan | Where-Object Status -eq 'Missing' | ForEach-Object Resource)
    if ($missingResources.Count -eq 0) {
        Write-Host '清单内资源均已存在，不需要提取。'
        return
    }
    if ($Resume -and [string]::IsNullOrWhiteSpace($RunPath)) {
        $RunPath = Get-ResumableRunPath $StagingRoot $missingResources
        if (-not [string]::IsNullOrWhiteSpace($RunPath)) {
            Write-Host "继续上一次未完成的提取任务: $RunPath"
        }
    }
    if ([string]::IsNullOrWhiteSpace($RunPath)) {
        $RunPath = Get-EffectiveRunPath $RunPath $StagingRoot -Create
    }
    $cliPath = if ($UseFallbackCli) { [string]$manifest.animeStudio.fallbackCli } else { [string]$manifest.animeStudio.primaryCli }
    Invoke-AnimeStudioExtraction $missingResources $manifest $cliPath $MapName $RunPath
}
else {
    $RunPath = Get-EffectiveRunPath $RunPath $StagingRoot
}

if ($Mode -eq 'Audit' -or $Mode -eq 'All') {
    Invoke-RunAudit $RunPath $projectRoot
}
if ($Mode -eq 'Sync' -or $Mode -eq 'All') {
    Sync-RunAssets $RunPath $projectRoot -AllowOverwrite:$Overwrite
}
