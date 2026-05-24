param(
    [string]$ApiBaseUrl = "http://localhost:5000",
    [string]$WebUiBaseUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

Write-Step "Checking backend API"
$health = Invoke-RestMethod -Uri "$ApiBaseUrl/health" -Method Get
Write-Host "API health: $($health | ConvertTo-Json -Compress)"

Write-Step "Checking Web UI HTTP response"
$response = Invoke-WebRequest -Uri $WebUiBaseUrl -Method Get

if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) {
    throw "Web UI returned status code $($response.StatusCode)."
}

$content = $response.Content

if ($content -notmatch "Video Lecture RAG Assistant") {
    throw "Web UI page does not look like the expected VideoRag page."
}

if ($content -notmatch "Rutube" -and $content -notmatch "VK" -and $content -notmatch "ссыл") {
    Write-Warning "The page was reachable, but URL ingest form text was not detected. Check the page manually."
}

Write-Host ""
Write-Host "[OK] Web UI smoke check passed. Now test URL ingest manually in the browser." -ForegroundColor Green
Write-Host "Open: $WebUiBaseUrl"
