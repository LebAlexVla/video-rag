param(
    [string]$ApiBaseUrl = "http://localhost:5000",
    [string]$BotProject = ".\clients\VideoRag.TelegramBot\VideoRag.TelegramBot.csproj"
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

Write-Step "Checking backend API"
$health = Invoke-RestMethod -Uri "$ApiBaseUrl/health" -Method Get
Write-Host "API health: $($health | ConvertTo-Json -Compress)"

Write-Step "Building Telegram bot project"
dotnet build $BotProject

if ($LASTEXITCODE -ne 0) {
    throw "Telegram bot project build failed."
}

Write-Host ""
Write-Host "[OK] Telegram bot smoke pre-check passed." -ForegroundColor Green
Write-Host ""
Write-Host "Manual Telegram checks:"
Write-Host "1. Run backend API: dotnet run"
Write-Host "2. Run bot: dotnet run --project $BotProject"
Write-Host "3. Send in Telegram: /health"
Write-Host "4. Send in Telegram: /add <rutube-or-vk-url>"
Write-Host "5. Send in Telegram: /status <jobId>"
Write-Host "6. After success, ask a question by normal message or /ask if implemented."
