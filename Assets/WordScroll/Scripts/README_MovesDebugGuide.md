# Debugging Moves Display Issue

## The Problem
Moves are not showing up in the move text UI.

## Debug Steps Added

### 1. **Enhanced Debug Logging**
Added comprehensive debug logging to:
- `GameManager.Start()` - Shows level system status
- `GameManager.StartGame()` - Shows initialization state  
- `GameManager.UpdateMovesUI()` - Shows what values are being set
- `OnLevelSystemMovesChanged()` - Shows when moves change events fire

### 2. **Manual UI Refresh Methods**
Added to GameManager:
- `RefreshMovesUI()` - Force update moves display
- `RefreshGameUI()` - Force update both score and moves

### 3. **Auto-Level Initialization**
GameManager now automatically starts level 0 if no current level is found when using level system.

### 4. **Debug Helper Component**
Created `MovesDisplayDebugger.cs` with:
- Press **M** key to debug current moves state
- Press **R** key to force UI refresh  
- Context menu options for manual testing

## Unity Setup Checklist

### 1. **Check GameManager Settings**
- Set `Use Level System = true`
- Set `Current Display Mode = Moves` 
- Assign `Moves Text` field to your UI text component
- Ensure `Status Display Group` is assigned and contains the moves text

### 2. **Check LevelManager Setup**
- LevelManager is in the scene
- `All Levels` list has at least one LevelData asset
- LevelData has `Max Moves` set to desired value (e.g., 10)

### 3. **Check UI Hierarchy**
- Moves Text GameObject is active
- Status Display Group is active
- Text component has a font assigned

### 4. **Testing in Play Mode**
1. Enter play mode
2. Check Console for debug logs:
   - "GameManager Start: useLevelSystem=true" 
   - "Level System Moves: LevelManager.Instance=Found"
   - "Moves Text Updated: \"Moves: 10\""

3. If logs show issues, use `MovesDisplayDebugger`:
   - Add component to any GameObject
   - Press **M** to see current state
   - Press **R** to force UI refresh
   - Use context menu "Start Level 0" if needed

## Expected Debug Output (Working System)
```
🎮 GameManager Start: useLevelSystem=true, LevelManager.Instance=Found
🎮 IsUsingLevelSystem=true
🎮 Level System: Starting Level 1
🎮 Status Display Group: Active=true, CurrentDisplayMode=Moves
📱 Moves Text GameObject: Active=true, DisplayMode=Moves
🎯 Level System Moves: LevelManager.Instance=Found, CurrentLevel=Level 1
🎯 Level Data: MaxMoves=10, CurrentMoves=0
🎯 UpdateMovesUI (Level System): CurrentMoves=0, MovesRemaining=10, MaxMoves=10
📱 Moves Text Updated: "Moves: 10"
```

## Common Issues & Solutions

### Issue: "LevelManager.Instance=NULL"
**Solution:** LevelManager not in scene or not properly initialized
- Add LevelManager prefab to scene
- Ensure it's active at start

### Issue: "CurrentLevel=NULL" 
**Solution:** No level started or no levels configured
- Check LevelManager has levels in `All Levels` list
- Use debugger "Start Level 0" button

### Issue: "Moves Text Updated" but nothing visible
**Solution:** UI setup issue
- Check Moves Text GameObject is active
- Check Status Display Group is active  
- Check Current Display Mode = Moves
- Check Text component has font

### Issue: Shows "Moves: 50" instead of "Moves: 10"
**Solution:** Still using traditional values
- Check `Use Level System = true` in GameManager
- Check LevelManager.Instance is not null
- Check CurrentLevel is properly set
