param(
    [string]$ComposeFile = "docker-compose.yml",
    [switch]$SkipBuild,
    [switch]$DownOnExit,
    [int]$TimeoutSeconds = 300
)

$ErrorActionPreference = "Stop"

function Invoke-HealthCheck {
    param(
        [string]$Name,
        [string]$Url,
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = $null

    while ((Get-Date) -lt $deadline) {
        try {
            Invoke-RestMethod -Uri $Url -TimeoutSec 5 | Out-Null
            Write-Host "[OK] $Name $Url"
            return
        }
        catch {
            $lastError = $_.Exception.Message
            Start-Sleep -Seconds 5
        }
    }

    throw "Health check failed for $Name ($Url): $lastError"
}

try {
    if (!(Test-Path -LiteralPath $ComposeFile)) {
        throw "Compose file not found: $ComposeFile"
    }

    $upArgs = @("compose", "-f", $ComposeFile, "up", "-d")
    if (-not $SkipBuild) {
        $upArgs += "--build"
    }

    Write-Host "[INFO] Starting Docker stack with $ComposeFile"
    docker @upArgs

    Invoke-HealthCheck -Name "API liveness" -Url "http://localhost:5000/health" -TimeoutSeconds $TimeoutSeconds
    Invoke-HealthCheck -Name "Document parser" -Url "http://localhost:8000/health" -TimeoutSeconds $TimeoutSeconds
    Invoke-HealthCheck -Name "Embedding service" -Url "http://localhost:8010/health" -TimeoutSeconds $TimeoutSeconds
    Invoke-HealthCheck -Name "API readiness" -Url "http://localhost:5000/health/ready" -TimeoutSeconds $TimeoutSeconds

    Write-Host "[OK] Docker smoke test completed."
}
catch {
    Write-Error $_
    Write-Host "[INFO] Recent API logs:"
    docker compose -f $ComposeFile logs --tail 120 api
    exit 1
}
finally {
    if ($DownOnExit) {
        Write-Host "[INFO] Stopping Docker stack."
        docker compose -f $ComposeFile down
    }
}
