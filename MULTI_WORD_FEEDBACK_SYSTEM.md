# 🎨 Multi-Word Wordle Feedback System

## Overview
Implemented a sophisticated multi-word validation system for the WordScroll game that provides real-time feedback when players scroll columns to form words in the center row.

## 🎯 Color System

### ✅ Green (Correct)
- Letter is in the dominant target word and in the correct position
- Example: 'C' at position 0 when forming "CATCH"

### 🟡 Yellow (Present)
- Letter is in the dominant target word but in the wrong position
- Example: 'T' from "CATCH" appearing at position 1 instead of position 2

### 🟣 Purple (Interference)
- Letter belongs to a different target word and interrupts the dominant word formation
- Example: 'S' from "STARS" appearing while trying to form "CATCH"

### ⚪ Gray (Absent)
- Letter is not found in any target word

## 🧠 Dominant Word Selection Logic

### Priority System:
1. **First Unique Letter Rule**: The word containing the first unique letter (not shared with other target words) becomes dominant
2. **Disambiguation**: If multiple words share the same letter, the next distinguishing letter determines dominance
3. **Random Selection**: When letters are equally ambiguous, random selection between tied candidates
4. **Match Score Fallback**: Uses weighted scoring (correct positions × 2 + wrong positions × 1) when no unique letters exist

### Example Scenarios:
- `"CATCH"` vs `"PREPS"` vs `"STARS"`
- If center row shows `"C____"` → CATCH becomes dominant (C is unique to CATCH)
- If center row shows `"CA___"` → CATCH stays dominant (A confirms the choice)
- If center row shows `"CASTR"` → CATCH dominant, but S,T,R show as purple (interference from STARS)

## 🔄 Trigger Conditions

### When Feedback Applies:
- **Only when scrolling stops** (via `SnapRowToGrid` and `SnapColumnToGrid`)
- **Wordle mode only** (requires `LevelData.IsWordleStyle = true`)
- **Multiple target words** (single words use traditional Wordle feedback)

### Real-time vs Static:
- Feedback is applied when user finishes scrolling a column/row
- No continuous feedback during drag operations
- Immediate visual response when scroll snaps to grid position

## 🛠️ Implementation Details

### Core Methods:
- `CheckCenterRowWordleFeedback()` - Main entry point, called from snap methods
- `ApplyMultiWordFeedback()` - Orchestrates the multi-word analysis
- `AnalyzeWordMatch()` - Calculates match scores for each target word
- `DetermineDominantWord()` - Applies dominance selection logic
- `ApplyDominantWordFeedback()` - Applies final color feedback with interference detection

### Data Structures:
```csharp
public class WordMatchAnalysis
{
    public string targetWord;
    public int correctPositions;      // Green matches
    public int wrongPositions;       // Yellow matches
    public int totalMatches;         // Total letter matches
    public float matchScore;         // Weighted score
    public bool hasUniqueLetters;    // Contains letters unique to this word
    public int firstUniquePosition; // Position of first unique letter
}
```

## 🧪 Testing Features

### Built-in Test Methods:
1. `TestCenterRowWordleFeedback()` - Tests with CATCH, PREPS, STARS
2. `TestMultiWordScenarios()` - Comprehensive scenario testing (accessible via Inspector context menu)

### Test Scenarios:
- Perfect matches: "CATCH", "STARS"
- Mixed words: "CATPR", "PRCAT", "STARP"
- Interference detection: "CSTAR"

### Manual Testing:
Right-click on WordGridManager in Inspector → "Test Multi-Word Scenarios"

## 🎮 Integration Points

### Automatic Triggers:
- Called from `SnapRowToGrid()` when row scrolling stops
- Called from `SnapColumnToGrid()` when column scrolling stops
- Integrated with existing scroll/snap mechanics

### Level Data Requirements:
- `LevelData.IsWordleStyle = true`
- `LevelData.TargetWords` array with multiple words
- Compatible with existing single-word Wordle levels

## 🚀 Usage Examples

### Setting Up Multi-Word Level:
```csharp
LevelData level = new LevelData();
level.IsWordleStyle = true;
level.TargetWords = new string[] { "CATCH", "PREPS", "STARS" };
level.GridSize = 5; // Match word length
```

### Expected Behavior:
1. Player scrolls columns to align letters in center row
2. When scrolling stops, system analyzes current center row
3. Determines dominant word based on unique letter positions
4. Applies color feedback: Green/Yellow for dominant word, Purple for interference, Gray for invalid
5. Visual feedback guides player toward completing target words

## 🔍 Debug Output

### Console Logging:
- Detailed analysis of each target word match
- Dominant word selection reasoning
- Per-letter feedback decisions
- Interference detection explanations

### Log Example:
```
🎯 ANALYSIS: 'CATCH' - Correct:2, Wrong:1, Score:5.00, UniqueAt:0
🎯 ANALYSIS: 'PREPS' - Correct:0, Wrong:2, Score:2.00, UniqueAt:1
🎯 DOMINANT: Single word with unique letters: 'CATCH' at position 0
🎯 INTERFERENCE: Letter 'S' at position 4 belongs to 'STARS' (not dominant 'CATCH')
```

This system provides engaging, educational feedback that guides players toward discovering multiple target words while clearly indicating when they're mixing letters from different words.
