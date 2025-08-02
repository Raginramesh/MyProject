# Letter Discovery Position Fix

## Bug Description
**Issue**: When a letter was discovered in the correct position, ALL instances of that letter in the dominant word were revealed, not just the letter in the correct position.

**Example Problem**:
- Dominant word: "HELLO"
- Player discovers 'L' in position 2 (correct)
- **Wrong behavior**: Both L's revealed → "H E L L _"
- **Correct behavior**: Only position 2 revealed → "H E L _ _"

## Fix Implementation

### Key Changes

#### 1. Position-Based Tracking
- **Before**: Tracked discovered letters globally (`HashSet<char> discoveredLetters`)
- **After**: Tracks discovered positions (`HashSet<int> discoveredPositions`)

#### 2. Updated Method Signatures
```csharp
// Old method
OnLetterDiscovered(char letter)

// New method  
OnLetterDiscovered(char letter, int position)
```

#### 3. Position-Specific Display Logic
```csharp
// Now checks specific positions instead of letters
if (discoveredPositions.Contains(i))
{
    displayWord.Append(letter);  // Show discovered letter
}
else
{
    displayWord.Append('_');     // Show dash
}
```

#### 4. Word Change Handling
- When dominant word changes, discovered positions are reset
- This ensures positions are relative to the current word
- Prevents position conflicts between different words

### Example Behavior

#### Scenario: Dominant word "HELLO"
1. **Initial state**: `"_ _ _ _ _"`
2. **'H' discovered at position 0**: `"H _ _ _ _"`
3. **'L' discovered at position 2**: `"H _ L _ _"` ✅ (Only position 2 L revealed)
4. **'L' discovered at position 3**: `"H _ L L _"` ✅ (Now both L positions discovered separately)

#### Scenario: Word changes from "HELLO" to "LEVEL"
1. **Previous discoveries**: Positions 0, 2 discovered in "HELLO"
2. **Word changes**: Positions reset automatically
3. **New word display**: `"_ _ _ _ _"` ✅ (Fresh start for "LEVEL")

### Technical Details

#### Position Tracking
- Uses `HashSet<int>` for O(1) position lookups
- Positions are 0-indexed (0, 1, 2, 3, 4 for 5-letter word)
- Automatically resets when dominant word changes

#### Backward Compatibility
- Maintains `discoveredLetters` HashSet for compatibility
- Debug logs include both letter and position information
- Fallback systems continue to work

#### Grid Integration
- WordGridManager passes both letter and position to GameManager
- Works with both single-word and multi-word feedback systems
- Maintains connection between grid colors and word display

### Debug Information

#### Console Logs
- `🎯 LETTER DISCOVERED: 'L' at position 2 - Total positions discovered: 3`
- `🎯 POSITION RESET: Cleared discovered positions for new dominant word 'LEVEL'`
- `🎯 DOMINANT DISPLAY: Updated 'HELLO' → 'H _ L _ _' (Discovery Mode)`

### Testing Instructions

#### Test Position-Specific Discovery
1. Start level with word containing duplicate letters (e.g., "HELLO", "LEVEL", "ASSET")
2. Scroll to make first instance of duplicate letter turn green
3. **Verify**: Only that position is revealed in dominant word display
4. Continue to make second instance turn green
5. **Verify**: Now both positions are revealed separately

#### Test Word Changes
1. Get some positions discovered in current word
2. Scroll to change dominant word to different word with same letters
3. **Verify**: New word starts with all dashes (positions reset)
4. **Verify**: New discoveries work correctly for new word

## Result
✅ **Fixed**: Letter discovery now works correctly with position-specific reveals  
✅ **Fixed**: Duplicate letters are revealed only when their specific positions are discovered  
✅ **Fixed**: Word changes properly reset position tracking  
✅ **Maintained**: All existing functionality and performance

This fix provides the precise, position-based letter discovery behavior that makes the Wordle-style gameplay intuitive and correct.
