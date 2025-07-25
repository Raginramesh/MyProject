# Unity Setup Guide for Dual-Mode UI System

## Overview
This guide covers the Unity Editor setup and script configuration required to implement the dual-mode UI system that switches between Scrabble-style (score-based) and Wordle-style (target word discovery) gameplay.

## Unity Scene Setup

### 1. Canvas Hierarchy Structure

Create the main Canvas structure in your game scene:

```
Canvas (Main Game UI)
├── SafeArea (RectTransform for mobile safe areas)
│   ├── TopBar (Contains level info, timer, moves)
│   │   ├── LevelNameText (TextMeshProUGUI)
│   │   ├── TimerText (TextMeshProUGUI)
│   │   └── MovesText (TextMeshProUGUI)
│   │
│   ├── ScrabbleUIGroup (GameObject - Score-based UI)
│   │   ├── ScoreDisplay (Panel)
│   │   │   ├── ScoreText (TextMeshProUGUI) - "Score: 1,250"
│   │   │   ├── RoundScoreText (TextMeshProUGUI) - "+45"
│   │   │   └── ScoreBackground (Image)
│   │   └── ScoreAnimations (Optional animations)
│   │
│   ├── WordleUIGroup (GameObject - Target word discovery UI)
│   │   ├── ProgressDisplay (Panel)
│   │   │   ├── ProgressText (TextMeshProUGUI) - "2/5 Words Found"
│   │   │   └── ProgressBackground (Image)
│   │   └── TargetWordFeedbackUI (Custom Component)
│   │       ├── FeedbackPanel (Panel - Initially inactive)
│   │       │   ├── TargetWordText (TextMeshProUGUI)
│   │       │   ├── FeedbackMessageText (TextMeshProUGUI)
│   │       │   └── LetterFeedbackContainer (HorizontalLayoutGroup)
│   │       └── (LetterFeedbackPrefab will be instantiated here)
│   │
│   ├── GameGrid (WordGridManager)
│   └── BottomBar (Optional UI elements)
│
└── Overlays (CanvasGroup)
    ├── PausePanel
    ├── GameOverPanel
    └── LevelCompletePanel
```

### 2. Component Configuration

#### GameManager Setup
1. **Create or locate GameManager GameObject**
2. **Attach GameManager script**
3. **Configure Inspector fields**:

```csharp
[Header("UI References")]
scoreText = // Assign ScoreText from ScrabbleUIGroup
roundScoreText = // Assign RoundScoreText from ScrabbleUIGroup
timerText = // Assign TimerText from TopBar
movesText = // Assign MovesText from TopBar

[Header("Dual-Mode UI")]
scrabbleUIGroup = // Assign ScrabbleUIGroup GameObject
wordleUIGroup = // Assign WordleUIGroup GameObject  
targetWordProgressText = // Assign ProgressText from WordleUIGroup
targetWordFeedbackUI = // Assign TargetWordFeedbackUI component

[Header("Component References")]
wordGridManager = // Assign WordGridManager component
wordValidator = // Assign WordValidator component
// ... other existing components
```

#### TargetWordFeedbackUI Setup
1. **Create TargetWordFeedbackUI GameObject** (child of WordleUIGroup)
2. **Attach TargetWordFeedbackUI script**
3. **Configure Inspector fields**:

```csharp
[Header("Target Word Display")]
feedbackPanel = // Assign FeedbackPanel GameObject
panelCanvasGroup = // Assign CanvasGroup on FeedbackPanel
targetWordText = // Assign TargetWordText component
feedbackMessageText = // Assign FeedbackMessageText component

[Header("Letter Feedback Display")]
letterFeedbackContainer = // Assign LetterFeedbackContainer Transform
letterFeedbackPrefab = // Assign LetterFeedbackPrefab (created below)

[Header("Animation Settings")]
wordPopScale = 1.2f
wordPopDuration = 0.5f
letterAnimationDelay = 0.1f
feedbackDisplayDuration = 2.0f

[Header("Colors")]
correctLetterColor = Green
presentLetterColor = Yellow
incorrectLetterColor = Gray
targetWordFoundColor = Green

[Header("Audio")]
audioSource = // Assign AudioSource component
targetWordFoundSound = // Assign success audio clip
letterRevealSound = // Assign letter reveal audio clip
```

#### Letter Feedback Prefab Creation
1. **Create new GameObject** → "LetterFeedbackPrefab"
2. **Add components**:
   - `RectTransform` (UI element)
   - `Image` (background with color)
   - `LetterFeedbackDisplay` script
3. **Create child GameObject** → "LetterText"
   - Add `TextMeshProUGUI` component
   - Configure font, size, alignment
4. **Configure LetterFeedbackDisplay script**:
   - Assign `backgroundImage` (parent Image)
   - Assign `letterText` (child TextMeshProUGUI)
   - Set color values
5. **Save as Prefab** in Assets folder
6. **Delete from scene** (keep as prefab reference only)

### 3. Level Data Configuration

#### Creating Scrabble-Style Levels
1. **Right-click in Assets** → Create → Word Scroll → Level Data
2. **Configure Level Settings**:
```csharp
[Header("Level Identity")]
levelNumber = 1
levelName = "Scrabble Challenge 1"
levelDescription = "Reach 500 points within 15 moves"

[Header("Game Mode Configuration")]
gameMode = ScrabbleStyle
gridSize = 5

[Header("Scrabble Style Settings")]
targetScore = 500
maxMoves = 15
unlimitedMoves = false

[Header("Star Rating Thresholds"]
oneStarPercentage = 50f    // 250 points for 1 star
twoStarPercentage = 75f    // 375 points for 2 stars
threeStarPercentage = 100f // 500 points for 3 stars
```

#### Creating Wordle-Style Levels
1. **Right-click in Assets** → Create → Word Scroll → Level Data
2. **Configure Level Settings**:
```csharp
[Header("Level Identity")]
levelNumber = 2
levelName = "Word Discovery 1"
levelDescription = "Find all 3 target words within 12 moves"

[Header("Game Mode Configuration")]
gameMode = WordleStyle
gridSize = 5

[Header("Wordle Style Settings")]
targetWords = ["HOUSE", "MOUSE", "PHONE"] // Add target words
winConditionType = MoveBased
maxMoves = 12
unlimitedMoves = false

[Header("Wordle Style Star Thresholds"]
threeStarEfficiencyPercentage = 25f  // Complete within 25% of moves (3 moves)
twoStarEfficiencyPercentage = 50f    // Complete within 50% of moves (6 moves)
oneStarEfficiencyPercentage = 90f    // Complete within 90% of moves (11 moves)
```

### 4. LevelManager Setup

1. **Create LevelManager GameObject** (DontDestroyOnLoad)
2. **Attach LevelManager script**
3. **Configure Inspector**:
```csharp
[Header("Level Configuration")]
allLevels = // Drag all LevelData assets here
currentLevelIndex = 0
debugMode = false // Enable to unlock all levels for testing
```

## Script Integration Setup

### 1. Execution Order
Set script execution order in **Project Settings → Script Execution Order**:
```
-100: LevelManager
0: GameManager (default)
100: WordGridManager
200: WordValidator
300: UI Components (TargetWordFeedbackUI, etc.)
```

### 2. Required Dependencies

Ensure these packages are installed via **Window → Package Manager**:
- **TextMeshPro** (for all text components)
- **DOTween** (for animations) - Optional but recommended
- **Unity UI** (built-in)

### 3. Layer Setup (Optional)
Create layers for better organization:
- **UI** (layer 5) - Already exists
- **GameGrid** (layer 8) - For grid elements
- **Feedback** (layer 9) - For floating feedback UI

## Testing Setup

### 1. Scene Testing
1. **Create test scene** with Canvas setup
2. **Add GameManager, LevelManager, WordGridManager**
3. **Create both Scrabble and Wordle style LevelData assets**
4. **Test UI switching** between game modes

### 2. Prefab Testing
1. **Test LetterFeedbackPrefab** instantiation
2. **Verify color assignments** for different feedback types
3. **Check animation timing** and audio playback

### 3. Level Progression Testing
1. **Start with Scrabble level** → Complete → Switch to Wordle level
2. **Verify UI groups toggle** correctly
3. **Test target word discovery** and progress updates

## Common Setup Issues & Solutions

### Issue 1: UI Elements Not Appearing
**Solution**: Check Canvas scaling settings:
- Canvas Scaler → UI Scale Mode: "Scale With Screen Size"
- Reference Resolution: 1920x1080
- Screen Match Mode: "Match Width Or Height" (0.5)

### Issue 2: TargetWordFeedbackUI Not Working
**Solution**: Verify component references:
- All TextMeshProUGUI components assigned
- CanvasGroup component on FeedbackPanel
- LetterFeedbackPrefab has LetterFeedbackDisplay script

### Issue 3: Level Data Not Loading
**Solution**: Check LevelManager setup:
- LevelData assets in allLevels array
- LevelManager set to DontDestroyOnLoad
- Current level index within bounds

### Issue 4: Compilation Errors
**Solution**: Script dependencies:
- Ensure all scripts are in correct folders
- Check for missing using statements
- Verify enum definitions (LevelGameMode, LetterFeedback)

## Build Settings

### 1. Scenes in Build
Add scenes in **File → Build Settings**:
1. MainMenu (index 0)
2. GameScene (index 1)
3. Any additional scenes

### 2. Player Settings
Configure **Edit → Project Settings → Player**:
- Company Name
- Product Name
- Bundle Identifier (mobile)
- Supported orientations
- Target platforms

### 3. Quality Settings
Optimize for target platforms:
- Texture Quality
- Anti-aliasing
- VSync settings
- Shadow settings

This setup provides a complete foundation for the dual-mode UI system, ensuring proper integration between Unity Editor configuration and script functionality.
