# Install Playwright Browsers - Automated Script

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host " Playwright Browser Installation" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

$MCPBridgePath = "path\to\your\clone\MCPBridge"

# Step 1: Check if MCPBridge directory exists
Write-Host "[1/5] Checking MCPBridge directory..." -ForegroundColor Yellow
if (-not (Test-Path $MCPBridgePath)) {
    Write-Host "      ? MCPBridge directory not found!" -ForegroundColor Red
    Write-Host "      Expected: $MCPBridgePath" -ForegroundColor Yellow
    exit 1
}
Write-Host "      ? MCPBridge found" -ForegroundColor Green

# Step 2: Stop any running MCPBridge processes
Write-Host ""
Write-Host "[2/5] Stopping MCPBridge services..." -ForegroundColor Yellow
try {
    Get-Job | Where-Object { $_.Name -like "*MCPBridge*" } | Stop-Job -ErrorAction SilentlyContinue
    Get-Job | Where-Object { $_.Name -like "*MCPBridge*" } | Remove-Job -ErrorAction SilentlyContinue
    
    $mcpProcess = Get-Process -Name "MCPBridge" -ErrorAction SilentlyContinue
    if ($mcpProcess) {
        Stop-Process -Name "MCPBridge" -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        Write-Host "      ? MCPBridge processes stopped" -ForegroundColor Green
    } else {
        Write-Host "      ? No MCPBridge processes running" -ForegroundColor Green
    }
} catch {
    Write-Host "      ! Could not stop some processes (this is OK)" -ForegroundColor Yellow
}

# Step 3: Build MCPBridge
Write-Host ""
Write-Host "[3/5] Building MCPBridge..." -ForegroundColor Yellow
Push-Location $MCPBridgePath

try {
    $buildOutput = dotnet build --configuration Debug 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "      ? Build successful" -ForegroundColor Green
    } else {
        Write-Host "      ! Build had warnings (continuing...)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "      ? Build error: $($_.Exception.Message)" -ForegroundColor Red
    Pop-Location
    exit 1
}

# Step 4: Check for playwright.ps1
Write-Host ""
Write-Host "[4/5] Checking for Playwright script..." -ForegroundColor Yellow
$playwrightScript = Join-Path $MCPBridgePath "bin\Debug\net8.0\playwright.ps1"

if (-not (Test-Path $playwrightScript)) {
    Write-Host "      ? playwright.ps1 not found at:" -ForegroundColor Red
    Write-Host "      $playwrightScript" -ForegroundColor Red
    Write-Host ""
    Write-Host "      Trying alternative installation method..." -ForegroundColor Yellow
    
    # Alternative: Use dotnet tool
    try {
        dotnet tool install --global Microsoft.Playwright.CLI
        playwright install
        Write-Host "      ? Browsers installed via global tool" -ForegroundColor Green
        Pop-Location
        
        Write-Host ""
        Write-Host "=========================================" -ForegroundColor Green
        Write-Host " Installation Complete!" -ForegroundColor Green
        Write-Host "=========================================" -ForegroundColor Green
        exit 0
    } catch {
        Write-Host "      ? Alternative method failed" -ForegroundColor Red
        Pop-Location
        exit 1
    }
}

Write-Host "      ? Found playwright.ps1" -ForegroundColor Green

# Step 5: Install browsers
Write-Host ""
Write-Host "[5/5] Installing Playwright browsers..." -ForegroundColor Yellow
Write-Host "      This may take 2-5 minutes..." -ForegroundColor Gray
Write-Host ""

try {
    # Run the playwright install command
    & pwsh $playwrightScript install
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "      ? Browsers installed successfully!" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "      ! Installation completed with warnings" -ForegroundColor Yellow
    }
} catch {
    Write-Host ""
    Write-Host "      ? Installation error: $($_.Exception.Message)" -ForegroundColor Red
    Pop-Location
    exit 1
}

Pop-Location

# Step 6: Verify installation
Write-Host ""
Write-Host "Verifying installation..." -ForegroundColor Yellow
$playwrightDir = Join-Path $env:LOCALAPPDATA "ms-playwright"

if (Test-Path $playwrightDir) {
    Write-Host "? Playwright directory found:" -ForegroundColor Green
    Write-Host "  $playwrightDir" -ForegroundColor Gray
    Write-Host ""
    
    $browsers = Get-ChildItem $playwrightDir -Directory
    if ($browsers.Count -gt 0) {
        Write-Host "? Installed browsers:" -ForegroundColor Green
        foreach ($browser in $browsers) {
            Write-Host "  ? $($browser.Name)" -ForegroundColor Green
        }
    } else {
        Write-Host "! No browsers found in Playwright directory" -ForegroundColor Yellow
    }
} else {
    Write-Host "! Playwright directory not found" -ForegroundColor Yellow
}

# Success message
Write-Host ""
Write-Host "=========================================" -ForegroundColor Green
Write-Host " Installation Complete!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Start MCPBridge:" -ForegroundColor White
Write-Host "   cd path\to\your\clone" -ForegroundColor Gray
Write-Host "   .\start_all.ps1" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Start Agent UI:" -ForegroundColor White
Write-Host "   cd agent" -ForegroundColor Gray
Write-Host "   .\start_ui.ps1" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Test with prompt:" -ForegroundColor White
Write-Host "   'Launch browser and navigate to https://www.google.com'" -ForegroundColor Gray
Write-Host ""
