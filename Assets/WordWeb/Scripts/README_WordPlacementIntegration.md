# Strategic Word Placement Game Integration Plan

## Overview
This document outlines the integration of a new strategic word-placement game prototype into the existing Unity word game project. The new game will leverage the existing modular architecture while introducing dynamic grid scaling, drag-and-drop word tiles, and Scrabble-style scoring.

**Location**: All new word-placement prototype scripts are located in `Assets/WordWeb/Scripts/`

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

## Project Structure

All new word-placement prototype scripts are now organized in the `Assets/WordWeb/Scripts/` folder:

```
Assets/WordWeb/Scripts/
├── DynamicGridManager.cs         # Dynamic grid system with auto-scaling
├── GridCell.cs                   # Individual grid cell component
├── WordTile.cs                   # Draggable word tile component
├── LetterBlock.cs                # Letter scoring and visual component
├── WordListPanel.cs              # Word list UI management
├── WordListScriptableObject.cs   # Word data asset definition
├── WordPlacementGameManager.cs   # Main game manager
├── WordPlacementUI.cs            # UI controller for the game
├── PlacementValidator.cs         # Word placement validation
├── WordPlacementScorer.cs        # Scrabble-style scoring system
└── README_WordPlacementIntegration.md  # This documentation
```

### Integration with Existing Systems
- **ModifierManager**: WordPlacementGameManager integrates with `WordScroll.Modifiers` namespace
- **AnimatedScoringSystem**: Can be extended for word placement animations
- **LevelManager**: Compatible with existing level progression system
- **UI Architecture**: Follows existing UI patterns and canvas scaling

This integration plan maintains the existing game's modular architecture while introducing the new strategic word-placement mechanics in a scalable, maintainable way.

## Unity Setup Instructions

### Phase 1: Scene Creation and Basic Setup

#### 1. Create New Scene
1. **File** → **New Scene** → **2D (URP)** or **2D** 
2. **Save As**: `WordPlacementGame.unity` in `Assets/Scenes/`
3. Delete default objects except **Main Camera** and **Directional Light**

#### 2. Canvas Setup
1. **Right-click** in Hierarchy → **UI** → **Canvas**
2. Set **Canvas** component:
   - **Render Mode**: Screen Space - Overlay
   - **UI Scale Mode**: Scale With Screen Size
   - **Reference Resolution**: 1920 x 1080
   - **Match**: 0.5 (balance width/height)
3. Add **Canvas Scaler** if not present
4. Add **GraphicRaycaster** if not present

#### 3. Create Core Game Objects
```
Hierarchy Structure:
├── Main Camera
├── Canvas
│   ├── GameUI (Empty GameObject)
│   │   ├── TopBar
│   │   │   ├── ScoreText (TextMeshPro)
│   │   │   ├── TimerText (TextMeshPro)
│   │   │   └── SettingsButton (Button)
│   │   ├── GridContainer (Empty GameObject)
│   │   └── WordListContainer (Empty GameObject)
│   └── Overlays (Empty GameObject)
│       ├── GameOverPanel
│       └── PausePanel
├── GameManager (Empty GameObject)
├── GridManager (Empty GameObject)
├── WordListPanel (Empty GameObject)
└── AudioManager (Empty GameObject)
```

### Phase 2: Grid System Setup

#### 1. Dynamic Grid Manager Setup
1. **Create Empty GameObject**: `GridManager`
2. **Add Component**: `DynamicGridManager.cs`
3. **Configure Inspector**:
   ```
   Grid Configuration:
   - Grid Size: 15
   - Base Cell Size: 50
   - Grid Padding: 20
   
   Scaling:
   - Auto Scale: ✓
   - Max Grid Screen Percentage: 0.7
   
   Prefabs:
   - Grid Cell Prefab: [Create prefab - see below]
   ```

#### 2. Create Grid Cell Prefab
1. **Right-click** in Project → **Create** → **UI** → **Button**
2. **Rename**: `GridCellPrefab`
3. **Configure RectTransform**:
   - **Width**: 50, **Height**: 50
   - **Anchors**: Middle Center
4. **Configure Button**:
   - **Interactable**: ✓
   - **Transition**: Color Tint
5. **Add Component**: `GridCell.cs`
6. **Configure GridCell** in Inspector:
   ```
   Visual References:
   - Background Image: [Button's Image component]
   - Canvas Group: [Add CanvasGroup component]
   
   Colors:
   - Empty Color: Light Gray (200, 200, 200, 255)
   - Occupied Color: Dark Gray (100, 100, 100, 255)
   - Center Color: Gold (255, 215, 0, 255)
   - Hover Color: Light Blue (173, 216, 230, 255)
   - Invalid Color: Red (255, 100, 100, 255)
   ```
7. **Drag to Project** to create prefab
8. **Delete** from scene

### Phase 3: Word Tile System Setup

#### 1. Create Letter Block Prefab
1. **Right-click** Hierarchy → **UI** → **Panel**
2. **Rename**: `LetterBlockPrefab`
3. **Configure RectTransform**: 40x40 size
4. **Add Children**:
   - **LetterText** (TextMeshPro): Center, font size 18
   - **ScoreText** (TextMeshPro): Bottom-right corner, font size 10
5. **Add Component**: `LetterBlock.cs`
6. **Configure LetterBlock**:
   ```
   Visual References:
   - Background Image: [Panel Image]
   - Letter Text: [LetterText TMP]
   - Score Text: [ScoreText TMP]
   
   Colors:
   - Default Color: White
   - Highlight Color: Yellow
   
   Font Settings:
   - Letter Font: [TMPro font asset]
   - Letter Font Size: 18
   - Score Font Size: 10
   ```
7. **Create Prefab**

#### 2. Create Word Tile Prefab
1. **Right-click** Hierarchy → **UI** → **Panel**
2. **Rename**: `WordTilePrefab`
3. **Configure**:
   - **Add CanvasGroup** component
   - **Add LayoutGroup** (Horizontal)
   - **Content Size Fitter**: Horizontal Fit
4. **Add Component**: `WordTile.cs`
5. **Configure WordTile**:
   ```
   Prefabs:
   - Letter Block Prefab: [LetterBlockPrefab]
   
   Visual:
   - Canvas Group: [CanvasGroup component]
   - Background Image: [Panel Image]
   
   Drag Settings:
   - Drag Threshold: 5
   - Return Duration: 0.3
   ```
6. **Create Prefab**

### Phase 4: UI Setup

#### 1. Word List Panel Setup
1. **Create Empty GameObject**: `WordListPanel` (child of Canvas)
2. **Add RectTransform**: Anchor to bottom, height ~150-200
3. **Add Component**: `WordListPanel.cs`
4. **Create Children**:
   ```
   WordListPanel
   ├── ScrollView (ScrollRect)
   │   ├── Viewport
   │   │   └── Content (with ContentSizeFitter)
   │   └── Scrollbar Horizontal
   └── Header (TextMeshPro): "Available Words"
   ```
5. **Configure ScrollRect**:
   - **Content**: Content GameObject
   - **Horizontal**: ✓, **Vertical**: ✗
   - **Movement Type**: Elastic
   - **Scrollbar**: Horizontal Scrollbar
6. **Configure WordListPanel**:
   ```
   UI References:
   - Scroll Rect: [ScrollView ScrollRect]
   - Content Parent: [Content GameObject]
   - Word Tile Prefab: [WordTilePrefab]
   
   Settings:
   - Max Visible Words: 8
   - Shuffle Words: ✓
   - Auto Scroll: ✓
   ```

#### 2. Game UI Setup
1. **Select GameUI** GameObject
2. **Add Component**: `WordPlacementUI.cs`
3. **Configure TopBar**:
   ```
   TopBar/ScoreText (TextMeshPro):
   - Text: "Score: 0"
   - Font Size: 24
   - Alignment: Center Left
   
   TopBar/TimerText (TextMeshPro):
   - Text: "05:00"
   - Font Size: 24
   - Alignment: Center Right
   ```
4. **Configure WordPlacementUI**:
   ```
   UI References:
   - Score Text: [TopBar/ScoreText]
   - Timer Text: [TopBar/TimerText]
   - Game Over Panel: [Create game over panel]
   - Pause Panel: [Create pause panel]
   ```

### Phase 5: Game Manager Setup

#### 1. Main Game Manager
1. **Select GameManager** GameObject
2. **Add Component**: `WordPlacementGameManager.cs`
3. **Configure References**:
   ```
   Game Systems:
   - Grid Manager: [GridManager GameObject]
   - Word List Panel: [WordListPanel GameObject]
   - Game UI: [GameUI GameObject]
   - Placement Validator: [Create empty GameObject with PlacementValidator.cs]
   - Scorer: [Create empty GameObject with WordPlacementScorer.cs]
   
   Game Settings:
   - Game Timer: 300 (5 minutes)
   - Target Score: 1000
   - Words To Win: 5
   
   Audio Settings:
   - Audio Source: [Add AudioSource component]
   ```

#### 2. Create Word List Asset
1. **Right-click** Project → **Create** → **Word Game** → **Word List**
2. **Rename**: `DefaultWordList`
3. **Configure**:
   ```
   Word List Info:
   - List Name: "Beginner Words"
   - Description: "Easy words for testing"
   - Min Words To Win: 3
   - Time Limit: 300
   - Target Score: 500
   
   Words: (Add sample words)
   - Word: "CAT", Difficulty: 1, Base Score: 15
   - Word: "DOG", Difficulty: 1, Base Score: 15
   - Word: "FISH", Difficulty: 2, Base Score: 25
   - Word: "BIRD", Difficulty: 2, Base Score: 25
   - Word: "HOUSE", Difficulty: 3, Base Score: 35
   ```
4. **Assign** to WordListPanel in Inspector

### Phase 6: Input System Setup

#### 1. Input Actions (if using New Input System)
1. **Right-click** Project → **Create** → **Input Actions**
2. **Rename**: `WordGameInputActions`
3. **Add Action Map**: "WordPlacement"
4. **Add Actions**:
   ```
   - Touch: <Touchscreen>/primaryTouch/press
   - Position: <Touchscreen>/primaryTouch/position
   - MouseClick: <Mouse>/leftButton
   - MousePosition: <Mouse>/position
   - Escape: <Keyboard>/escape
   ```
5. **Generate C# Class**
6. **Add PlayerInput** component to GameManager

### Phase 7: Scene References and Final Setup

#### 1. Connect All References
1. **GameManager references**:
   - Drag all component GameObjects to respective fields
   - Assign prefabs and ScriptableObjects
2. **DynamicGridManager**:
   - Assign GridCellPrefab
   - Set parent transform (GridContainer)
3. **WordListPanel**:
   - Assign WordTilePrefab
   - Assign DefaultWordList

#### 2. Layer Setup (Optional)
1. **Create Layers** in Tags & Layers:
   - UI Layer (built-in)
   - Grid Layer
   - WordTile Layer
2. **Assign layers** to appropriate GameObjects

#### 3. Camera Setup
1. **Main Camera**:
   - **Clear Flags**: Solid Color
   - **Background**: Dark blue or black
   - **Culling Mask**: Everything (or exclude unnecessary layers)

### Phase 8: Testing and Validation

#### 1. Basic Functionality Test
1. **Play Scene**
2. **Verify**:
   - Grid generates correctly
   - Word tiles appear in panel
   - Drag and drop works
   - Score updates
   - Timer counts down

#### 2. Debug Tools
1. **Add Debug UI** (optional):
   ```csharp
   [Header("Debug")]
   public bool showDebugInfo = true;
   public KeyCode debugKey = KeyCode.F1;
   ```
2. **Enable Gizmos** in Scene view for grid visualization

### Phase 9: Build Settings

#### 1. Add Scene to Build
1. **File** → **Build Settings**
2. **Add Open Scenes**: WordPlacementGame scene
3. **Set Platform**: iOS/Android for mobile

#### 2. Player Settings (Mobile)
1. **Orientation**: Portrait or Auto-rotate
2. **Screen Resolution**: Adaptive
3. **Safe Area Handling**: Enabled

### Troubleshooting Common Issues

#### Grid Not Appearing
- Check GridCellPrefab is assigned
- Verify Canvas scaling settings
- Check grid container anchoring

#### Word Tiles Not Draggable
- Ensure CanvasGroup is attached
- Check GraphicRaycaster on Canvas
- Verify Input System setup

#### Performance Issues
- Enable object pooling
- Reduce grid size for testing
- Check for excessive UI rebuilds

#### References Not Found
- Use FindFirstObjectByType for missing references
- Check GameObject names match script expectations
- Verify components are added to correct objects

### Next Steps After Setup
1. **Create additional word lists** for different difficulty levels
2. **Add particle effects** for word placement feedback
3. **Implement save/load** functionality
4. **Add sound effects** and background music
5. **Create tutorial system** for new players
6. **Implement modifier cards** integration
7. **Add level progression** system
8. **Test on target devices** for performance optimization

This setup provides a complete, functional word-placement game prototype that integrates with your existing game architecture.
