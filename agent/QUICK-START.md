# ?? Quick Command Reference - Agent UI

## ? All Issues FIXED!

### Issue 1: Curly Braces in F-String ? FIXED
**Line:** 1426-1534  
**Change:** `{` ? `{{` and `}` ? `}}`

### Issue 2: Lowercase Boolean ? FIXED  
**Line:** 868  
**Change:** `false` ? `False`

### Issue 3: Background Process ? FIXED
**File:** `start_ui.ps1`  
**Change:** Run Python in foreground

---

## ?? START AGENT UI NOW

### Two-Window Setup:

#### Window 1: MCPBridge
```powershell
cd path\to\your\clone
.\start_all.ps1
```

**Wait for:**
```
? MCPBridge API is running on http://localhost:5555
```

**KEEP WINDOW OPEN!**

---

#### Window 2: Agent UI
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

======== Running on http://0.0.0.0:8080 ========
```

**Browser opens automatically to:** `http://localhost:8080`

**KEEP WINDOW OPEN!**

---

## ? Verification

### Check MCPBridge (Window 1):
```
? MCPBridge API is running on http://localhost:5555
? MCP Server started
? Agent Runner started
```

### Check UI Server (Window 2):
```
?? TalosAI QA Agent UI Server
?? http://localhost:8080
======== Running on http://0.0.0.0:8080 ========
```

### Check Browser:
- Opens to `http://localhost:8080`
- Shows text input box
- Shows "Run Agent" button
- **NO** "can't reach this page" error

---

## ?? Test Prompts

### Test 1: Basic
```
What tools do you have?
```

### Test 2: Browser
```
Launch Chrome browser
```

### Test 3: Navigation
```
Navigate to https://www.google.com
```

### Test 4: Complete Test
```
Open browser, go to google.com, search for test automation, and take a screenshot
```

---

## ?? Quick Fixes

### Port 8080 in Use:
```powershell
$proc = Get-NetTCPConnection -LocalPort 8080 | Select -ExpandProperty OwningProcess
Stop-Process -Id $proc -Force
```

### MCPBridge Not Running:
```powershell
cd path\to\your\clone
.\start_all.ps1
```

### Check Syntax:
```powershell
cd agent
python -m py_compile agent_runner.py
```
Should show NO errors

---

## ?? Files Changed

| File | Issue | Status |
|------|-------|--------|
| `agent_runner.py` line 1426-1534 | Curly braces | ? Fixed |
| `agent_runner.py` line 868 | `false` ? `False` | ? Fixed |
| `start_ui.ps1` | Background process | ? Fixed |

---

## ?? Status

**Build:** ? Successful  
**Syntax:** ? No Errors  
**MCPBridge:** ? Integrated  
**Playwright:** ? Implemented  
**Agent UI:** ? Ready

---

## ?? READY TO USE!

Run the commands above and start testing! ??
