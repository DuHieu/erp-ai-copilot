# ERP AI Copilot — Docker Up Script for Windows PowerShell

$ErrorActionPreference = "Stop"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host " ERP AI Copilot — Launching Docker Stack " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

if (-not (Test-Path ".env")) {
    if (Test-Path ".env.example") {
        Write-Host "Creating .env file from .env.example..." -ForegroundColor Yellow
        Copy-Item ".env.example" ".env"
    }
}

Write-Host "Building and launching Docker containers..." -ForegroundColor Green
docker compose up -d --build

Write-Host "`nChecking container status..." -ForegroundColor Green
docker compose ps

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host " Access Endpoints:" -ForegroundColor Cyan
Write-Host " Web UI:     http://localhost:5001" -ForegroundColor Yellow
Write-Host " Swagger:    http://localhost:5000/swagger" -ForegroundColor Yellow
Write-Host " Health:     http://localhost:5000/health/ready" -ForegroundColor Yellow
Write-Host "=========================================" -ForegroundColor Cyan
