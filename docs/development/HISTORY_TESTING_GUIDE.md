# History Loading - Testing Guide

## Quick Start Test

### 1. Basic Responsiveness Test (5 minutes)
```
1. Run the app
2. Navigate to History tab
3. Observe:
   ? Panel appears immediately (within 100ms)
   ? Toolbar is visible and clickable
   ? "Loading history..." message appears briefly
   ? Items start appearing in list
   ? "loading thumbnails..." appears briefly in toolbar
   ? Thumbnails appear progressively
   ? No freezing at any point
```

### 2. Large History Test (10 minutes)
**Prerequisites**: History with 100+ items
```
1. Navigate to History tab with large history
2. Time to first display: <100ms (measured from click to panel visible)
3. Time to items: <2s (measured from click to first items visible)
4. Can scroll immediately while thumbnails load in background
5. Verify no UI lag while scrolling
```

### 3. Interaction During Load (5 minutes)
```
1. Navigate to History
2. While thumbnails loading (watch for "loading thumbnails..." text):
   ? Can scroll list
   ? Can toggle between grid/list view
   ? Can click items
   ? Can right-click for context menu
   ? All interactions are responsive
```

---

## Detailed Test Scenarios

### Scenario 1: Initial Load
**Expected**: Items load quickly, thumbnails load in background

```
Time  Event
????????????????????????????
0ms   User clicks History tab
10ms  Panel renders empty
50ms  Toolbar visible and enabled
100ms LoadHistoryAsync begins (background)
1000ms Items appear in UI
1100ms LoadThumbnailsInBackgroundAsync begins
1200ms Toolbar shows "loading thumbnails..."
2000ms First thumbnails appear
10000ms All thumbnails fully loaded
10100ms "loading thumbnails..." disappears
```

**Verification**:
- [ ] Panel visible within 100ms
- [ ] Items visible within 2s
- [ ] No visual artifacts or blank spaces
- [ ] Toolbar buttons responsive before items load
- [ ] Loading indicators accurate

---

### Scenario 2: Rapid Tab Switching
**Expected**: Previous loads cancelled, new load starts fresh

```
1. Click History tab
2. Immediately click another tab (e.g., Settings)
3. Before thumbnails finish loading
4. Click History again
```

**Expected Behavior**:
- [ ] Previous thumbnail load cancelled
- [ ] New history load starts
- [ ] No "ghost" thumbnails from previous load
- [ ] Clean restart of loading process

**Verification in Code**:
```
Debug output should show:
"Thumbnail loading was cancelled"  ? Previous load
"History loaded: 150 items"        ? New load starts
```

---

### Scenario 3: Refresh During Load
**Expected**: Refresh cancels ongoing loads and restarts

```
1. Click History tab (loads)
2. While thumbnails loading: click Refresh button
3. Observe fresh load
```

**Expected Behavior**:
- [ ] Ongoing thumbnail load cancelled immediately
- [ ] Fresh history load starts
- [ ] No duplicate items in list
- [ ] Toolbar "loading thumbnails..." updates

**Debug Output**:
```
Thumbnail loading was cancelled
History loaded: 150 items
Thumbnails pre-loaded: 150 images
```

---

### Scenario 4: Scrolling Performance
**Expected**: Smooth scrolling even while thumbnails load

```
1. Open History with 100+ items
2. While "loading thumbnails..." is visible
3. Scroll up/down rapidly multiple times
```

**Expected Behavior**:
- [ ] No stuttering or frame drops
- [ ] Smooth scrolling animation
- [ ] Items render as they come into view
- [ ] Responsive to input

**Performance Metrics**:
- Frame rate should stay 60 FPS
- Input latency < 100ms

---

### Scenario 5: View Toggle
**Expected**: View switch works while loading

```
1. Open History
2. While loading history items: click toggle button
3. Switch between Grid and List view
```

**Expected Behavior**:
- [ ] View switches immediately
- [ ] Items visible in new view
- [ ] Loading continues in background
- [ ] No lag in animation

---

### Scenario 6: File System Changes
**Expected**: Missing files handled gracefully

```
1. Open History with 100+ items
2. Delete some image files from disk
3. Click Refresh
```

**Expected Behavior**:
- [ ] History items still display
- [ ] Missing thumbnails skip gracefully
- [ ] No error messages
- [ ] No crash

**Debug Output**:
```
History loaded: 150 items  ? Items still there
Thumbnails pre-loaded: 145 images  ? Some skipped
```

---

### Scenario 7: Very Large History
**Prerequisites**: 500+ history items

```
1. Open History with very large dataset
2. Measure startup performance
3. Monitor memory usage
```

**Expected Behavior**:
- [ ] Panel still appears within 100ms
- [ ] Items list renders within 2s
- [ ] Thumbnails load progressively without crashing
- [ ] Memory usage stays reasonable (<500MB)

**Performance Targets**:
- Initial load: <200ms
- Items display: <3s
- Thumbnail load: <30s
- Memory: <500MB total

---

## Memory & CPU Monitoring

### During Thumbnail Loading

**CPU Usage**:
```
Baseline:     ~5%
During load:  ~25-40%  ? Batch of 5 items
Then pause:   ~5%      ? 50ms sleep
Repeat
```

? **Good**: Spikes to 40% then drops (batch + pause pattern)  
? **Bad**: Constant 80%+ (indicates no pausing)

**Memory Usage**:
```
Before History: ~200MB
After Load:     ~250-300MB  ? Thumbnails cached
Should NOT:     >500MB      ? Memory leak indicator
```

? **Good**: Stable within 50-100MB of baseline  
? **Bad**: Growing continuously (leak detected)

### How to Monitor

#### Windows
```powershell
# Open Task Manager (Ctrl+Shift+Esc)
# Columns to watch:
# - CPU %
# - Memory (Private Working Set)
# - Memory (Committed)
```

#### Linux
```bash
# Watch memory in real-time
watch -n 1 'ps aux | grep XerahS'

# Or use htop for interactive view
htop -p $(pgrep XerahS)
```

#### macOS
```bash
# Use Activity Monitor
open -a "Activity Monitor"

# Or command line
sample XerahS 5 | head -50
```

---

## Debug Output Verification

### Expected Log Sequence

```
[HistoryViewModel] History file path: C:\...\history.db
[HistoryViewModel] History.xml location: ... (exists=True)
[HistoryManagerSQLite] (DB operations in background)
History loaded: 150 items
Thumbnails pre-loaded: 150 images
```

### Warning Signs

? **No "History loaded" message**
- Database query timed out
- File corrupted
- Permission issue

? **"Thumbnails pre-loaded: 0 images"**
- No valid images in history
- File paths invalid
- All files missing

? **Multiple "Thumbnail loading was cancelled"**
- User repeatedly switching tabs too fast
- Normal in some scenarios, but check task load

---

## Stress Tests

### Test 1: Repeated Loading (10 iterations)
```csharp
for (int i = 0; i < 10; i++)
{
    // Click refresh button
    // Wait for load to complete
    // Verify memory stable
}
```

**Verify**: Memory returns to baseline after each load

### Test 2: Concurrent Operations
```
While history loading:
  - Scroll history
  - Right-click items
  - Copy file paths
  - Delete items
```

**Verify**: No deadlocks or crashes

### Test 3: Extreme Scale (1000+ items)
**How to generate test data**:
```csharp
// In HistoryViewModel constructor:
for (int i = 0; i < 1000; i++)
{
    var item = new HistoryItem { /* ... */ };
    _historyManager.AppendHistoryItem(item);
}
```

**Verify**:
- [ ] Still responsive
- [ ] No timeout
- [ ] Memory < 1GB

---

## Regression Tests

### Before Each Release

```
Test                                Status    Notes
??????????????????????????????????????????????????????
1. History displays immediately       [ ]
2. Items load progressively           [ ]
3. Thumbnails load in background      [ ]
4. View toggle works during load      [ ]
5. Refresh cancels ongoing load       [ ]
6. Rapid tab switching safe           [ ]
7. Scrolling smooth while loading     [ ]
8. Missing files handled              [ ]
9. Memory stable (no leaks)           [ ]
10. CPU not saturated                 [ ]
```

---

## Issue Reporting Template

If you find a problem:

```
Title: [History Loading] Brief description

Reproduction:
1. Step 1
2. Step 2
3. Step 3

Expected:
What should happen

Actual:
What actually happens

Environment:
- OS: Windows 10 / 11 / Linux / macOS
- History Items: 50 / 100 / 500
- Image Count: X
- Available RAM: GB
- SSD/HDD: 

Logs:
[Paste relevant debug output]

Attachments:
- Screenshot if visual issue
- Performance graph if slowness issue
```

---

## Performance Benchmarks

### Target Performance

| Metric | Target | Current | Status |
|--------|--------|---------|--------|
| Time to Panel Display | <100ms | ? | ?? |
| Time to Items | <2s | ? | ?? |
| Time to Thumbnails | <15s | ? | ?? |
| Scroll FPS | 60 FPS | ? | ?? |
| Memory Peak | <500MB | ? | ?? |
| CPU Peak | <50% | ? | ?? |

### How to Measure

#### Time to Panel Display
```
1. Open Timeline or Performance Monitor
2. Start capture
3. Click History tab
4. Record time until panel renders
5. Stop capture
6. Compare to <100ms target
```

#### Time to Items
```
1. Start stopwatch
2. Click History tab
3. Stop when first item appears
4. Target: <2s
```

#### Scrolling Performance
```
1. Open History with items and thumbnails loaded
2. Use Developer Tools frame rate meter
3. Scroll rapidly
4. Target: 60 FPS maintained
```

---

## Checklist for Release

Before shipping:

- [ ] All test scenarios pass
- [ ] No regression from previous version
- [ ] Memory leaks verified absent
- [ ] CPU usage reasonable
- [ ] Error messages helpful
- [ ] Debug logging appropriate
- [ ] Cancel token properly implemented
- [ ] UI thread safety verified
- [ ] Performance acceptable on slow hardware
- [ ] Documentation updated

---
