#Requires -Version 5.1
<#
.SYNOPSIS
  Stage + optionally upload RaidersPlaceSandbags to Steam Workshop.
#>
[CmdletBinding(DefaultParameterSetName = 'Stage')]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $ChangeNote,

    [Parameter(ParameterSetName = 'Stage')]
    [switch] $StageOnly,

    [Parameter(ParameterSetName = 'Upload')]
    [switch] $Upload,

    [string] $SteamCmdPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RimWorldAppId = '294100'
$DllName = 'TSA_RaidersPlaceSandbags.dll'
$AllowFolders = @('About', 'Assemblies', 'Textures')

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$StageRoot = Join-Path $RepoRoot 'tools\_workshop_stage'
$ContentDir = Join-Path $StageRoot 'content'
$VdfPath = Join-Path $StageRoot 'upload.vdf'
$PublishedIdPath = Join-Path $RepoRoot 'About\PublishedFileId.txt'
$DllSource = Join-Path $RepoRoot "Assemblies\$DllName"

function Write-Info([string] $Message) { Write-Host $Message }

function Get-PublishedFileId {
    if (-not (Test-Path -LiteralPath $PublishedIdPath)) {
        throw "Missing PublishedFileId at: $PublishedIdPath"
    }
    $id = (Get-Content -LiteralPath $PublishedIdPath -Raw).Trim()
    if ($id -notmatch '^\d+$') {
        throw "Invalid PublishedFileId '$id' in $PublishedIdPath"
    }
    return $id
}

function Clear-Stage {
    if (Test-Path -LiteralPath $ContentDir) {
        Remove-Item -LiteralPath $ContentDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $ContentDir -Force | Out-Null
}

function Copy-TreeOptional([string] $RelativeFolder) {
    $src = Join-Path $RepoRoot $RelativeFolder
    if (-not (Test-Path -LiteralPath $src)) { return }
    $dst = Join-Path $ContentDir $RelativeFolder
    New-Item -ItemType Directory -Path $dst -Force | Out-Null
    $robolog = Join-Path $env:TEMP ("sandbags-workshop-robocopy-{0}.log" -f [guid]::NewGuid().ToString('N'))
    & robocopy $src $dst /E /NFL /NDL /NJH /NJS /NP /XD .git .vs bin obj /XF Thumbs.db Desktop.ini .DS_Store | Out-File -FilePath $robolog -Encoding utf8
    $code = $LASTEXITCODE
    Remove-Item -LiteralPath $robolog -Force -ErrorAction SilentlyContinue
    if ($code -ge 8) { throw "robocopy failed for $RelativeFolder (exit $code)" }
}

function Stage-Content {
    Clear-Stage
    Copy-TreeOptional 'About'
    Copy-TreeOptional 'Textures'

    if (-not (Test-Path -LiteralPath $DllSource)) {
        throw "Missing assembly: $DllSource"
    }
    $asmDir = Join-Path $ContentDir 'Assemblies'
    New-Item -ItemType Directory -Path $asmDir -Force | Out-Null
    Copy-Item -LiteralPath $DllSource -Destination (Join-Path $asmDir $DllName) -Force
}

function Assert-Stage {
    $actual = @(Get-ChildItem -LiteralPath $ContentDir -Force | ForEach-Object { $_.Name })
    $extra = @($actual | Where-Object { $_ -notin $AllowFolders })
    if ($extra.Count -gt 0) {
        throw "Stage content has unexpected top-level entries: $($extra -join ', ')"
    }
    foreach ($need in @('About', 'Assemblies')) {
        if ($need -notin $actual) { throw "Stage content missing: $need" }
    }

    $asmFiles = @(Get-ChildItem -LiteralPath (Join-Path $ContentDir 'Assemblies') -File -Force)
    if ($asmFiles.Count -ne 1 -or $asmFiles[0].Name -ne $DllName) {
        throw "Assemblies must contain exactly '$DllName'"
    }
    if (-not (Test-Path -LiteralPath (Join-Path $ContentDir 'About\Preview.png'))) {
        throw 'Staged About/Preview.png is missing.'
    }
}

function Escape-VdfString([string] $Value) {
    return ($Value -replace '\\', '\\' -replace '"', '\"')
}

function Write-UploadVdf([string] $PublishedFileId) {
    $preview = Join-Path $ContentDir 'About\Preview.png'
    $contentAbs = (Resolve-Path -LiteralPath $ContentDir).Path
    $previewAbs = (Resolve-Path -LiteralPath $preview).Path
    $note = Escape-VdfString $ChangeNote
    $vdf = @"
"workshopitem"
{
	"appid"		"$RimWorldAppId"
	"publishedfileid"		"$PublishedFileId"
	"contentfolder"		"$contentAbs"
	"previewfile"		"$previewAbs"
	"changenote"		"$note"
}
"@
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($VdfPath, $vdf, $utf8NoBom)
    Write-Info "Wrote VDF: $VdfPath"
}

function Resolve-SteamCmd {
    if ($SteamCmdPath) {
        if (-not (Test-Path -LiteralPath $SteamCmdPath)) { throw "SteamCmdPath not found: $SteamCmdPath" }
        return (Resolve-Path -LiteralPath $SteamCmdPath).Path
    }
    if ($env:STEAMCMD -and (Test-Path -LiteralPath $env:STEAMCMD)) {
        return (Resolve-Path -LiteralPath $env:STEAMCMD).Path
    }
    $fallback = 'C:\Users\Torben Sandforth\Desktop\Spiele\Steamcmd\steamcmd.exe'
    if (Test-Path -LiteralPath $fallback) { return $fallback }
    throw 'SteamCMD not found. Set -SteamCmdPath or $env:STEAMCMD.'
}

function Invoke-SteamUpload([string] $PublishedFileId) {
    if (-not $env:STEAM_USERNAME) {
        throw 'Set $env:STEAM_USERNAME to the Workshop item owner account before -Upload.'
    }
    Write-Warning 'Quit the Steam client before uploading if login fails.'
    Write-Info ("Uploading Workshop item {0} as {1} ..." -f $PublishedFileId, $env:STEAM_USERNAME)
    $cmd = Resolve-SteamCmd
    & $cmd +login $env:STEAM_USERNAME +workshop_build_item $VdfPath +quit
    if ($LASTEXITCODE -ne 0) {
        throw "SteamCMD failed with exit code $LASTEXITCODE"
    }
    Write-Info 'SteamCMD finished.'
}

$doUpload = $Upload.IsPresent
$publishedId = Get-PublishedFileId
Write-Info "Repo: $RepoRoot"
Write-Info "Workshop publishedfileid: $publishedId"
Stage-Content
Assert-Stage
Write-Info '--- staged ---'
Get-ChildItem -LiteralPath $ContentDir -Recurse -File | ForEach-Object {
    Write-Info ("  {0}" -f $_.FullName.Substring($ContentDir.Length + 1))
}
Write-UploadVdf -PublishedFileId $publishedId

if ($doUpload) {
    Invoke-SteamUpload -PublishedFileId $publishedId
}
else {
    Write-Info 'StageOnly complete. Re-run with -Upload to publish.'
}

