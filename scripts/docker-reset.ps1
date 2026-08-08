# ERP AI Copilot — Docker Reset Script (Deletes Volumes)

Write-Host "===============================================================" -ForegroundColor Red
Write-Host " WARNING: This will remove all Docker containers and volumes!" -ForegroundColor Red
Write-Host " All downloaded Ollama models and SQLite data will be deleted. " -ForegroundColor Red
Write-Host "===============================================================" -ForegroundColor Red

$confirmation = Read-Host "Are you sure you want to proceed with full reset? (y/N)"
if ($confirmation -eq "y" -or $confirmation -eq "Y") {
    Write-Host "Removing containers and volumes..." -ForegroundColor Yellow
    docker compose down -v
    Write-Host "Reset completed cleanly." -ForegroundColor Green
} else {
    Write-Host "Reset operation cancelled." -ForegroundColor Gray
}
