# Agent UI Startup Guide

## ? Fixed Issues

1. **Python Syntax Error** - Fixed unescaped curly braces in f-string templates
2. **Script Improvements** - Updated start_ui.ps1 to run server in foreground

---

## ?? How to Start Agent UI

### Step 1: Ensure MCPBridge is Running

**PowerShell Window 1:**
```powershell
cd path\to\your\clone
.\start_all.ps1
```

Wait for:
```
? MCPBridge API is running on http://localhost:5555
? MCP Server started  
? Agent Runner started
```

**DO NOT CLOSE THIS WINDOW!**

---

### Step 2: Start Agent UI

**PowerShell Window 2** (NEW window):
```powershell
cd path\to\your\clone\agent
.\start_ui.ps1
```

**Expected Output:**
```
=========================================
 TalosAI QA Agent UI
=========================================
?? Checking MCPBridge...
? MCPBridge already running
?? Starting Agent UI Server...
   URL: http://localhost:8080
   Press Ctrl+C to stop

?? TalosAI QA Agent UI Server
?? http://localhost:8080
?? Opening browser...

======== Running on http://0.0.0.0:8080 ========
(Press CTRL+C to quit)
```

**DO NOT CLOSE THIS WINDOW!**

---

### Step 3: Use Agent UI

- Edge browser should open automatically to `http://localhost:8080`
- If not, manually navigate to `http://localhost:8080`
- Type prompts in the text box
- Click "Run Agent" to execute

---

## ?? Troubleshooting

### Problem: Port 8080 already in use

```powershell
# Kill process on port 8080
$proc = Get-NetTCPConnection -LocalPort 8080 | Select-Object -ExpandProperty OwningProcess
Stop-Process -Id $proc -Force

# Then restart UI
.\start_ui.ps1
```

### Problem: MCPBridge not running

```powershell
# In another window
cd ..
.\start_all.ps1
```

### Problem: Syntax errors

Already fixed! But if you see them again:
```powershell
# Check syntax
python -m py_compile agent_runner.py

# Should show no errors
```

### Problem: aiohttp not installed

```powershell
pip install aiohttp
```

---

## ?? Testing Prompts

Once UI is running, try these prompts:

### Test 1: Basic Info
```
"What tools do you have available?"
```

### Test 2: Launch Browser
```
"Launch Chrome browser in non-headless mode"
```

### Test 3: Navigate
```
"Navigate to https://www.google.com"
```

### Test 4: Full Test
```
"Go to https://www.google.com, search for 'playwright testing', and take a screenshot"
```

---

## ? Stop Services

### Stop UI:
In PowerShell Window 2, press **Ctrl+C**

### Stop MCPBridge:
In PowerShell Window 1, press **Ctrl+C**

OR run:
```powershell
Get-Job | Stop-Job
Get-Job | Remove-Job
```

---

## ?? Quick Checklist

Before starting:
- [ ] GITHUB_TOKEN environment variable is set
- [ ] Python 3.x is installed
- [ ] aiohttp package is installed (`pip install aiohttp`)
- [ ] Ports 5555 and 8080 are free

When running:
- [ ] Window 1: MCPBridge running (don't close)
- [ ] Window 2: UI Server running (don't close)
- [ ] Edge browser showing UI at localhost:8080

**Ready to test!** ??
