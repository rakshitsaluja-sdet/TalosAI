# ????????????????????????????????????????????????????????????????
# analyse-and-generate.ps1
# PURPOSE: Reads your entire TalosAI solution and builds a context
#          file that GitHub Copilot can read to understand your
#          full framework before generating anything.
# HOW TO RUN: Right-click ? Run with PowerShell
#             OR in PowerShell terminal: .\analyse-and-generate.ps1
# ????????????????????????????????????????????????????????????????
 
# ?? Path Configuration ????????????????????????????????????????????????
# Resolve the repo root relative to this script's own location so it works
# on any checkout, regardless of where it's cloned.
$repoRoot      = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$githubFolder  = "$repoRoot\.github"
$talosaiProject = "$repoRoot\TalosAI"
$mcpBridge     = "$repoRoot\MCPBridge"
$agentsFolder  = "$githubFolder\agents"
$outputFolder  = "$agentsFolder\output"
 
# ?? Create output folder if not exists ???????????????????????????????
New-Item -ItemType Directory -Force -Path $outputFolder | Out-Null
 
Write-Host ""
Write-Host "????????????????????????????????????????" -ForegroundColor Cyan
Write-Host " TalosAI Solution Analyser" -ForegroundColor Cyan
Write-Host " Building Copilot Context File" -ForegroundColor Cyan
Write-Host "????????????????????????????????????????" -ForegroundColor Cyan
Write-Host ""
 
# ?? Helper function to read files ????????????????????????????????????
function Read-FilesOfType {
    param(
        [string]$folder,
        [string]$filter,
        [string]$label,
        [int]$maxFiles = 999
    )
 
    if (-not (Test-Path $folder)) {
        return "  [Folder not found: $folder]`n"
    }
 
    $files = Get-ChildItem -Path $folder -Filter $filter -Recurse `
             -ErrorAction SilentlyContinue |
             Where-Object { 
                 $_.FullName -notlike "*\bin\*" -and 
                 $_.FullName -notlike "*\obj\*" 
             } |
             Select-Object -First $maxFiles
 
    if ($files.Count -eq 0) {
        return "  [No $label files found in $folder]`n"
    }
 
    $content = "??? $label ($($files.Count) files) ???`n`n"
    
    foreach ($file in $files) {
        $relativePath = $file.FullName.Replace($talosaiProject, "")
        $content += "?? FILE: $relativePath`n"
        $content += "?`n"
        try {
            $fileContent = Get-Content $file.FullName -Raw -ErrorAction Stop
            foreach ($line in ($fileContent -split "`n")) {
                $content += "?  $line`n"
            }
        } catch {
            $content += "?  [Could not read file: $_]`n"
        }
        $content += "??????????????????????????????`n`n"
    }
 
    return $content
}
 
# ?? Helper to count items ?????????????????????????????????????????????
function Count-Occurrences {
    param([string]$text, [string]$pattern)
    $matches = [regex]::Matches($text, $pattern)
    return $matches.Count
}
 
# ?? STEP 1: Read solution structure ???????????????????????????????????
Write-Host "Step 1/8 - Reading solution structure..." -ForegroundColor Yellow
 
$allProjectFiles = Get-ChildItem -Path $repoRoot -Recurse `
    -ErrorAction SilentlyContinue |
    Where-Object { 
        $_.FullName -notlike "*\bin\*" -and 
        $_.FullName -notlike "*\obj\*" -and
        $_.FullName -notlike "*\.git\*" -and
        $_.FullName -notlike "*\allure*" -and
        $_.FullName -notlike "*\reports\*"
    }
 
$structureText  = "SOLUTION STRUCTURE`n"
$structureText += "Solution root: $repoRoot`n`n"
 
$grouped = $allProjectFiles | Group-Object { 
    Split-Path $_.FullName -Parent 
}
 
foreach ($group in $grouped | Select-Object -First 40) {
    $relFolder = $group.Name.Replace($repoRoot, "")
    $structureText += "  $relFolder\`n"
    foreach ($f in $group.Group | Select-Object -First 10) {
        $structureText += "    $($f.Name)`n"
    }
}
 
# ?? STEP 2: Read Feature Files ????????????????????????????????????????
Write-Host "Step 2/8 - Reading feature files..." -ForegroundColor Yellow
$featureContent = Read-FilesOfType `
    -folder "$talosaiProject\automation\features" `
    -filter "*.feature" `
    -label "FEATURE FILES"
 
$featureCount = (Get-ChildItem "$talosaiProject\automation\features" `
    -Filter "*.feature" -Recurse -ErrorAction SilentlyContinue).Count
 
# ?? STEP 3: Read Step Definitions ?????????????????????????????????????
Write-Host "Step 3/8 - Reading step definitions..." -ForegroundColor Yellow
$stepsContent = Read-FilesOfType `
    -folder "$talosaiProject\automation\steps" `
    -filter "*.cs" `
    -label "STEP DEFINITIONS"
 
# ?? STEP 4: Read Page Objects ?????????????????????????????????????????
Write-Host "Step 4/8 - Reading page objects..." -ForegroundColor Yellow
$pagesContent = Read-FilesOfType `
    -folder "$talosaiProject\automation\pages" `
    -filter "*.cs" `
    -label "PAGE OBJECTS"
 
# ?? STEP 5: Read Hooks ????????????????????????????????????????????????
Write-Host "Step 5/8 - Reading hooks..." -ForegroundColor Yellow
$hooksContent = Read-FilesOfType `
    -folder "$talosaiProject\automation\hooks" `
    -filter "*.cs" `
    -label "HOOKS"
 
# ?? STEP 6: Read Models / POCO ????????????????????????????????????????
Write-Host "Step 6/8 - Reading models..." -ForegroundColor Yellow
$modelsContent = Read-FilesOfType `
    -folder "$talosaiProject\automation\models" `
    -filter "*.cs" `
    -label "AUTOMATION MODELS"
 
$coreModels = Read-FilesOfType `
    -folder "$talosaiProject\core\models" `
    -filter "*.cs" `
    -label "CORE MODELS"
 
$utilsContent = Read-FilesOfType `
    -folder "$talosaiProject\core\utils" `
    -filter "*.cs" `
    -label "UTILITIES"
 
# ?? STEP 7: Read Config ???????????????????????????????????????????????
Write-Host "Step 7/8 - Reading config files..." -ForegroundColor Yellow
$configContent = ""
$configFiles = Get-ChildItem -Path $talosaiProject -Filter "*.json" -Recurse `
    -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notlike "*\bin\*" -and $_.FullName -notlike "*\obj\*" }
 
foreach ($cf in $configFiles) {
    $configContent += "?? CONFIG: $($cf.Name)`n"
    $configContent += (Get-Content $cf.FullName -Raw -ErrorAction SilentlyContinue)
    $configContent += "`n??????????????????????????????`n`n"
}
 
# ?? STEP 8: Build the context file ???????????????????????????????????
Write-Host "Step 8/8 - Building context file..." -ForegroundColor Yellow
 
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$scenarioCount = Count-Occurrences -text $featureContent -pattern "Scenario:"
$scenarioOutlineCount = Count-Occurrences -text $featureContent -pattern "Scenario Outline:"
$stepBindingCount = Count-Occurrences -text $stepsContent -pattern "\[Given\]|\[When\]|\[Then\]"
 
$contextFile = "$outputFolder\solution-context.md"
 
$context = @"
# TalosAI Test Framework � Full Solution Context
Generated: $timestamp
 
---
 
## INSTRUCTION FOR COPILOT / AI AGENT
You are the Senior QA Automation Architect for the TalosAI project.
The content below is the COMPLETE current state of the TalosAI test framework.
Use this as your authoritative knowledge base for this entire session.
 
Before generating ANYTHING you must:
1. Study the namespaces in step definition files � use the SAME namespace
2. Study the base classes � inherit from the SAME base classes
3. Study the using statements � reuse the SAME ones
4. Study the Gherkin style in feature files � match it EXACTLY
5. Study the page object pattern � replicate it EXACTLY
6. Study the WebDriver access pattern � use the SAME approach
7. Never create duplicate step bindings � check existing steps first
 
---
 
## SOLUTION SUMMARY
- Solution root:    $repoRoot
- TalosAI project:   $talosaiProject
- Feature files:    $featureCount files found
- Scenarios:        $scenarioCount scenarios + $scenarioOutlineCount outlines
- Step bindings:    $stepBindingCount bindings found
- Generated at:     $timestamp
 
---
 
## FOLDER STRUCTURE
$structureText
 
---
 
$featureContent
 
---
 
$stepsContent
 
---
 
$pagesContent
 
---
 
$hooksContent
 
---
 
$modelsContent
 
---
 
$coreModels
 
---
 
$utilsContent
 
---
 
## CONFIG FILES
$configContent
 
---
 
## TALOSAI CODING CONVENTIONS (derived from reading above files)
Study the files above and enforce these in every generated file:
- Namespace pattern:   (read from step definition files above)
- Base class:          (read from step definition files above)
- Driver access:       (read from hooks/steps above)
- Locator strategy:    (read from page objects above)
- Wait pattern:        (read from page objects above)
- Tag conventions:     (read from feature files above)
"@
 
# Write context file
$context | Out-File -FilePath $contextFile -Encoding UTF8 -Force
 
# ?? Output summary ????????????????????????????????????????????????????
$fileSizeKB = [Math]::Round((Get-Item $contextFile).Length / 1KB, 1)
 
Write-Host ""
Write-Host "????????????????????????????????????????" -ForegroundColor Green
Write-Host " Context file built successfully!" -ForegroundColor Green
Write-Host "????????????????????????????????????????" -ForegroundColor Green
Write-Host ""
Write-Host " File:       $contextFile" -ForegroundColor White
Write-Host " Size:       $fileSizeKB KB" -ForegroundColor White
Write-Host " Features:   $featureCount files" -ForegroundColor White
Write-Host " Scenarios:  $scenarioCount + $scenarioOutlineCount outlines" -ForegroundColor White
Write-Host " Steps:      $stepBindingCount bindings" -ForegroundColor White
Write-Host ""
Write-Host "????????????????????????????????????????" -ForegroundColor Cyan
Write-Host " NEXT STEPS:" -ForegroundColor Cyan
Write-Host "????????????????????????????????????????" -ForegroundColor Cyan
Write-Host ""
Write-Host " FOR GITHUB COPILOT:" -ForegroundColor Yellow
Write-Host " 1. Open VS Code or Visual Studio" -ForegroundColor White
Write-Host " 2. Open Copilot Chat" -ForegroundColor White
Write-Host " 3. Attach file: .github\agents\output\solution-context.md" -ForegroundColor White
Write-Host " 4. Attach agent: .github\agents\regression.agent.md" -ForegroundColor White
Write-Host " 5. Type your request" -ForegroundColor White
Write-Host ""
Write-Host " FOR MCP AGENT RUNNER:" -ForegroundColor Yellow
Write-Host " cd $repoRoot\agents" -ForegroundColor White
Write-Host ' python agent_runner.py "your goal here"' -ForegroundColor White
Write-Host ""
 
# Open output folder in Explorer
explorer $outputFolder
 