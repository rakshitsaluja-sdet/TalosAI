# Test UI Server - Better error handling
Write-Host "========================================="
Write-Host " Testing Agent UI Server"
Write-Host "========================================="
Write-Host ""

# Check Python
Write-Host "Checking Python..." -ForegroundColor Yellow
try {
    $pythonVersion = python --version 2>&1
    Write-Host "? Python found: $pythonVersion" -ForegroundColor Green
} catch {
    Write-Host "? Python not found in PATH" -ForegroundColor Red
    exit 1
}

# Check MCPBridge
Write-Host ""
Write-Host "Checking MCPBridge..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:5555/health" -UseBasicParsing -TimeoutSec 2
    Write-Host "? MCPBridge is running" -ForegroundColor Green
} catch {
    Write-Host "??  MCPBridge is NOT running!" -ForegroundColor Red
    Write-Host "   Start it first with: .\start_all.ps1" -ForegroundColor Yellow
    exit 1
}

# Check aiohttp
Write-Host ""
Write-Host "Checking Python dependencies..." -ForegroundColor Yellow
try {
    python -c "import aiohttp; print('aiohttp OK')" 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "? aiohttp installed" -ForegroundColor Green
    } else {
        Write-Host "? aiohttp not found - installing..." -ForegroundColor Yellow
        pip install aiohttp
    }
} catch {
    Write-Host "? Installing aiohttp..." -ForegroundColor Yellow
    pip install aiohttp
}

# Check for syntax errors
Write-Host ""
Write-Host "Checking for syntax errors..." -ForegroundColor Yellow
Push-Location $PSScriptRoot
$syntaxCheck = python -m py_compile agent_runner.py 2>&1
Pop-Location

if ($LASTEXITCODE -ne 0) {
    Write-Host "? Syntax error in agent_runner.py:" -ForegroundColor Red
    Write-Host $syntaxCheck -ForegroundColor Red
    Write-Host ""
    Write-Host "Please fix the syntax error before running the server." -ForegroundColor Yellow
    exit 1
} else {
    Write-Host "? No syntax errors found" -ForegroundColor Green
}

# Start server
Write-Host ""
Write-Host "Starting UI Server..." -ForegroundColor Cyan
Write-Host "Press Ctrl+C to stop"  -ForegroundColor Gray
Write-Host ""

Push-Location $PSScriptRoot
python agent_web_server.py
Pop-Location
