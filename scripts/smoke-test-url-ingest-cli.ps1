param(
    [Parameter(Mandatory = $true)]
    [string]$Url,

    [string]$Title = "URL ingest CLI smoke test",

    [string]$Language = "ru",

    [string]$TranscriptionProvider = "faster-whisper",

    [string]$TranscriptionModel = "small",

    [string]$Project = ".\VideoLectureRagAssistant.csproj",

    [switch]$SkipDependencyChecks
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Require-Command([string]$Name, [string]$InstallHint) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw "$Name was not found in PATH. $InstallHint"
    }
}

if ([string]::IsNullOrWhiteSpace($Url)) {
    throw "Url is required."
}

if (-not $SkipDependencyChecks) {
    Write-Step "Checking local dependencies"
    Require-Command "dotnet" "Install .NET 8 SDK."
    Require-Command "yt-dlp" "Install it with: python -m pip install -U yt-dlp"
    Require-Command "ffmpeg" "Install FFmpeg and add it to PATH."
}

Write-Step "Building solution/project"
dotnet build

Write-Step "Running URL audio ingest through CLI"
dotnet run --project $Project -- ingest-url $Url `
    --title $Title `
    --language $Language `
    --transcription-provider $TranscriptionProvider `
    --transcription-model $TranscriptionModel `
    --overwrite true

if ($LASTEXITCODE -ne 0) {
    throw "CLI ingest-url failed with exit code $LASTEXITCODE."
}

Write-Step "Checking expected local artifacts"
if (-not (Test-Path ".\data\downloads\audio")) {
    throw "data/downloads/audio was not created."
}

$audioFiles = Get-ChildItem ".\data\downloads\audio" -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending

if ($audioFiles.Count -eq 0) {
    throw "No downloaded audio file was found in data/downloads/audio."
}

$transcripts = Get-ChildItem ".\data\transcripts" -Filter "*.transcript.json" -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending

if ($transcripts.Count -eq 0) {
    throw "No transcript file was found in data/transcripts."
}

Write-Host ""
Write-Host "[OK] CLI URL audio ingest smoke test passed." -ForegroundColor Green
Write-Host "Latest audio: $($audioFiles[0].FullName)"
Write-Host "Latest transcript: $($transcripts[0].FullName)"
