[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Container })]
    [string]$InputPath,

    [ValidateScript({ -not $_ -or (Test-Path -LiteralPath $_ -PathType Container) })]
    [string]$CompanionRoot,

    [string]$OutputPath,

    [switch]$Recurse
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-AnimationClipAudit {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo]$File,

        [Parameter(Mandatory = $true)]
        [hashtable]$CompanionFiles
    )

    $paths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $sampleRate = 0.0
    $stopTime = 0.0
    $loopTime = 0
    $keyframeLines = 0
    $hasInvalidNumericValue = $false
    $humanoidMuscleAttributes = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $rootMotionAttributes = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)

    $reader = [System.IO.StreamReader]::new($File.FullName, [System.Text.Encoding]::UTF8, $true)
    try {
        while (($line = $reader.ReadLine()) -ne $null) {
            if ($line -match '^    path: (.+)$') {
                [void]$paths.Add($Matches[1])
                continue
            }

            if ($line -match '^  m_SampleRate: (.+)$') {
                [void][double]::TryParse(
                    $Matches[1],
                    [System.Globalization.NumberStyles]::Float,
                    [System.Globalization.CultureInfo]::InvariantCulture,
                    [ref]$sampleRate)
                continue
            }

            if ($line -match '^    m_StopTime: (.+)$') {
                [void][double]::TryParse(
                    $Matches[1],
                    [System.Globalization.NumberStyles]::Float,
                    [System.Globalization.CultureInfo]::InvariantCulture,
                    [ref]$stopTime)
                continue
            }

            if ($line -match '^    m_LoopTime: (\d+)$') {
                $loopTime = [int]$Matches[1]
                continue
            }

            if ($line -match '^\s+time: ') {
                $keyframeLines++
            }

            if ($line -match '^    attribute: (.+)$') {
                $attribute = $Matches[1]
                if ($attribute -match '^(?:Motion|Root)[TQ]\.[xyzw]$') {
                    [void]$rootMotionAttributes.Add($attribute)
                }
                elseif ($attribute -match '\s') {
                    # Humanoid muscle properties use semantic names such as "Left Arm Down-Up".
                    [void]$humanoidMuscleAttributes.Add($attribute)
                }
            }

            # Match invalid scalar values, but do not mistake m_PreInfinity/m_PostInfinity metadata for data errors.
            if ($line -match '(?i)(?:^|[:\[,]\s*)(?:[-+]?(?:nan|infinity|\.nan|\.inf))(?:\s*(?:[,}\]]|$))') {
                $hasInvalidNumericValue = $true
            }
        }
    }
    finally {
        $reader.Dispose()
    }

    $majorBodyPaths = [System.Collections.Generic.List[string]]::new()
    $attachmentPaths = [System.Collections.Generic.List[string]]::new()
    $auxiliaryPaths = [System.Collections.Generic.List[string]]::new()
    $unresolvedPaths = [System.Collections.Generic.List[string]]::new()

    foreach ($path in $paths) {
        if ($path -match '^path_\d+$') {
            $unresolvedPaths.Add($path)
            continue
        }

        if ($path -match '(^|/)\+') {
            $auxiliaryPaths.Add($path)
            continue
        }

        $leaf = ($path -split '/')[-1]
        if ($leaf -match '^Bip001(?: Pelvis| Spine\d*| Neck| Head| [LR] (?:Clavicle|UpperArm|Forearm|Hand|Thigh|Calf|Foot|Toe\d*))$') {
            $majorBodyPaths.Add($path)
        }
        else {
            $attachmentPaths.Add($path)
        }
    }

    $majorLeaves = @($majorBodyPaths | ForEach-Object { ($_ -split '/')[-1] })
    $hasTorso = @($majorLeaves | Where-Object { $_ -match '^Bip001 (?:Pelvis|Spine\d*)$' }).Count -gt 0
    $hasLeftArm = @($majorLeaves | Where-Object { $_ -match '^Bip001 L (?:UpperArm|Forearm|Hand)$' }).Count -gt 0
    $hasRightArm = @($majorLeaves | Where-Object { $_ -match '^Bip001 R (?:UpperArm|Forearm|Hand)$' }).Count -gt 0
    $hasLeftLeg = @($majorLeaves | Where-Object { $_ -match '^Bip001 L (?:Thigh|Calf|Foot)$' }).Count -gt 0
    $hasRightLeg = @($majorLeaves | Where-Object { $_ -match '^Bip001 R (?:Thigh|Calf|Foot)$' }).Count -gt 0
    $hasFullBodyCoverage = $hasTorso -and $hasLeftArm -and $hasRightArm -and $hasLeftLeg -and $hasRightLeg

    $classification = if ($hasInvalidNumericValue) {
        'InvalidNumeric'
    }
    elseif ($paths.Count -eq 0 -and $humanoidMuscleAttributes.Count -eq 0 -and $rootMotionAttributes.Count -eq 0) {
        'Empty'
    }
    elseif ($humanoidMuscleAttributes.Count -ge 20) {
        'FullBodyHumanoid'
    }
    elseif ($humanoidMuscleAttributes.Count -gt 0) {
        'PartialHumanoid'
    }
    elseif ($hasFullBodyCoverage) {
        'FullBodyTransform'
    }
    elseif ($majorBodyPaths.Count -gt 0) {
        'BodyPartial'
    }
    elseif ($auxiliaryPaths.Count -gt 0 -and ($attachmentPaths.Count -gt 0 -or $unresolvedPaths.Count -gt 0)) {
        'AuxiliaryWithAttachments'
    }
    elseif ($auxiliaryPaths.Count -gt 0) {
        'AuxiliaryOnly'
    }
    elseif ($attachmentPaths.Count -gt 0) {
        'AttachmentOnly'
    }
    else {
        'UnresolvedOnly'
    }

    $baseCandidates = [System.Collections.Generic.List[string]]::new()
    $clipName = [System.IO.Path]::GetFileNameWithoutExtension($File.Name)
    if ($clipName -match '^Ani_Avatar_(?<body>[^_]+)_(?<weapon>Sword|Claymore|Bow|Catalyst|Pole|Polearm)_(?<character>[^_]+)_(?<action>.+)$') {
        $baseCandidates.Add("Ani_Avatar_$($Matches.body)_$($Matches.weapon)_$($Matches.action).anim")
        $baseCandidates.Add("Ani_Avatar_$($Matches.body)_$($Matches.action).anim")
    }

    $foundBaseClips = [System.Collections.Generic.List[string]]::new()
    foreach ($candidate in $baseCandidates) {
        if ($CompanionFiles.ContainsKey($candidate)) {
            foreach ($candidatePath in $CompanionFiles[$candidate]) {
                $foundBaseClips.Add($candidatePath)
            }
        }
    }

    [PSCustomObject]@{
        ClipName              = $clipName
        FilePath              = $File.FullName
        SizeMB                = [math]::Round($File.Length / 1MB, 2)
        DurationSeconds       = [math]::Round($stopTime, 4)
        SampleRate            = $sampleRate
        LoopTime              = $loopTime
        KeyframeLines         = $keyframeLines
        UniqueBindingPaths    = $paths.Count
        MajorBodyPaths        = $majorBodyPaths.Count
        AttachmentPaths       = $attachmentPaths.Count
        AuxiliaryPaths        = $auxiliaryPaths.Count
        UnresolvedPaths       = $unresolvedPaths.Count
        HumanoidMuscleCurves  = $humanoidMuscleAttributes.Count
        RootMotionCurves      = $rootMotionAttributes.Count
        InvalidNumericValues  = $hasInvalidNumericValue
        Classification       = $classification
        SuggestedBaseClips    = $baseCandidates -join '; '
        FoundBaseClips        = $foundBaseClips -join '; '
    }
}

$resolvedInputPath = (Resolve-Path -LiteralPath $InputPath).Path
$resolvedCompanionRoot = if ($CompanionRoot) {
    (Resolve-Path -LiteralPath $CompanionRoot).Path
}
else {
    $resolvedInputPath
}

$companionFiles = @{}
Get-ChildItem -LiteralPath $resolvedCompanionRoot -Filter '*.anim' -File -Recurse | ForEach-Object {
    if (-not $companionFiles.ContainsKey($_.Name)) {
        $companionFiles[$_.Name] = [System.Collections.Generic.List[string]]::new()
    }

    $companionFiles[$_.Name].Add($_.FullName)
}

$scanArguments = @{
    LiteralPath = $resolvedInputPath
    Filter      = '*.anim'
    File        = $true
}
if ($Recurse) {
    $scanArguments.Recurse = $true
}

$results = @(
    Get-ChildItem @scanArguments |
        Sort-Object Name |
        ForEach-Object { Get-AnimationClipAudit -File $_ -CompanionFiles $companionFiles }
)

$results |
    Group-Object Classification |
    Sort-Object Name |
    Select-Object Name, Count |
    Format-Table -AutoSize

$results |
    Select-Object ClipName, DurationSeconds, SampleRate, MajorBodyPaths, HumanoidMuscleCurves, AuxiliaryPaths, UnresolvedPaths, Classification, FoundBaseClips |
    Format-Table -AutoSize

if ($OutputPath) {
    $resolvedOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
    $outputDirectory = [System.IO.Path]::GetDirectoryName($resolvedOutputPath)
    if ($outputDirectory) {
        [void][System.IO.Directory]::CreateDirectory($outputDirectory)
    }

    $results | Export-Csv -LiteralPath $resolvedOutputPath -NoTypeInformation -Encoding UTF8
    Write-Host "Report written to: $resolvedOutputPath"
}
