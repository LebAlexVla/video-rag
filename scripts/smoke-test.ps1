$ErrorActionPreference = "Stop"

Write-Host "1. Health check..."
$health = Invoke-RestMethod -Uri "http://localhost:5000/health" -Method Get
$health | ConvertTo-Json -Depth 5

Write-Host ""
Write-Host "2. Ask check..."
$body = @{
    question = "What is this transcript for?"
    topK = 5
    minScore = 0.1
} | ConvertTo-Json

$response = Invoke-RestMethod `
    -Uri "http://localhost:5000/ask" `
    -Method Post `
    -ContentType "application/json" `
    -Body $body

$response | ConvertTo-Json -Depth 10