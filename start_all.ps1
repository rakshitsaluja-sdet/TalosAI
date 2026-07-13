# Start all components for MCPBridge automation
# No admin rights required

param(
    [switch]$UseOllama = $false,
    [string]$Model = "gpt-4o"
)

Write-Host "=== MCPBridge Automation Startup ===" -ForegroundColor Cyan
Write-Host ""

# Set working directory to script location
$repoRoot = $PSScriptRoot
Set-Location $repoRoot

Write-Host "Repository root: $repoRoot" -ForegroundColor Gray
Write-Host ""

# Verify environment variables
Write-Host "Checking environment variables..." -ForegroundColor Yellow

if ($UseOllama) {
    Write-Host "? Using Ollama (offline mode)" -ForegroundColor Green
} else {
    if (-not $env:GITHUB_TOKEN) {
        Write-Host "? GITHUB_TOKEN not found!" -ForegroundColor Red
        Write-Host "   Set it with:" -ForegroundColor Yellow
        Write-Host "   `$env:GITHUB_TOKEN = 'your_token_here'" -ForegroundColor Gray
        Write-Host "   [System.Environment]::SetEnvironmentVariable('GITHUB_TOKEN', 'your_token', 'User')" -ForegroundColor Gray
        Write-Host ""
        Write-Host "   Or use Ollama mode: .\start_all.ps1 -UseOllama" -ForegroundColor Yellow
        exit 1
    }
    Write-Host "? GITHUB_TOKEN configured" -ForegroundColor Green
}

# Check if ports are available
Write-Host ""
Write-Host "Checking port availability..." -ForegroundColor Yellow

$portsInUse = @()
$requiredPorts = @(5555, 8000)

foreach ($port in $requiredPorts) {
    $connection = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
    if ($connection) {
        $portsInUse += $port
    }
}

if ($portsInUse.Count -gt 0) {
    Write-Host "??  Ports in use: $($portsInUse -join ', ')" -ForegroundColor Yellow
    Write-Host "   Attempting to stop existing processes..." -ForegroundColor Yellow
    
    foreach ($port in $portsInUse) {
        $processes = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue | 
                     Select-Object -ExpandProperty OwningProcess -Unique
        foreach ($proc in $processes) {
            if ($proc) {
                try {
                    Stop-Process -Id $proc -Force -ErrorAction SilentlyContinue
                    Write-Host "   Stopped process $proc on port $port" -ForegroundColor Yellow
                } catch {
                    Write-Host "   Could not stop process $proc" -ForegroundColor Red
                }
            }
        }
    }
    Start-Sleep -Seconds 2
}

Write-Host "? Ports available" -ForegroundColor Green

# Create logs directory
$logDir = Join-Path $repoRoot "logs"
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir | Out-Null
    Write-Host "Created logs directory: $logDir" -ForegroundColor Gray
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"

# Start MCPBridge (C# API)
Write-Host ""
Write-Host "Starting MCPBridge API (C#)..." -ForegroundColor Yellow

$mcpBridgePath = Join-Path $repoRoot "MCPBridge"
$mcpBridgeLog = Join-Path $logDir "mcpbridge_$timestamp.log"

if (-not (Test-Path $mcpBridgePath)) {
    Write-Host "? MCPBridge directory not found: $mcpBridgePath" -ForegroundColor Red
    exit 1
}

$mcpBridgeJob = Start-Job -ScriptBlock {
    param($path, $logFile)
    Set-Location $path
    dotnet run 2>&1 | Tee-Object -FilePath $logFile
} -ArgumentList $mcpBridgePath, $mcpBridgeLog

Write-Host "   Job ID: $($mcpBridgeJob.Id)" -ForegroundColor Gray
Write-Host "   Log: $mcpBridgeLog" -ForegroundColor Gray

# Wait for MCPBridge to start
Write-Host "   Waiting for API to be ready..." -ForegroundColor Yellow
$maxAttempts = 30
$attempt = 0
$apiReady = $false

while ($attempt -lt $maxAttempts -and -not $apiReady) {
    Start-Sleep -Seconds 1
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5555/health" -UseBasicParsing -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            $apiReady = $true
            Write-Host "? MCPBridge API is running on http://localhost:5555" -ForegroundColor Green
        }
    } catch {
        $attempt++
        Write-Host "." -NoNewline -ForegroundColor Gray
    }
}
Write-Host ""

if (-not $apiReady) {
    Write-Host "? MCPBridge API failed to start!" -ForegroundColor Red
    Write-Host "   Check log: $mcpBridgeLog" -ForegroundColor Yellow
    
    if (Test-Path $mcpBridgeLog) {
        Write-Host ""
        Write-Host "Last 20 lines of log:" -ForegroundColor Yellow
        Get-Content $mcpBridgeLog -Tail 20
    }
    
    Stop-Job -Id $mcpBridgeJob.Id -ErrorAction SilentlyContinue
    Remove-Job -Id $mcpBridgeJob.Id -ErrorAction SilentlyContinue
    exit 1
}

# Start MCP Server (Python) - Optional
$mcpServerPath = Join-Path $repoRoot "mcp_server"

if (Test-Path $mcpServerPath) {
    Write-Host ""
    Write-Host "Starting MCP Server (Python)..." -ForegroundColor Yellow
    $serverLog = Join-Path $logDir "mcp_server_$timestamp.log"
    
    $mcpServerJob = Start-Job -ScriptBlock {
        param($path, $logFile)
        Set-Location $path
        python server.py 2>&1 | Tee-Object -FilePath $logFile
    } -ArgumentList $mcpServerPath, $serverLog
    
    Write-Host "   Job ID: $($mcpServerJob.Id)" -ForegroundColor Gray
    Write-Host "   Log: $serverLog" -ForegroundColor Gray
    Write-Host "? MCP Server started" -ForegroundColor Green
    
    Start-Sleep -Seconds 2
}

# Start Agent (Python)
$agentPath = Join-Path $repoRoot "agent"

if (Test-Path $agentPath) {
    Write-Host ""
    
    if ($UseOllama) {
        Write-Host "Starting Agent Runner (Ollama - Offline Mode)..." -ForegroundColor Yellow
        $agentScript = "agent_runner_ollama.py"
    } else {
        Write-Host "Starting Agent Runner (GitHub Models)..." -ForegroundColor Yellow
        $agentScript = "agent_runner.py"
    }
    
    $agentLog = Join-Path $logDir "agent_$timestamp.log"
    
    $agentJob = Start-Job -ScriptBlock {
        param($path, $script, $logFile)
        Set-Location $path
        python $script 2>&1 | Tee-Object -FilePath $logFile
    } -ArgumentList $agentPath, $agentScript, $agentLog
    
    Write-Host "   Job ID: $($agentJob.Id)" -ForegroundColor Gray
    Write-Host "   Log: $agentLog" -ForegroundColor Gray
    Write-Host "? Agent Runner started" -ForegroundColor Green
}

# Summary
Write-Host ""
Write-Host "=== All Services Started ===" -ForegroundColor Cyan
Write-Host ""

Write-Host "Running Jobs:" -ForegroundColor Yellow
Get-Job | Format-Table -AutoSize

Write-Host ""
Write-Host "Endpoints:" -ForegroundColor Yellow
Write-Host "  MCPBridge API: http://localhost:5555" -ForegroundColor Gray
Write-Host "  Health Check:  http://localhost:5555/health" -ForegroundColor Gray
Write-Host "  Tools List:    http://localhost:5555/tools" -ForegroundColor Gray

Write-Host ""
Write-Host "Logs Directory: $logDir" -ForegroundColor Yellow

Write-Host ""
Write-Host "To view logs in real-time:" -ForegroundColor Yellow
Write-Host "  Get-Content '$mcpBridgeLog' -Wait -Tail 20" -ForegroundColor Gray

Write-Host ""
Write-Host "To stop all services:" -ForegroundColor Yellow
Write-Host "  Get-Job | Stop-Job; Get-Job | Remove-Job" -ForegroundColor Gray

Write-Host ""
Write-Host "Press Ctrl+C to stop monitoring..." -ForegroundColor Yellow
Write-Host ""

# Monitor jobs
try {
    while ($true) {
        Start-Sleep -Seconds 5
        
        $runningJobs = Get-Job | Where-Object { $_.State -eq 'Running' }
        
        if ($runningJobs.Count -eq 0) {
            Write-Host ""
            Write-Host "??  All jobs have stopped!" -ForegroundColor Red
            break
        }
    }
} finally {
    Write-Host ""
    Write-Host "Cleaning up jobs..." -ForegroundColor Yellow
    Get-Job | Stop-Job -ErrorAction SilentlyContinue
    Get-Job | Remove-Job -Force -ErrorAction SilentlyContinue
    Write-Host "? Cleanup complete" -ForegroundColor Green
}