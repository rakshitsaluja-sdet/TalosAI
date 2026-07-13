# Complete Restart Script for MCPBridge
# Run from: path\to\your\clone\

Write-Host ""
Write-Host "???????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "  MCPBridge Complete Restart" -ForegroundColor Cyan
Write-Host "???????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host ""

# Step 1: Stop old process
Write-Host "Step 1: Stopping old MCPBridge process..." -ForegroundColor Yellow
$process = Get-Process MCPBridge -ErrorAction SilentlyContinue
if ($process) {
    Stop-Process -Name MCPBridge -Force
    Start-Sleep -Seconds 2
    Write-Host "? Stopped old process" -ForegroundColor Green
} else {
    Write-Host "? No running process found" -ForegroundColor Green
}

Write-Host ""

# Step 2: Clean build
Write-Host "Step 2: Cleaning old build..." -ForegroundColor Yellow
Push-Location MCPBridge
dotnet clean --verbosity quiet
if ($LASTEXITCODE -eq 0) {
    Write-Host "? Clean complete" -ForegroundColor Green
} else {
    Write-Host "? Clean failed" -ForegroundColor Red
    Pop-Location
    exit 1
}

Write-Host ""

# Step 3: Build
Write-Host "Step 3: Building MCPBridge..." -ForegroundColor Yellow
$buildOutput = dotnet build --verbosity quiet 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "? Build successful!" -ForegroundColor Green
} else {
    Write-Host "? Build failed!" -ForegroundColor Red
    Write-Host $buildOutput
    Pop-Location
    exit 1
}

Pop-Location
Write-Host ""

# Step 4: Start MCPBridge
Write-Host "Step 4: Starting MCPBridge..." -ForegroundColor Yellow
$mcpBridgePath = Join-Path (Get-Location).Path "MCPBridge"
Start-Job -Name "MCPBridge" -ScriptBlock {
    param($path)
    Set-Location $path
    dotnet run
} -ArgumentList $mcpBridgePath

Start-Sleep -Seconds 3

Write-Host "? MCPBridge started" -ForegroundColor Green
Write-Host ""

# Step 5: Wait for service to be ready
Write-Host "Step 5: Waiting for service to be ready..." -ForegroundColor Yellow
$maxAttempts = 10
$attempt = 0
$ready = $false

while ($attempt -lt $maxAttempts -and -not $ready) {
    $attempt++
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5555/health" -UseBasicParsing -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            $ready = $true
            Write-Host "? MCPBridge is ready!" -ForegroundColor Green
        }
    } catch {
        Write-Host "  Attempt $attempt/$maxAttempts - waiting..." -ForegroundColor Gray
        Start-Sleep -Seconds 2
    }
}

if (-not $ready) {
    Write-Host "? MCPBridge failed to start!" -ForegroundColor Red
    Write-Host "Check job output: Get-Job -Name MCPBridge | Receive-Job" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Step 6: Verify tools
Write-Host "Step 6: Verifying tools..." -ForegroundColor Yellow
try {
    $tools = (Invoke-WebRequest http://localhost:5555/tools -UseBasicParsing).Content | ConvertFrom-Json
    $toolCount = $tools.Count
    
    Write-Host "Total tools: $toolCount" -ForegroundColor Cyan
    
    if ($toolCount -ge 105) {
        Write-Host "? All tools loaded!" -ForegroundColor Green
    } else {
        Write-Host "? Expected 105 tools, found $toolCount" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "Tools by category:" -ForegroundColor White
    $tools | Group-Object category | Sort-Object Count -Descending | Format-Table Count, Name -AutoSize
    
} catch {
    Write-Host "? Failed to get tools" -ForegroundColor Red
}

Write-Host ""
Write-Host "???????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "  MCPBridge Status" -ForegroundColor Cyan
Write-Host "???????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host ""
Write-Host "MCPBridge URL: http://localhost:5555" -ForegroundColor White
Write-Host "Tool Count: $toolCount" -ForegroundColor White
Write-Host "Job Status: Running" -ForegroundColor Green
Write-Host ""
Write-Host "Commands:" -ForegroundColor Yellow
Write-Host "  View logs: Get-Job -Name MCPBridge | Receive-Job" -ForegroundColor Gray
Write-Host "  Stop: Get-Job -Name MCPBridge | Stop-Job" -ForegroundColor Gray
Write-Host ""
Write-Host "Ready to test! ??" -ForegroundColor Green
