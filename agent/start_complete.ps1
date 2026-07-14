# Complete Agent UI Startup Script
# This script validates everything and starts the UI

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host " QA Agent UI - Complete Startup" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# === Pre-flight Checks ===

Write-Host "[1/6] Checking Python..." -ForegroundColor Yellow
try {
    $pythonVer = python --version 2>&1
    Write-Host "      ? $pythonVer" -ForegroundColor Green
} catch {
    Write-Host "      ? Python not found!" -ForegroundColor Red
    Write-Host "      Install Python and add to PATH" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "[2/6] Checking Claude Code CLI..." -ForegroundColor Yellow
if (Get-Command claude -ErrorAction SilentlyContinue) {
    Write-Host "      ? Claude Code CLI found" -ForegroundColor Green
} else {
    Write-Host "      ? Claude Code CLI not found on PATH!" -ForegroundColor Red
    Write-Host "      Install it with: npm install -g @anthropic-ai/claude-code" -ForegroundColor Yellow
    Write-Host "      Then run 'claude' once from a terminal to sign in." -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "[3/6] Checking Python dependencies..." -ForegroundColor Yellow
try {
    python -c "import aiohttp" 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "      ? aiohttp installed" -ForegroundColor Green
    } else {
        Write-Host "      ! Installing aiohttp..." -ForegroundColor Yellow
        pip install aiohttp -q
        Write-Host "      ? aiohttp installed" -ForegroundColor Green
    }
} catch {
    Write-Host "      ! Installing aiohttp..." -ForegroundColor Yellow
    pip install aiohttp
}

Write-Host ""
Write-Host "[4/6] Checking syntax..." -ForegroundColor Yellow
Push-Location $PSScriptRoot
$syntaxCheck = python -m py_compile agent_runner.py 2>&1
Pop-Location

if ($LASTEXITCODE -ne 0) {
    Write-Host "      ? Syntax error in agent_runner.py" -ForegroundColor Red
    Write-Host "      $syntaxCheck" -ForegroundColor Red
    exit 1
} else {
    Write-Host "      ? No syntax errors" -ForegroundColor Green
}

Write-Host ""
Write-Host "[5/6] Checking MCPBridge..." -ForegroundColor Yellow
try {
    $health = Invoke-WebRequest -Uri "http://localhost:5555/health" -UseBasicParsing -TimeoutSec 2 2>$null
    if ($health.StatusCode -eq 200) {
        Write-Host "      ? MCPBridge is running" -ForegroundColor Green
    }
} catch {
    Write-Host "      ? MCPBridge NOT running!" -ForegroundColor Red
    Write-Host ""
    Write-Host "      Please start MCPBridge first:" -ForegroundColor Yellow
    Write-Host "      1. Open another PowerShell window" -ForegroundColor Gray
    Write-Host "      2. cd path\to\your\clone" -ForegroundColor Gray
    Write-Host "      3. .\start_all.ps1" -ForegroundColor Gray
    Write-Host "      4. Wait for 'MCPBridge API is running' message" -ForegroundColor Gray
    Write-Host "      5. Then run this script again" -ForegroundColor Gray
    Write-Host ""
    exit 1
}

Write-Host ""
Write-Host "[6/6] Checking port 8080..." -ForegroundColor Yellow
$portInUse = Get-NetTCPConnection -LocalPort 8080 -ErrorAction SilentlyContinue
if ($portInUse) {
    Write-Host "      ! Port 8080 is in use" -ForegroundColor Yellow
    Write-Host "      Attempting to free port..." -ForegroundColor Yellow
    $proc = $portInUse | Select-Object -ExpandProperty OwningProcess -First 1
    try {
        Stop-Process -Id $proc -Force
        Start-Sleep -Seconds 2
        Write-Host "      ? Port 8080 freed" -ForegroundColor Green
    } catch {
        Write-Host "      ? Could not free port 8080" -ForegroundColor Red
        Write-Host "      Please close the application using port 8080" -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Host "      ? Port 8080 available" -ForegroundColor Green
}

# === All checks passed ===

Write-Host ""
Write-Host "=========================================" -ForegroundColor Green
Write-Host " All checks passed! Starting UI..." -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
Write-Host ""
Write-Host "UI URL:  http://localhost:8080" -ForegroundColor Cyan
Write-Host "         Browser will open automatically" -ForegroundColor Gray
Write-Host ""
Write-Host "Press Ctrl+C to stop the server" -ForegroundColor Yellow
Write-Host ""

# === Start UI Server ===

Push-Location $PSScriptRoot
Start-Sleep -Seconds 1

# Start server and open browser after 2 seconds
Start-Job -ScriptBlock {
    Start-Sleep -Seconds 2
    Start-Process "msedge.exe" "http://localhost:8080"
} | Out-Null

python agent_web_server.py

Pop-Location
