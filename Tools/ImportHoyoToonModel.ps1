#requires -Version 7.0
<#
.SYNOPSIS
Download a HoyoToon GI character package and copy verified assets into this project.
.EXAMPLE
pwsh -File Tools/ImportHoyoToonModel.ps1 -Character Odette
.EXAMPLE
pwsh -File Tools/ImportHoyoToonModel.ps1 -Character Odette -PlanOnly
.NOTES
Requires curl.exe. Character accepts an exact English/Chinese name, model name or numeric ID.
Multiple outfits require -Variant. Existing different project files are never overwritten.
The returned ModelAssets paths are inputs for the existing character onboarding tool.
Unity imports copied files through Auto Refresh; this script does not run HoyoToon Setup,
create Profiles, change Addressables or modify the existing onboarding pipeline.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Character,
    [string]$Variant,
    [string]$DownloadRoot = 'F:\资源文件\genshinmodel',
    [string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent),
    [switch]$PlanOnly,
    [switch]$RefreshCatalog,
    [switch]$AllowOfflineRevocation
)

$ErrorActionPreference = 'Stop'
$site = 'https://assets.hoyotoon.com'
$utf8 = [Text.UTF8Encoding]::new($false)
$DownloadRoot = [IO.Path]::GetFullPath($DownloadRoot)
$ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot)
if (!(Test-Path (Join-Path $ProjectRoot 'Assets/Art/Model/Character') -PathType Container)) {
    throw 'The project must have Assets/Art/Model/Character.'
}
New-Item -ItemType Directory -Force $DownloadRoot | Out-Null

function Invoke-Download([string]$Url, [string]$Destination, [string]$JsonBody,
    [string]$ResumeETag, [string]$PreviousETag) {
    $uri = [uri]::new([uri]$site, $Url)
    if ($uri.Scheme -ne 'https') { throw "HTTPS required: $uri" }
    $partial = "$Destination.part"
    $arguments = @('--fail', '--show-error', '--location', '--http1.1', '--proto', '=https',
        '--proto-redir', '=https', '--connect-timeout', '30', '--max-time', '180',
        '--retry', '2', '--retry-delay', '3', '--retry-max-time', '540',
        '--speed-limit', '1024', '--speed-time', '30', '--output', $partial)
    # Keep certificate validation; this opt-in only tolerates an unreachable revocation server.
    if ($AllowOfflineRevocation) { $arguments += '--ssl-revoke-best-effort' }
    if ($ResumeETag) {
        $ResumeETag = $ResumeETag.Trim('"')
        if ($ResumeETag -notmatch '^[a-fA-F0-9]{32}$') { throw 'Cannot verify this download ETag as MD5.' }
        if ((Test-Path -LiteralPath $Destination) -and
            (Get-FileHash -LiteralPath $Destination -Algorithm MD5).Hash -eq $ResumeETag) { return }
        if ((Test-Path -LiteralPath $partial) -and
            (Get-FileHash -LiteralPath $partial -Algorithm MD5).Hash -eq $ResumeETag) {
            Move-Item -LiteralPath $partial -Destination $Destination -Force
            return
        }
        $identityPath = "$partial.etag"
        $savedETag = if (Test-Path -LiteralPath $identityPath) {
            [IO.File]::ReadAllText($identityPath).Trim()
        } else { $PreviousETag }
        # Signed URLs expire; resume by file identity, not by the changing URL.
        if ((Test-Path -LiteralPath $partial) -and $savedETag -ne $ResumeETag) {
            $backup = "$partial.$([guid]::NewGuid().ToString('N')).bak"
            Move-Item -LiteralPath $partial -Destination $backup
            Write-Host "Different or unknown download identity; preserved previous data: $backup"
        }
        [IO.File]::WriteAllText($identityPath, $ResumeETag, $utf8)
        $offset = if (Test-Path -LiteralPath $partial) { (Get-Item -LiteralPath $partial).Length } else { 0 }
        Write-Host "Downloading $(Split-Path $Destination -Leaf); resuming at $offset bytes."
        # A slow but progressing file should not restart on an arbitrary total time limit.
        $arguments += @('--max-time', '0', '--continue-at', '-', '--header', "If-Match: `"$ResumeETag`"")
    } else { $arguments += '--silent' }
    if ($JsonBody) {
        $arguments += @('--header', 'Content-Type: application/json', '--data-binary', '@-')
        $JsonBody | & curl.exe @arguments $uri.AbsoluteUri
    } else { & curl.exe @arguments $uri.AbsoluteUri }
    if ($LASTEXITCODE -ne 0) {
        throw "Download failed ($LASTEXITCODE): $($uri.GetLeftPart([UriPartial]::Path)). Partial data kept at $partial; rerun to resume."
    }
    if ($ResumeETag -and (Get-FileHash -LiteralPath $partial -Algorithm MD5).Hash -ne $ResumeETag) {
        throw "Download checksum mismatch; not importing: $partial"
    }
    Move-Item -LiteralPath $partial -Destination $Destination -Force
}

function Get-SafePath([string]$Root, [string]$Relative) {
    if ([string]::IsNullOrWhiteSpace($Relative) -or $Relative -match '[:\\]' -or
        $Relative.StartsWith('/') -or ($Relative.Split('/') -contains '..')) {
        throw "Unsafe archive path: $Relative"
    }
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    $result = [IO.Path]::GetFullPath((Join-Path $Root $Relative))
    if (!$result.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes destination: $Relative"
    }
    # Refuse junctions/symlinks as well as textual traversal.
    for ($node = $result; $node; $node = Split-Path $node -Parent) {
        if ((Test-Path -LiteralPath $node) -and
            ((Get-Item -LiteralPath $node -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Reparse point is not allowed: $node"
        }
    }
    return $result
}

function Test-RemoteFile([string]$Path, $Expected) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { return $false }
    if ((Get-Item -LiteralPath $Path).Length -ne [long]$Expected.size) { return $false }
    # These single-part S3 ETags are MD5 hashes; multipart ETags are not.
    $etag = ([string]$Expected.etag).Trim('"')
    if ($etag -match '^[a-fA-F0-9]{32}$') {
        return (Get-FileHash -LiteralPath $Path -Algorithm MD5).Hash -eq $etag
    }
    return $false
}

$catalogPath = Join-Path $DownloadRoot 'hoyotoon-gi-catalog.json'
if ($RefreshCatalog -or !(Test-Path $catalogPath) -or
    (Get-Item $catalogPath).LastWriteTimeUtc -lt [DateTime]::UtcNow.AddDays(-1)) {
    Write-Host 'Querying HoyoToon character catalog...'
    Invoke-Download "$site/api/catalog?game=gi&type=characters" $catalogPath
}
$catalog = Get-Content -Raw -Encoding UTF8 $catalogPath | ConvertFrom-Json
$matches = @($catalog.snapshot.page.items | Where-Object {
    $Character -in @($_.characterId, $_.displayName, $_.internalName, $_.primaryArtName,
        $_.externalRefs.nanoka.summary.translations.zh) + @($_.aliases)
})
if ($matches.Count -ne 1) { throw "Expected one exact character match for '$Character', found $($matches.Count)." }
$row = $matches[0]
$slug = ([string]$row.internalName).ToLowerInvariant()
if ($slug -notmatch '^[a-z][a-z0-9_-]*$') { throw "Unsafe character folder name: $slug" }
$indexPath = Join-Path $DownloadRoot "$slug-files.json"
Invoke-Download "$site/api/remote-assets?game=gi&type=characters&ids=$($row.characterId)" $indexPath
$index = Get-Content -Raw -Encoding UTF8 $indexPath | ConvertFrom-Json
$variants = @($index.files | ForEach-Object { ($_.path -split '/')[2] } | Sort-Object -Unique)
if (!$Variant -and $variants.Count -eq 1) { $Variant = $variants[0] }
if (!$Variant -or $Variant -notin $variants) {
    throw "Specify -Variant. Available variants: $($variants -join ', ')"
}
$files = @($index.files | Where-Object { ($_.path -split '/')[2] -eq $Variant })
if (!$files.Count -or !($files | Where-Object { $_.path -like '*.fbx' })) { throw 'No downloadable FBX in this variant.' }
$channels = @($files | ForEach-Object { if ($_.channel) { $_.channel } else { 'live' } } | Sort-Object -Unique)
if ($channels.Count -ne 1) { throw 'Cannot combine files from different release channels.' }
$variantSlug = ($Variant.ToLowerInvariant() -replace "[^a-z0-9]+", '-').Trim('-')
if (!$variantSlug) { throw 'Variant cannot be converted to a safe folder name.' }
$name = "$slug-$variantSlug"
$assetFolder = "Assets/Art/Model/Character/$name"
$destination = Join-Path $ProjectRoot $assetFolder
$extracted = Join-Path $DownloadRoot $name
$zipPath = Join-Path $DownloadRoot "$name.zip"
$allowed = @('.fbx', '.json', '.png', '.tga', '.jpg', '.jpeg', '.dds', '.tif', '.tiff', '.bmp', '.exr', '.txt')
$expected = @{}
foreach ($file in $files) {
    $relative = (($file.path -split '/') | Select-Object -Skip 3) -join '/'
    $null = Get-SafePath $extracted $relative
    $null = Get-SafePath $destination $relative
    if ([IO.Path]::GetExtension($relative).ToLowerInvariant() -notin $allowed) { throw "Unsupported asset type: $relative" }
    if ($expected.ContainsKey($relative)) { throw "Duplicate path: $relative" }
    $expected[$relative] = $file
}
Write-Host "$($row.displayName) / $Variant : $($files.Count) files -> $assetFolder"
if ($PlanOnly) {
    [pscustomobject]@{ Character = $row.internalName; Variant = $Variant; Files = $files.Count; AssetFolder = $assetFolder }
    return
}

# Validated unpacked files are the resume point, never a half-written ZIP or download.
$missing = @($expected.Keys | Where-Object { !(Test-RemoteFile (Get-SafePath $extracted $_) $expected[$_]) })
if ($missing.Count) {
    $request = @{
        anonymousId = [guid]::NewGuid().ToString(); game = 'gi'; channel = $channels[0]; name = $name; intent = 'bundle'
        files = @($expected.Keys | Sort-Object | ForEach-Object { @{ path = $expected[$_].path; archivePath = $_ } })
    } | ConvertTo-Json -Depth 5 -Compress
    $planPath = Join-Path $DownloadRoot "$name-download-plan.json"
    $previousPlan = if (Test-Path -LiteralPath $planPath) {
        Get-Content -Raw -Encoding UTF8 $planPath | ConvertFrom-Json
    }
    Invoke-Download "$site/api/remote-assets/download" $planPath $request
    $plan = Get-Content -Raw -Encoding UTF8 $planPath | ConvertFrom-Json
    New-Item -ItemType Directory -Force $extracted | Out-Null
    if ($plan.kind -eq 'archive') {
        Invoke-Download $plan.url $zipPath -ResumeETag $plan.etag -PreviousETag $previousPlan.etag
        Write-Host 'ZIP verified. Extracting model package...'
        $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
        try {
            $seen = @{}
            foreach ($entry in $archive.Entries) {
                if (!$entry.Name) { continue }
                $relative = $entry.FullName
                $target = Get-SafePath $extracted $relative
                if (!$expected.ContainsKey($relative) -or $seen.ContainsKey($relative) -or
                    $entry.Length -ne [long]$expected[$relative].size) { throw "Unexpected ZIP entry: $relative" }
                $seen[$relative] = $true
                if (Test-RemoteFile $target $expected[$relative]) { continue }
                New-Item -ItemType Directory -Force (Split-Path $target -Parent) | Out-Null
                [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, "$target.part", $true)
                Move-Item -LiteralPath "$target.part" -Destination $target -Force
            }
        } finally { $archive.Dispose() }
    } elseif ($plan.kind -eq 'files') {
        # The website also creates ZIPs client-side when a prepared archive is unavailable.
        foreach ($file in $plan.files) {
            $relative = [string]$file.archivePath
            if (!$expected.ContainsKey($relative)) { throw "Unexpected download file: $relative" }
            $target = Get-SafePath $extracted $relative
            if (Test-RemoteFile $target $expected[$relative]) { continue }
            Write-Host "Downloading $relative"
            New-Item -ItemType Directory -Force (Split-Path $target -Parent) | Out-Null
            Invoke-Download $file.url $target -ResumeETag $expected[$relative].etag
        }
    } else { throw "Unknown download plan kind: $($plan.kind)" }
}

# Check the complete package and all project conflicts before writing anything into Assets.
foreach ($relative in $expected.Keys) {
    $source = Get-SafePath $extracted $relative
    if (!(Test-RemoteFile $source $expected[$relative])) { throw "Size/ETag verification failed: $relative" }
    $target = Get-SafePath $destination $relative
    if ((Test-Path -LiteralPath $target) -and
        (Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
        throw "Project file differs; no overwrite performed: $target"
    }
}
if (!(Test-Path $zipPath) -or ($missing.Count -and $plan.kind -eq 'files')) {
    # Build only from verified files; exclude any leftover .part files from failed attempts.
    $stream = [IO.File]::Open("$zipPath.part", [IO.FileMode]::Create)
    $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($relative in $expected.Keys) {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,
                (Get-SafePath $extracted $relative), $relative) | Out-Null
        }
    } finally { $archive.Dispose() }
    Move-Item -LiteralPath "$zipPath.part" -Destination $zipPath -Force
}
$copied = 0
foreach ($relative in $expected.Keys) {
    $target = Get-SafePath $destination $relative
    if (Test-Path -LiteralPath $target) { continue }
    New-Item -ItemType Directory -Force (Split-Path $target -Parent) | Out-Null
    Copy-Item -LiteralPath (Get-SafePath $extracted $relative) -Destination $target
    $copied++
}
$result = [pscustomobject]@{
    Character = $row.internalName; Variant = $Variant; Archive = $zipPath; Extracted = $extracted
    AssetFolder = $assetFolder; CopiedFiles = $copied; VerifiedFiles = $expected.Count
    ModelAssets = @($expected.Keys | Where-Object { $_ -like '*.fbx' } | Sort-Object | ForEach-Object { "$assetFolder/$_" })
}
[IO.File]::WriteAllText((Join-Path $DownloadRoot "$name-import.json"), ($result | ConvertTo-Json -Depth 4), $utf8)
Write-Host "Imported $copied new files. Unity Auto Refresh will import Assets; no Setup or onboarding was run."
$result
