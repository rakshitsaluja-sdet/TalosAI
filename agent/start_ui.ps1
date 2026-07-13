Write-Host "========================================="
Write-Host " QA Agent UI"
Write-Host "========================================="

$UiUrl = "http://localhost:8080"
$McpHealth = "http://localhost:5555/health"

function IsUp($url) {
    try {
        Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2 | Out-Null
        return $true
    } catch {
        return $false
    }
}

Write-Host "?? Checking MCPBridge..."

if (-not (IsUp $McpHealth)) {
    Write-Host "?? MCPBridge not running. Starting it..."

    # Move to TestAutomation folder BEFORE running restart script
    Push-Location ..
    & .\restart_mcpbridge.ps1
    Pop-Location

    Write-Host "? Waiting for MCPBridge to stabilize..."
    Start-Sleep -Seconds 5
} else {
    Write-Host "? MCPBridge already running"
}

Write-Host "?? Starting Agent UI Server..."
Write-Host "   URL: http://localhost:8080"
Write-Host "   Press Ctrl+C to stop"
Write-Host ""

# Start Python server directly (NOT in background)
python agent_web_server.py