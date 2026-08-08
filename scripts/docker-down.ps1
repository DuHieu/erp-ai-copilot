# ERP AI Copilot — Docker Down Script for Windows PowerShell

Write-Host "Stopping ERP AI Copilot Docker containers..." -ForegroundColor Yellow
docker compose down
Write-Host "Containers stopped." -ForegroundColor Green
