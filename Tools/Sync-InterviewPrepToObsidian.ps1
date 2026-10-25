[CmdletBinding()]
param(
    [Parameter()]
    [string]$SourceRoot,

    [Parameter()]
    [string]$DestinationRoot,

    [Parameter()]
    [switch]$CheckOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$manifestFileName = '.interview-prep-sync.json'

if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
    $SourceRoot = Join-Path $PSScriptRoot '..\Docs\interview-prep'
}

if ([string]::IsNullOrWhiteSpace($DestinationRoot)) {
    # Keep this script compatible with Windows PowerShell 5.1, which can misread
    # non-ASCII literals in UTF-8 files without a BOM.
    $documentSuffix = [string]([char]0x6587) + [char]0x6863
    $reviewFolder = [string]([char]0x9762) + [char]0x8BD5 + [char]0x590D +
        [char]0x76D8 + [char]0x6587 + [char]0x6863
    $obsidianProjectRoot = Join-Path 'D:\Obsidian-1.9.14\MyObjects\Unity' ('RPG_Demo' + $documentSuffix)
    $DestinationRoot = Join-Path $obsidianProjectRoot $reviewFolder
}

function Get-NormalizedRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [bool]$MustExist
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)

    if ($MustExist -and -not (Test-Path -LiteralPath $fullPath -PathType Container)) {
        throw "Directory does not exist: $fullPath"
    }

    return $fullPath.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
}

function Get-Sha256 {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Get-SafeDestinationPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    if ([string]::IsNullOrWhiteSpace($RelativePath) -or
        [System.IO.Path]::IsPathRooted($RelativePath)) {
        throw "Invalid relative path in sync state: '$RelativePath'"
    }

    $nativeRelativePath = $RelativePath.Replace(
        '/',
        [System.IO.Path]::DirectorySeparatorChar)
    $candidate = [System.IO.Path]::GetFullPath((Join-Path $Root $nativeRelativePath))
    $rootPrefix = $Root + [System.IO.Path]::DirectorySeparatorChar

    if (-not $candidate.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Relative path escapes the destination root: '$RelativePath'"
    }

    return $candidate
}

function New-Action {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Kind,

        [Parameter(Mandatory = $true)]
        [string]$RelativePath,

        [Parameter()]
        [string]$SourcePath,

        [Parameter()]
        [string]$DestinationPath,

        [Parameter()]
        [string]$Hash
    )

    return [pscustomobject]@{
        Kind = $Kind
        RelativePath = $RelativePath
        SourcePath = $SourcePath
        DestinationPath = $DestinationPath
        Hash = $Hash
    }
}

$source = Get-NormalizedRoot -Path $SourceRoot -MustExist $true
$destination = Get-NormalizedRoot -Path $DestinationRoot -MustExist $false
$manifestPath = Join-Path $destination $manifestFileName
$sourcePrefix = $source + [System.IO.Path]::DirectorySeparatorChar

$sourceFiles = @(Get-ChildItem -LiteralPath $source -Recurse -File | Sort-Object FullName)
$currentByPath = New-Object 'System.Collections.Generic.Dictionary[string,object]' ([System.StringComparer]::OrdinalIgnoreCase)

foreach ($file in $sourceFiles) {
    if (-not $file.FullName.StartsWith($sourcePrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Source enumeration escaped the source root: $($file.FullName)"
    }

    $relativePath = $file.FullName.Substring($sourcePrefix.Length).Replace('\', '/')

    if ($relativePath -eq $manifestFileName) {
        throw "The reserved sync state filename cannot exist in the source: $relativePath"
    }

    if ($currentByPath.ContainsKey($relativePath)) {
        throw "Duplicate source path after Windows case folding: $relativePath"
    }

    $currentByPath.Add($relativePath, [pscustomobject]@{
        RelativePath = $relativePath
        FullName = $file.FullName
        Hash = Get-Sha256 -Path $file.FullName
    })
}

$managedByPath = New-Object 'System.Collections.Generic.Dictionary[string,object]' ([System.StringComparer]::OrdinalIgnoreCase)

if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
    try {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        throw "Cannot parse sync state '$manifestPath': $($_.Exception.Message)"
    }

    if ($null -eq $manifest -or $manifest.manifestVersion -ne 1) {
        throw "Unsupported or missing manifestVersion in '$manifestPath'."
    }

    foreach ($entry in @($manifest.entries)) {
        $relativePath = [string]$entry.relativePath
        $hash = ([string]$entry.sha256).ToUpperInvariant()
        [void](Get-SafeDestinationPath -Root $destination -RelativePath $relativePath)

        if ($hash -notmatch '^[0-9A-F]{64}$') {
            throw "Invalid SHA-256 in sync state for '$relativePath'."
        }

        if ($managedByPath.ContainsKey($relativePath)) {
            throw "Duplicate managed path in sync state: $relativePath"
        }

        $managedByPath.Add($relativePath, [pscustomobject]@{
            RelativePath = $relativePath
            Hash = $hash
        })
    }
}

$actions = New-Object 'System.Collections.Generic.List[object]'
$conflicts = New-Object 'System.Collections.Generic.List[string]'
$unchangedCount = 0

foreach ($current in $currentByPath.Values) {
    $targetPath = Get-SafeDestinationPath -Root $destination -RelativePath $current.RelativePath
    $targetExists = Test-Path -LiteralPath $targetPath -PathType Leaf

    if ($managedByPath.ContainsKey($current.RelativePath)) {
        $previous = $managedByPath[$current.RelativePath]

        if (-not $targetExists) {
            $conflicts.Add("Managed destination file was deleted or moved: $($current.RelativePath)")
            continue
        }

        $targetHash = Get-Sha256 -Path $targetPath

        if ($targetHash -eq $current.Hash) {
            if ($previous.Hash -eq $current.Hash) {
                $unchangedCount++
            }
            else {
                $actions.Add((New-Action -Kind 'AdoptMatchingDestination' -RelativePath $current.RelativePath -SourcePath $current.FullName -DestinationPath $targetPath -Hash $current.Hash))
            }

            continue
        }

        if ($targetHash -ne $previous.Hash) {
            $conflicts.Add("Managed destination file was modified in Obsidian: $($current.RelativePath)")
            continue
        }

        $actions.Add((New-Action -Kind 'Update' -RelativePath $current.RelativePath -SourcePath $current.FullName -DestinationPath $targetPath -Hash $current.Hash))
        continue
    }

    if ($targetExists) {
        $targetHash = Get-Sha256 -Path $targetPath

        if ($targetHash -eq $current.Hash) {
            $actions.Add((New-Action -Kind 'AdoptExistingMatch' -RelativePath $current.RelativePath -SourcePath $current.FullName -DestinationPath $targetPath -Hash $current.Hash))
        }
        else {
            $conflicts.Add("Unmanaged destination collision; existing file was protected: $($current.RelativePath)")
        }
    }
    else {
        $actions.Add((New-Action -Kind 'Add' -RelativePath $current.RelativePath -SourcePath $current.FullName -DestinationPath $targetPath -Hash $current.Hash))
    }
}

foreach ($previous in $managedByPath.Values) {
    if ($currentByPath.ContainsKey($previous.RelativePath)) {
        continue
    }

    $targetPath = Get-SafeDestinationPath -Root $destination -RelativePath $previous.RelativePath

    if (-not (Test-Path -LiteralPath $targetPath -PathType Leaf)) {
        $actions.Add((New-Action -Kind 'PruneMissingManagedEntry' -RelativePath $previous.RelativePath -DestinationPath $targetPath -Hash $previous.Hash))
        continue
    }

    $targetHash = Get-Sha256 -Path $targetPath

    if ($targetHash -ne $previous.Hash) {
        $conflicts.Add("Source file was removed, but the managed destination copy was modified; destination was protected: $($previous.RelativePath)")
        continue
    }

    $actions.Add((New-Action -Kind 'RemoveManagedFile' -RelativePath $previous.RelativePath -DestinationPath $targetPath -Hash $previous.Hash))
}

if ($conflicts.Count -gt 0) {
    $detail = ($conflicts | Sort-Object) -join [Environment]::NewLine
    throw "Synchronization stopped before making changes because conflicts were found:$([Environment]::NewLine)$detail"
}

if (-not $CheckOnly) {
    if (-not (Test-Path -LiteralPath $destination -PathType Container)) {
        [void](New-Item -ItemType Directory -Path $destination -Force)
    }

    foreach ($action in $actions) {
        switch ($action.Kind) {
            'Add' {
                $parent = Split-Path -Parent $action.DestinationPath
                if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
                    [void](New-Item -ItemType Directory -Path $parent -Force)
                }
                Copy-Item -LiteralPath $action.SourcePath -Destination $action.DestinationPath -Force
            }
            'Update' {
                Copy-Item -LiteralPath $action.SourcePath -Destination $action.DestinationPath -Force
            }
            'RemoveManagedFile' {
                Remove-Item -LiteralPath $action.DestinationPath -Force
            }
            'AdoptExistingMatch' { }
            'AdoptMatchingDestination' { }
            'PruneMissingManagedEntry' { }
            default { throw "Unknown synchronization action: $($action.Kind)" }
        }
    }

    $nextEntries = New-Object 'System.Collections.Generic.List[object]'

    foreach ($current in ($currentByPath.Values | Sort-Object RelativePath)) {
        $targetPath = Get-SafeDestinationPath -Root $destination -RelativePath $current.RelativePath

        if (-not (Test-Path -LiteralPath $targetPath -PathType Leaf)) {
            throw "Post-sync verification failed; destination file is missing: $($current.RelativePath)"
        }

        $targetHash = Get-Sha256 -Path $targetPath

        if ($targetHash -ne $current.Hash) {
            throw "Post-sync SHA-256 mismatch: $($current.RelativePath)"
        }

        $nextEntries.Add([ordered]@{
            relativePath = $current.RelativePath
            sha256 = $current.Hash
        })
    }

    $manifestNeedsWrite = -not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or
        $actions.Count -gt 0

    if ($manifestNeedsWrite) {
        $nextManifest = [ordered]@{
            manifestVersion = 1
            sourceRoot = $source
            destinationRoot = $destination
            updatedAtUtc = [DateTime]::UtcNow.ToString('O')
            entries = $nextEntries
        }
        $manifestJson = $nextManifest | ConvertTo-Json -Depth 5
        $temporaryManifestPath = $manifestPath + '.tmp'
        $utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($temporaryManifestPath, $manifestJson + [Environment]::NewLine, $utf8WithoutBom)
        Move-Item -LiteralPath $temporaryManifestPath -Destination $manifestPath -Force
    }
}

$counts = @{}
foreach ($action in $actions) {
    if (-not $counts.ContainsKey($action.Kind)) {
        $counts[$action.Kind] = 0
    }
    $counts[$action.Kind]++
}

$summary = [pscustomobject]@{
    SourceRoot = $source
    DestinationRoot = $destination
    CheckOnly = [bool]$CheckOnly
    SourceFileCount = $sourceFiles.Count
    Added = [int]($counts['Add'])
    Updated = [int]($counts['Update'])
    Removed = [int]($counts['RemoveManagedFile'])
    Adopted = [int]($counts['AdoptExistingMatch']) + [int]($counts['AdoptMatchingDestination'])
    PrunedMissingEntries = [int]($counts['PruneMissingManagedEntry'])
    Unchanged = $unchangedCount
    Conflicts = 0
}

$summary
