# Strategic Word Placement Game Integration Plan

## Overview
This document outlines the integration of a new strategic word-placement game prototype into the existing Unity word game project. The new game will leverage the existing modular architecture while introducing dynamic grid scaling, drag-and-drop word tiles, and Scrabble-style scoring.

## Core Architecture Integration

### 1. Scene Structure
- **New Scene**: `WordPlacementGame.unity`
- **Scene Flow**: Home → Game Selection → WordPlacementGame
- **Reuse**: Keep existing home screen, modifier, upgrade, and gift systems

### 2. Game Manager Architecture
```csharp
// New: WordPlacementGameManager.cs
public class WordPlacementGameManager : MonoBehaviour
{
    // Core game systems
    private DynamicGridManager gridManager;
    private WordTileManager tileManager;
    private PlacementValidator validator;
    private ScrabbleScorer scorer;
    
    // Inherited systems (reuse existing)
    private ModifierManager modifierManager;
    private LevelManager levelManager;
    private AnimatedScoringSystem animatedScoring;
    
    // New UI components
    private WordPlacementUI gameUI;
    private WordListPanel wordListPanel;
}
```

## New System Components

### 1. Dynamic Grid Manager
```csharp
// New: DynamicGridManager.cs
public class DynamicGridManager : MonoBehaviour
{
    [Header("Grid Configuration")]
    public int gridSize = 15;
    public float baseCellSize = 50f;
    public float gridPadding = 20f;
    
    [Header("Scaling")]
    public bool autoScale = true;
    public float maxGridScreenPercentage = 0.7f;
    
    // Dynamic scaling based on screen resolution
    private void CalculateOptimalCellSize()
    {
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;
        float availableSpace = Mathf.Min(screenWidth, screenHeight) * maxGridScreenPercentage;
        
        float calculatedCellSize = (availableSpace - gridPadding * 2) / gridSize;
        cellSize = Mathf.Max(calculatedCellSize, 30f); // Minimum cell size
    }
}
```

### 2. Word Tile System
```csharp
// New: WordTile.cs
public class WordTile : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Tile Data")]
    public string word;
    public int[] letterScores;
    public int totalScore;
    
    [Header("Visual")]
    public List<LetterBlock> letterBlocks;
    public CanvasGroup canvasGroup;
    
    // Drag and drop functionality
    public void OnBeginDrag(PointerEventData eventData) { }
    public void OnDrag(PointerEventData eventData) { }
    public void OnEndDrag(PointerEventData eventData) { }
}
```

### 3. Word List Panel
```csharp
// New: WordListPanel.cs
public class WordListPanel : MonoBehaviour
{
    [Header("UI References")]
    public ScrollRect scrollRect;
    public Transform contentParent;
    public GameObject wordTilePrefab;
    
    [Header("Word Data")]
    public WordListScriptableObject currentWordList;
    
    public void PopulateWordList(WordListScriptableObject wordList)
    {
        // Clear existing tiles
        // Instantiate new tiles from word list
        // Apply difficulty-based scoring
    }
}
```

### 4. Placement Validation
```csharp
// New: PlacementValidator.cs
public class PlacementValidator : MonoBehaviour
{
    public bool ValidatePlacement(WordTile tile, Vector2Int startPosition, PlacementOrientation orientation)
    {
        // Check center cell requirement (first word)
        // Check overlap with existing words
        // Check bounds
        // Check collision with other tiles
        return isValid;
    }
}
```

## UI/UX Integration

### 1. Screen Layout (Mobile & Desktop)
```
┌─────────────────────────────────────┐
│ Score: 1250    Timer: 02:30    ⚙️   │
├─────────────────────────────────────┤
│                                     │
│           DYNAMIC GRID              │
│        (15x15, auto-scaled)         │
│                                     │
├─────────────────────────────────────┤
│      WORD LIST PANEL (Scrollable)   │
│ [WORD1] [WORD2] [WORD3] [WORD4]     │
│ [WORD5] [WORD6] [WORD7] [WORD8]     │
└─────────────────────────────────────┘
```

### 2. Responsive Design
- **Mobile Portrait**: Stack vertically (Grid top, word list bottom)
- **Mobile Landscape**: Grid left, word list right
- **Desktop**: Grid center, word list sidebar
- **Tablet**: Hybrid layout with larger touch targets

### 3. Canvas Scaling Strategy
```csharp
// Resolution-independent scaling
Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
CanvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
CanvasScaler.referenceResolution = new Vector2(1920, 1080);
CanvasScaler.matchWidthOrHeight = 0.5f; // Balance between width/height
```

## Existing System Reuse

### 1. Modifier System Integration
```csharp
// In WordPlacementGameManager.cs
private void ApplyModifierEffects()
{
    var activeModifiers = ModifierManager.Instance.GetAllActiveModifiers();
    
    foreach (var modifier in activeModifiers)
    {
        switch (modifier.modifierType)
        {
            case ModifierType.ScoreMultiplier:
                scorer.SetScoreMultiplier(modifier.value);
                break;
            case ModifierType.ExtraTime:
                gameTimer.AddTime(modifier.value);
                break;
            case ModifierType.BonusWords:
                wordListPanel.AddBonusWords(modifier.value);
                break;
        }
    }
}
```

### 2. Animated Scoring Reuse
```csharp
// Extend existing AnimatedScoringSystem
public class WordPlacementScoringSystem : AnimatedScoringSystem
{
    public void AnimateWordPlacement(WordTile tile, Vector2Int position, int score)
    {
        // Animate tile placement
        // Show score calculation
        // Apply modifier effects
        // Update total score
    }
}
```

### 3. Level Progression
```csharp
// Extend existing LevelManager
public class WordPlacementLevelManager : LevelManager
{
    [Header("Word Placement Specific")]
    public WordListScriptableObject[] levelWordLists;
    public float[] levelTimeRequirements;
    
    protected override void ConfigureLevel(int levelIndex)
    {
        base.ConfigureLevel(levelIndex);
        
        // Set word list for this level
        var wordList = levelWordLists[levelIndex % levelWordLists.Length];
        wordListPanel.PopulateWordList(wordList);
    }
}
```

## Data Structure Design

### 1. Word List ScriptableObject
```csharp
[CreateAssetMenu(fileName = "WordList", menuName = "Word Game/Word List")]
public class WordListScriptableObject : ScriptableObject
{
    [System.Serializable]
    public class WordData
    {
        public string word;
        public int difficulty; // 1-5
        public string[] hints;
        public int baseScore;
    }
    
    public WordData[] words;
    public int minWordsToWin = 5;
    public float timeLimit = 300f;
}
```

### 2. Grid State Management
```csharp
[System.Serializable]
public class GridState
{
    public char[,] letters;
    public int[,] scores;
    public bool[,] occupied;
    public Vector2Int centerPosition;
    
    public bool IsValidPlacement(string word, Vector2Int start, PlacementOrientation orientation)
    {
        // Validation logic
    }
}
```

## Implementation Timeline

### Phase 1: Core Grid System (Week 1)
- [ ] Create DynamicGridManager
- [ ] Implement resolution-independent scaling
- [ ] Basic cell instantiation and layout
- [ ] Grid centering and padding

### Phase 2: Word Tiles & UI (Week 2)
- [ ] WordTile prefab and component
- [ ] WordListPanel with scrolling
- [ ] Drag and drop mechanics
- [ ] Ghost placement feedback

### Phase 3: Game Logic (Week 3)
- [ ] Placement validation
- [ ] Scrabble-style scoring
- [ ] Word intersection logic
- [ ] Game state management

### Phase 4: Integration & Polish (Week 4)
- [ ] Modifier system integration
- [ ] Animated scoring integration
- [ ] Level progression
- [ ] Mobile/desktop UI optimization

## Technical Considerations

### 1. Performance Optimization
- **Object Pooling**: Reuse WordTile instances
- **Efficient Grid Updates**: Only refresh changed cells
- **Batch UI Updates**: Minimize Canvas rebuilds

### 2. Mobile Optimization
- **Touch Targets**: Minimum 44pt touch targets
- **Drag Sensitivity**: Adjust for finger vs mouse
- **Performance**: Target 60fps on mid-range devices

### 3. Accessibility
- **Text Scaling**: Support system text scaling
- **Color Blind**: High contrast mode
- **Screen Readers**: Proper UI labels

## Testing Strategy

### 1. Unit Tests
- Grid placement validation
- Score calculation
- Word intersection logic

### 2. Integration Tests
- Modifier effects
- Level progression
- Save/load functionality

### 3. Platform Testing
- iOS/Android performance
- Various screen resolutions
- Portrait/landscape modes

## Next Steps

1. **Architecture Review**: Confirm this integration approach
2. **Prototype Development**: Start with DynamicGridManager
3. **UI Design**: Create wireframes for different screen sizes
4. **Asset Creation**: Design word tile graphics and animations
5. **Testing Plan**: Define acceptance criteria for each phase

This integration plan maintains the existing game's modular architecture while introducing the new strategic word-placement mechanics in a scalable, maintainable way.
