param(
    [Parameter(Mandatory = $true)]
    [string]$Url,

    [string]$LectureTitle = "URL ingest API smoke test",

    [string]$ApiBaseUrl = "http://localhost:5000",

    [int]$PollSeconds = 5,

    [int]$MaxPollAttempts = 120
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-JsonPost($Uri, $Body) {
    return Invoke-RestMethod `
        -Uri $Uri `
        -Method Post `
        -ContentType "application/json" `
        -Body ($Body | ConvertTo-Json -Depth 8)
}

if ([string]::IsNullOrWhiteSpace($Url)) {
    throw "Url is required."
}

Write-Step "Checking API health"
$health = Invoke-RestMethod -Uri "$ApiBaseUrl/health" -Method Get
Write-Host "Health: $($health | ConvertTo-Json -Compress)"

Write-Step "Starting URL ingest job"
$startResponse = Invoke-JsonPost "$ApiBaseUrl/ingest/url" @{
    url = $Url
    lectureTitle = $LectureTitle
}

if ([string]::IsNullOrWhiteSpace($startResponse.jobId)) {
    throw "API response does not contain jobId."
}

$jobId = $startResponse.jobId
Write-Host "JobId: $jobId"
Write-Host "Initial status: $($startResponse.status)"

Write-Step "Polling job status"
for ($i = 1; $i -le $MaxPollAttempts; $i++) {
    $status = Invoke-RestMethod -Uri "$ApiBaseUrl/ingest/jobs/$jobId" -Method Get

    Write-Host "[$i/$MaxPollAttempts] status=$($status.status); stage=$($status.stage); message=$($status.message)"

    if ($status.status -eq "Succeeded") {
        Write-Host ""
        Write-Host "[OK] API URL ingest smoke test passed." -ForegroundColor Green
        Write-Host ($status | ConvertTo-Json -Depth 8)
        exit 0
    }

    if ($status.status -eq "Failed") {
        Write-Host ($status | ConvertTo-Json -Depth 8)
        throw "URL ingest job failed: $($status.error)"
    }

    Start-Sleep -Seconds $PollSeconds
}

throw "URL ingest job did not finish after $MaxPollAttempts polling attempts."
