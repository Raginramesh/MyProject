# Dual-Mode UI System Implementation

## Overview
This document describes the implementation of the dual-mode UI system that displays different interfaces based on the game mode:
- **Scrabble Style**: Shows score-based UI with points and round scores
- **Wordle Style**: Shows target word progress (count only) and letter feedback

**Design Decision**: Target words are kept hidden from players to maintain puzzle integrity and challenge. Only the progress count "X/Y Words Found" is displayed.

## Design Rationale

### Target Word Secrecy
Target words are intentionally hidden from players in Wordle-style levels to:
- **Maintain Challenge**: Players must discover words through exploration rather than having a checklist
- **Preserve Puzzle Integrity**: Showing target words would eliminate the discovery aspect
- **Encourage Experimentation**: Players try different words and learn from letter feedback
- **Create Authentic Wordle Experience**: Mirrors the original Wordle game where the target word is unknown

### UI Information Display
- **Progress Counter**: Shows "2/5 Words Found" to indicate advancement without spoiling the puzzle
- **Letter Feedback**: Provides hints through color coding (Green/Yellow/Gray) for each attempted word
- **Target Word Celebration**: When a target word is found, it's briefly displayed as positive reinforcement
- **No Word Lists**: Avoids showing target words in any form (checklist, hints, etc.)

## Core Components

### 1. GameManager Updates
**File**: `GameManager.cs`

**New UI Fields**:
```csharp
[Header("Dual-Mode UI")]
[SerializeField] private GameObject scrabbleUIGroup; // Contains score-related UI elements
[SerializeField] private GameObject wordleUIGroup; // Contains target word progress UI elements
[SerializeField] private TextMeshProUGUI targetWordProgressText; // Shows "X/Y words found"
[SerializeField] private TargetWordFeedbackUI targetWordFeedbackUI; // For Wordle-style feedback
```

**Key Methods**:
- `UpdateScoreUI()` - Enhanced to route to appropriate UI mode
- `UpdateScrabbleUI()` - Handles score-based display (existing logic)
- `UpdateWordleUI()` - Handles target word progress display (count only)
- `InitializeDualModeUI()` - Sets up UI based on level type

### 2. TargetWordFeedbackUI
**File**: `TargetWordFeedbackUI.cs`

**Purpose**: Provides visual feedback for Wordle-style gameplay including:
- Target word discovery animations
- Letter-by-letter feedback (Green/Yellow/Gray)
- Progress updates
- Celebratory animations

**Key Methods**:
- `ShowTargetWordFound(string word)` - Celebrates target word discovery
- `ShowLetterFeedback(string word, LetterFeedback[] feedbacks)` - Shows letter-by-letter feedback
- `ShowProgressUpdate(int foundCount, int totalCount)` - Quick progress notification

### 3. LetterFeedbackDisplay
**File**: `LetterFeedbackDisplay.cs`

**Purpose**: Individual letter feedback component for prefab system
- Displays a single letter with color-coded background
- Green = Correct position
- Yellow = Present in word but wrong position  
- Gray = Not present in any target word

### 4. DualModeUIManager (Optional)
**File**: `DualModeUIManager.cs`

**Purpose**: Centralized UI coordination (alternative to GameManager integration)
- Manages UI group toggling
- Coordinates between different UI systems
- Provides clean separation of concerns

## UI Hierarchy Setup

### Scrabble Mode UI Group
```
ScrabbleUIGroup (GameObject)
├── ScoreText (TextMeshProUGUI) - Total score display
├── RoundScoreText (TextMeshProUGUI) - Current round score
└── ScoreBackground (Image) - Optional background
```

### Wordle Mode UI Group
```
WordleUIGroup (GameObject)
├── ProgressText (TextMeshProUGUI) - "X/Y Words Found" (target words hidden)
└── TargetWordFeedbackUI (Custom Component)
    ├── FeedbackPanel (GameObject)
    │   ├── TargetWordText (TextMeshProUGUI)
    │   ├── FeedbackMessageText (TextMeshProUGUI)
    │   └── LetterFeedbackContainer (Transform)
    └── LetterFeedbackPrefab (Prefab Reference)
```

## Integration Flow

### Level Start
1. `GameManager.StartGame()` calls `InitializeDualModeUI()`
2. Check if current level is Wordle or Scrabble style
3. Toggle appropriate UI groups (scrabbleUIGroup vs wordleUIGroup)
4. Initialize UI with starting values

### Word Processing (Scrabble Mode)
1. Words found → Calculate scores
2. `UpdateScrabbleUI()` → Update score displays
3. Traditional scoring animations continue

### Word Processing (Wordle Mode)
1. Words found → Check for target words and generate letter feedback
2. `ProcessWordleStyleLogic()` → Show letter feedback for non-target words
3. For target words: `TargetWordFeedbackUI.ShowTargetWordFound()`
4. `UpdateWordleUI()` → Update progress counter (no target word list shown)
5. Check for level completion (all target words found)

## Visual Feedback System

### Target Word Discovery
- **Animation**: Scale-up celebration effect
- **Sound**: Success audio clip
- **Display**: "TARGET WORD FOUND!" message
- **Duration**: 2 seconds display time

### Letter Feedback
- **Colors**: 
  - Green: Correct letter in correct position
  - Yellow: Correct letter in wrong position
  - Gray: Letter not in any target word
- **Animation**: Letters appear sequentially with bounce effect
- **Sound**: Letter reveal sound for each letter
- **Timing**: 0.1s delay between letters

### Progress Updates
- **Display**: "X/Y Words Found" counter (target words kept secret for puzzle integrity)
- **Updates**: Real-time as target words are discovered

## Configuration

### Inspector Setup
1. **GameManager**:
   - Assign `scrabbleUIGroup` and `wordleUIGroup` GameObjects
   - Set `targetWordProgressText` and `targetWordListText` references
   - Assign `targetWordFeedbackUI` component reference

2. **TargetWordFeedbackUI**:
   - Configure feedback panel and text components
   - Set up letter feedback container and prefab
   - Adjust animation timing and colors
   - Assign audio clips for feedback sounds

3. **LetterFeedbackPrefab**:
   - Create prefab with Image (background) and TextMeshProUGUI (letter)
   - Attach `LetterFeedbackDisplay` component
   - Configure default colors

### Level Data
- Ensure `LevelData` has correct `gameMode` setting:
  - `LevelGameMode.ScrabbleStyle` for score-based levels
  - `LevelGameMode.WordleStyle` for target word levels
- Set `targetWords` array for Wordle-style levels

## Testing

### Scrabble Mode Testing
1. Create level with `gameMode = ScrabbleStyle`
2. Verify only `scrabbleUIGroup` is visible
3. Find words and confirm score updates
4. Check traditional scoring animations work

### Wordle Mode Testing  
1. Create level with `gameMode = WordleStyle` and target words
2. Verify only `wordleUIGroup` is visible
3. Find non-target words → Should show letter feedback
4. Find target words → Should show celebration + update progress
5. Find all target words → Should complete level

### UI Switching Testing
1. Play Scrabble level → Switch to Wordle level
2. Verify UI groups toggle correctly
3. Confirm no UI overlap or persistence issues

## Performance Considerations

- **Letter Feedback**: Pooling system could be added for frequent feedback
- **Animation Queuing**: Multiple target words found simultaneously are queued
- **UI Updates**: Only update when values change to avoid unnecessary redraws
- **Memory**: LetterFeedback prefabs are destroyed after display to prevent accumulation

## Extension Points

### Future Enhancements
1. **Animation System**: Add Tetris-style letter drop animations
2. **Sound Design**: Enhanced audio feedback for different achievement types
3. **Visual Effects**: Particle systems for target word discovery
4. **Accessibility**: Screen reader support and colorblind-friendly options
5. **Themes**: Different visual themes for different level types

### Custom Feedback Types
- Extend `LetterFeedback` enum for new feedback types
- Add custom colors and animations in `TargetWordFeedbackUI`
- Create specialized feedback for different word types

This dual-mode UI system provides a foundation for rich, context-aware user interfaces that adapt seamlessly to different gameplay styles while maintaining consistent performance and user experience.
