using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

public class WordGridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int _fallbackGridSize = 7; // Used when no LevelData is available
    [SerializeField] private float _baseCellSize = 100f; // Base cell size for calculations
    [SerializeField] private float _spacing = 10f;
    public float spacing => _spacing;   
    [SerializeField] private GameObject letterCellPrefab;
    [SerializeField] private Transform gridParent;
    
    [Header("Dynamic Sizing")]
    [SerializeField] private bool enableAdaptiveSizing = true;
    [SerializeField] private RectTransform referencePanel; // Panel to use for size calculations
    [SerializeField] private float maxGridScreenPercentage = 1f; // Increased from 0.8f to 0.9f
    [SerializeField] private float minCellSize = 170f; // Reduced minimum from 60f
    [SerializeField] private float maxCellSize = 230f; // Increased maximum from 120f
    [SerializeField] private float gridPadding = 10f; // Reduced from 20f
    
    // Dynamic cell size property - calculated based on grid size and screen space
    private float _calculatedCellSize;
    public float cellSize => enableAdaptiveSizing ? _calculatedCellSize : _baseCellSize;
    
    // Helper properties to reduce redundant level manager access
    private LevelManager levelManager => LevelManager.Instance;
    private LevelData currentLevelData => levelManager?.CurrentLevel;
    
    // Dynamic grid size based on LevelData or fallback
    public int gridSize 
    { 
        get 
        {
            // Add additional safety check
            if (currentLevelData != null)
            {
                int levelGridSize = currentLevelData.GridSize;
                //Debug.Log($"📐 Using level grid size: {levelGridSize}");
                return levelGridSize;
            }
            
            Debug.Log($"📐 Using fallback grid size: {_fallbackGridSize}");
            return _fallbackGridSize;
        } 
    }

    [Header("Appearance")]
    [SerializeField] private float cellFadeInDuration = 0.1f;
    public float CellFadeInDuration => cellFadeInDuration;
    [SerializeField] private Color cellColorPrimary = Color.white;
    [SerializeField] private Color cellColorAlternate = Color.grey;

    [Header("Highlighting")]
    [Tooltip("Color for all valid highlighted words - creates a unified visual appearance")]
    [SerializeField] private Color validWordColor = Color.yellow;
    
    [Tooltip("Color for intersecting letters (letters shared between multiple words) - makes intersections clearly visible")]
    [SerializeField] private Color intersectionLetterColor = Color.magenta;
    
    [Header("Wordle Feedback Colors")]
    [Tooltip("Color for letters that are in the target word and in correct position (green)")]
    [SerializeField] private Color correctLetterColor = Color.green;
    
    [Tooltip("Color for letters that are in the target word but in wrong position (yellow)")]
    [SerializeField] private Color presentLetterColor = Color.yellow;
    
    [Tooltip("Color for letters that are not in the target word (gray)")]
    [SerializeField] private Color absentLetterColor = Color.gray;
    
    [Tooltip("Color for letters from different target words that interrupt the dominant word (purple)")]
    [SerializeField] private Color interferenceLetterColor = Color.magenta;

    [Header("References")]
    [SerializeField] private WordValidator wordValidator;
    [SerializeField] private GameManager gameManager;
    
    [Header("Cell Type System")]
    [SerializeField] private CellTypeManager cellTypeManager;

    private CellController[,] gridCells;
    public CellData[,] gridData { get; private set; } // Enhanced cell data
    private char[,] legacyGridData; // Compatibility layer for existing code

    public bool isAnimating { get; private set; } = false;

    // Note: WeightedLetters removed - now handled by CellTypeManager
    private Vector2 gridCenterOffset;

    // Define the number of extra cells for wrap-around effect on each side
    private const int WRAP_COUNT = 2; 
    
    // Arrays to hold the wraparound cells
    private CellController[,] _horizontalWrapCells; 
    private CellController[,] _verticalWrapCells;

    // Helper arrays for visual offsets of wraparound cells
    // For _horizontalWrapCells[row, i], _visualColOffsets[i] is its column relative to main grid start
    // For _verticalWrapCells[col, i], _visualRowOffsets[i] is its row relative to main grid start
    private static int[] _visualColOffsets;
    private static int[] _visualRowOffsets;
    
    // Track if cells are initialized for wraparound
    private bool wraparoundInitialized = false;

    // Unique ID counter for cell tracking
    private static int nextUniqueID = 1;

    void Awake()
    {
        if (letterCellPrefab == null) { Debug.LogError("WGM: Letter Cell Prefab not assigned!", this); enabled = false; return; }
        if (gridParent == null) { Debug.LogError("WGM: Grid Parent not assigned!", this); enabled = false; return; }
        if (wordValidator == null) wordValidator = FindFirstObjectByType<WordValidator>();
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (wordValidator == null) Debug.LogWarning("WGM: WordValidator not found!", this);
        if (gameManager == null) Debug.LogWarning("WGM: GameManager not found!", this);

        // Grid arrays will be initialized when level starts
        // Note: PopulateWeightedLettersList() removed - now handled by CellTypeManager
        CalculateGridCenterOffset();

        // Subscribe to level events for dynamic grid sizing
        if (LevelManager.Instance != null)
        {
            LevelManager.OnLevelStarted += OnLevelStarted;
        }
    }
    
    // Track screen size for adaptive resizing
    private Vector2 lastScreenSize;
    
    void Start()
    {
        lastScreenSize = new Vector2(Screen.width, Screen.height);
    }
    
    void Update()
    {
        // Check for screen size changes (orientation change, window resize, etc.)
        if (enableAdaptiveSizing)
        {
            Vector2 currentScreenSize = new Vector2(Screen.width, Screen.height);
            if (currentScreenSize != lastScreenSize)
            {
                lastScreenSize = currentScreenSize;
                // Delay the refresh to allow UI to settle
                StartCoroutine(DelayedRefreshCellSizes());
            }
        }
    }
    
    private IEnumerator DelayedRefreshCellSizes()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame(); // Extra frame wait to ensure UI is settled
        RefreshCellSizes();
    }
    
    void OnDestroy()
    {
        if (LevelManager.Instance != null)
        {
            LevelManager.OnLevelStarted -= OnLevelStarted;
        }
    }
    
    void OnLevelStarted(LevelData levelData)
    {
        Debug.Log($"📐 WordGridManager: Level started with grid size {levelData.GridSize}, Game Mode: {levelData.GameMode}");
        ReinitializeForLevel();
    }
    
    void InitializeGridArrays()
    {
        int size = gridSize;
        gridCells = new CellController[size, size];
        gridData = new CellData[size, size];
        Debug.Log($"📐 Grid arrays initialized for size {size}x{size}");
    }
    
    void InitializeVisualOffsets()
    {
        // Initialize visual offset maps based on WRAP_COUNT and gridSize
        _visualColOffsets = new int[WRAP_COUNT * 2];
        _visualRowOffsets = new int[WRAP_COUNT * 2];
        for (int i = 0; i < WRAP_COUNT; i++)
        {
            _visualColOffsets[i] = -WRAP_COUNT + i; // e.g., -2, -1 for WRAP_COUNT=2
            _visualColOffsets[i + WRAP_COUNT] = gridSize + i; // e.g., gridSize, gridSize+1 for WRAP_COUNT=2

            _visualRowOffsets[i] = -WRAP_COUNT + i;
            _visualRowOffsets[i + WRAP_COUNT] = gridSize + i;
        }
    }

    public void SetGameManager(GameManager manager)
    {
        this.gameManager = manager;
    }

    private void CalculateGridCenterOffset()
    {
        float totalGridWidth = gridSize * cellSize + (gridSize - 1) * spacing;
        gridCenterOffset = new Vector2(totalGridWidth / 2f - cellSize / 2f, totalGridWidth / 2f - cellSize / 2f);
    }

    public Vector2 GetBaseCellPosition(int r, int c)
    {
        float xPos = c * (cellSize + spacing) - gridCenterOffset.x;
        float yPos = -(r * (cellSize + spacing) - gridCenterOffset.y);
        return new Vector2(xPos, yPos);
    }

    /// <summary>
    /// Calculate optimal cell size based on available screen space and MAIN GRID SIZE ONLY
    /// This only considers the core grid (3x3 or 5x5) and ignores wraparound/scrolling cells
    /// </summary>
    private void CalculateAdaptiveCellSize()
    {
        if (!enableAdaptiveSizing)
        {
            _calculatedCellSize = _baseCellSize;
            return;
        }

        Vector2 availableSize;
        
        // Use reference panel if assigned, otherwise fall back to Canvas detection
        if (referencePanel != null)
        {
            availableSize = referencePanel.rect.size;
            Debug.Log($"📐 Using reference panel '{referencePanel.name}' for size calculation: {availableSize}");
        }
        else
        {
            // Fallback to Canvas detection
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                availableSize = canvasRect.rect.size;
                Debug.Log($"📐 Using Canvas '{canvas.name}' for size calculation: {availableSize}");
            }
            else
            {
                // Final fallback to screen size
                availableSize = new Vector2(Screen.width, Screen.height);
                Debug.LogWarning($"📐 No reference panel or Canvas found! Using Screen size: {availableSize}");
            }
        }
        
        // Safety check for valid size
        if (availableSize.x <= 0 || availableSize.y <= 0)
        {
            Debug.LogWarning($"📐 Invalid reference size {availableSize}! Using Screen size fallback.");
            availableSize = new Vector2(Screen.width, Screen.height);
        }

        // Calculate available space for grid
        float availableWidth = availableSize.x * maxGridScreenPercentage;
        float availableHeight = availableSize.y * maxGridScreenPercentage;
        float availableSpace = Mathf.Min(availableWidth, availableHeight);

        // IMPORTANT: Calculate optimal cell size based ONLY on the main grid dimensions
        // Formula: (availableSpace - padding - totalSpacing) / MAIN_GRID_SIZE
        // This ensures that only the visible 3x3 or 5x5 core grid fits the screen area
        // The wraparound/scrolling cells will use the same size but extend beyond the visible area
        float totalMainGridSpacing = (gridSize - 1) * spacing;
        float calculatedCellSize = (availableSpace - gridPadding * 2 - totalMainGridSpacing) / gridSize;
        
        // Clamp to min/max bounds
        _calculatedCellSize = Mathf.Clamp(calculatedCellSize, minCellSize, maxCellSize);

        Debug.Log($"📐 DETAILED CELL SIZE CALCULATION:");
        Debug.Log($"📐 ↳ Reference Size: {availableSize}");
        Debug.Log($"📐 ↳ Screen Percentage: {maxGridScreenPercentage * 100}%");
        Debug.Log($"📐 ↳ Available Width: {availableWidth}px, Available Height: {availableHeight}px");
        Debug.Log($"📐 ↳ Available Space (min): {availableSpace}px");
        Debug.Log($"📐 ↳ Grid Size: {gridSize}×{gridSize}");
        Debug.Log($"📐 ↳ Grid Padding (both sides): {gridPadding * 2}px");
        Debug.Log($"📐 ↳ Spacing between cells: {spacing}px × {gridSize - 1} = {totalMainGridSpacing}px");
        Debug.Log($"📐 ↳ Space for cells: {availableSpace} - {gridPadding * 2} - {totalMainGridSpacing} = {availableSpace - gridPadding * 2 - totalMainGridSpacing}px");
        Debug.Log($"📐 ↳ Raw calculated size: {calculatedCellSize}px");
        Debug.Log($"📐 ↳ Clamped size (min:{minCellSize}, max:{maxCellSize}): {_calculatedCellSize}px");
        Debug.Log($"📐 ↳ Total grid width: {gridSize * _calculatedCellSize + totalMainGridSpacing}px");
    }

    /// <summary>
    /// Update cell sizes for all existing cells without recreating them
    /// Called when screen size changes or grid size changes
    /// </summary>
    public void RefreshCellSizes()
    {
        if (!enableAdaptiveSizing) return;
        
        // Recalculate cell size
        CalculateAdaptiveCellSize();
        
        // Recalculate grid center offset
        CalculateGridCenterOffset();
        
        // Update all existing cells
        if (gridCells != null)
        {
            for (int r = 0; r < gridSize; r++)
            {
                for (int c = 0; c < gridSize; c++)
                {
                    if (gridCells[r, c] != null)
                    {
                        // Update cell size
                        RectTransform cellRectTransform = gridCells[r, c].GetComponent<RectTransform>();
                        if (cellRectTransform != null)
                        {
                            cellRectTransform.sizeDelta = new Vector2(cellSize, cellSize);
                        }
                        
                        // Update cell position
                        Vector2 newPos = GetBaseCellPosition(r, c);
                        gridCells[r, c].transform.localPosition = newPos;
                    }
                }
            }
        }
        
        // Update wraparound cells if they exist
        RefreshWraparoundCellSizes();
        
        Debug.Log($"📐 Refreshed cell sizes: NewCellSize={cellSize}");
    }

    /// <summary>
    /// Refresh wraparound cell sizes and positions
    /// </summary>
    private void RefreshWraparoundCellSizes()
    {
        if (!wraparoundInitialized) return;
        
        // Update horizontal wraparound cells
        if (_horizontalWrapCells != null)
        {
            for (int r = 0; r < gridSize; r++)
            {
                for (int i = 0; i < WRAP_COUNT * 2; i++)
                {
                    if (_horizontalWrapCells[r, i] != null)
                    {
                        RectTransform cellRect = _horizontalWrapCells[r, i].GetComponent<RectTransform>();
                        if (cellRect != null)
                        {
                            cellRect.sizeDelta = new Vector2(cellSize, cellSize);
                        }
                    }
                }
            }
        }
        
        // Update vertical wraparound cells
        if (_verticalWrapCells != null)
        {
            for (int c = 0; c < gridSize; c++)
            {
                for (int i = 0; i < WRAP_COUNT * 2; i++)
                {
                    if (_verticalWrapCells[c, i] != null)
                    {
                        RectTransform cellRect = _verticalWrapCells[c, i].GetComponent<RectTransform>();
                        if (cellRect != null)
                        {
                            cellRect.sizeDelta = new Vector2(cellSize, cellSize);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Public method to reinitialize grid for a new level
    /// </summary>
    public void ReinitializeForLevel()
    {
        // Clear existing grid
        ClearGrid();
        
        // Reinitialize with new grid size
        InitializeGridArrays();
        InitializeVisualOffsets();
        
        // Calculate adaptive cell size for new grid size
        CalculateAdaptiveCellSize();
        
        // Reinitialize the grid with new size
        InitializeGrid();
    }
    
    /// <summary>
    /// Clear existing grid cells
    /// </summary>
    void ClearGrid()
    {
        if (gridCells != null)
        {
            for (int r = 0; r < gridCells.GetLength(0); r++)
            {
                for (int c = 0; c < gridCells.GetLength(1); c++)
                {
                    if (gridCells[r, c] != null)
                    {
                        DestroyImmediate(gridCells[r, c].gameObject);
                    }
                }
            }
        }
        
        // Clear wraparound cells if they exist
        ClearWraparoundCells();
    }
    
    /// <summary>
    /// Clear wraparound cells
    /// </summary>
    void ClearWraparoundCells()
    {
        if (_horizontalWrapCells != null)
        {
            for (int r = 0; r < _horizontalWrapCells.GetLength(0); r++)
            {
                for (int i = 0; i < _horizontalWrapCells.GetLength(1); i++)
                {
                    if (_horizontalWrapCells[r, i] != null)
                    {
                        DestroyImmediate(_horizontalWrapCells[r, i].gameObject);
                    }
                }
            }
        }
        
        if (_verticalWrapCells != null)
        {
            for (int c = 0; c < _verticalWrapCells.GetLength(0); c++)
            {
                for (int i = 0; i < _verticalWrapCells.GetLength(1); i++)
                {
                    if (_verticalWrapCells[c, i] != null)
                    {
                        DestroyImmediate(_verticalWrapCells[c, i].gameObject);
                    }
                }
            }
        }
    }

    public void InitializeGrid()
    {
        Debug.Log($"📐 InitializeGrid called: LevelManager.Instance={LevelManager.Instance != null}");
        
        isAnimating = true; // Temporarily set for initial fade-in
        
        // Ensure we have grid arrays initialized for current grid size
        if (gridCells == null || gridCells.GetLength(0) != gridSize || gridCells.GetLength(1) != gridSize)
        {
            Debug.Log($"📐 Reinitializing grid arrays for size {gridSize}x{gridSize}");
            InitializeGridArrays();
        }
        
        // Calculate adaptive cell size based on current grid size
        CalculateAdaptiveCellSize();
        
        // Recalculate grid center offset with new cell size
        CalculateGridCenterOffset();
        
        PopulateGridData();

        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                GameObject cellGO;
                if (gridCells[r, c] == null)
                {
                    cellGO = Instantiate(letterCellPrefab, gridParent);
                    gridCells[r, c] = cellGO.GetComponent<CellController>();
                    if (gridCells[r, c] == null)
                    {
                        Debug.LogError($"WGM: Cell prefab {letterCellPrefab.name} is missing CellController script.", this);
                        Destroy(cellGO);
                        isAnimating = false;
                        return;
                    }
                }
                else
                {
                    cellGO = gridCells[r, c].gameObject;
                }

                CellController cell = gridCells[r, c];
                cell.gameObject.name = $"Cell_{r}_{c}";
                
                // Assign unique ID for tracking
                if (cell.uniqueID == -1)
                {
                    cell.SetUniqueID(nextUniqueID++);
                }

                RectTransform cellRectTransform = cellGO.GetComponent<RectTransform>();
                if (cellRectTransform != null)
                {
                    cellRectTransform.sizeDelta = new Vector2(cellSize, cellSize);
                }
                else
                {
                    Debug.LogWarning($"WGM: Cell_{r}_{c} prefab instance is missing RectTransform.", this);
                }

                cell.transform.localPosition = GetBaseCellPosition(r, c);
                cell.SetCellData(gridData[r, c]); // Updated to use new CellData system
                Image bgImage = cell.GetComponent<Image>();
                if (bgImage != null)
                {
                    bgImage.color = (r + c) % 2 == 0 ? cellColorPrimary : cellColorAlternate;
                }
                cell.StoreDefaultColor(); // Store color after setting it
                cell.FadeIn(cellFadeInDuration);
            }
        }

        // Now initialize wraparound cells
        InitializeWraparoundGrid();

        DOVirtual.DelayedCall(cellFadeInDuration + 0.1f, () =>
        {
            ResetAnimationFlag("InitializeGrid");
            TriggerValidationCheckAndHighlightUpdate();
            
            // IMMEDIATE TEST: Force some cells to specific colors to test the highlighting system
            DOVirtual.DelayedCall(1f, () => {
                TestHighlightingSystem();
            });
            
            // ADDITIONAL TEST: Test center row feedback after 3 seconds
            DOVirtual.DelayedCall(3f, () => {
                TestCenterRowWordleFeedback();
            });
        }, false);
    }
    
    /// <summary>
    /// Test method to directly apply colors to cells and verify the highlighting system works
    /// </summary>
    private void TestHighlightingSystem()
    {
        Debug.Log($"🧪 TESTING: Starting highlight system test");
        
        // Test different cells with different colors
        for (int r = 0; r < Mathf.Min(3, gridSize); r++)
        {
            for (int c = 0; c < Mathf.Min(3, gridSize); c++)
            {
                if (gridCells[r, c] != null)
                {
                    Color testColor;
                    string colorName;
                    
                    // Create a pattern to test different colors
                    if ((r + c) % 3 == 0)
                    {
                        testColor = presentLetterColor; // Yellow
                        colorName = "Yellow (Present)";
                    }
                    else if ((r + c) % 3 == 1)
                    {
                        testColor = correctLetterColor; // Green
                        colorName = "Green (Correct)";
                    }
                    else
                    {
                        testColor = absentLetterColor; // Gray
                        colorName = "Gray (Absent)";
                    }
                    
                    Debug.Log($"🧪 TEST: Setting cell ({r},{c}) to {colorName} = {testColor}");
                    gridCells[r, c].SetHighlightState(true, testColor);
                }
            }
        }
        
        Debug.Log($"🧪 TEST: Applied test colors - check if cells are visually colored!");
        Debug.Log($"🧪 TEST: presentLetterColor = {presentLetterColor}");
        Debug.Log($"🧪 TEST: correctLetterColor = {correctLetterColor}");
        Debug.Log($"🧪 TEST: absentLetterColor = {absentLetterColor}");
    }
    
    /// <summary>
    /// Test center row Wordle feedback specifically with multi-word scenario
    /// </summary>
    public void TestCenterRowWordleFeedback()
    {
        Debug.Log($"🎯 TESTING CENTER ROW: Starting multi-word center row feedback test");
        
        int centerRow = gridSize / 2;
        Debug.Log($"🎯 CENTER ROW: Testing row {centerRow} (gridSize = {gridSize})");
        
        // Clear all highlights first
        ClearAllCellHighlights(false);
        
        // Get the letters in the center row
        string centerRowWord = "";
        for (int c = 0; c < gridSize; c++)
        {
            if (gridCells[centerRow, c] != null)
            {
                char letter = GetLetterFromCellData(gridData[centerRow, c]);
                centerRowWord += letter;
            }
        }
        
        Debug.Log($"🎯 CENTER ROW: Center row contains: '{centerRowWord}'");
        
        // Test multiple target words for advanced feedback
        string[] testTargetWords = new string[] { "CATCH", "PREPS", "STARS" };
        
        Debug.Log($"🎯 CENTER ROW: Testing against multiple target words: [{string.Join(", ", testTargetWords)}]");
        
        // Apply the sophisticated multi-word feedback
        ApplyMultiWordFeedback(centerRowWord, testTargetWords);
        
        Debug.Log($"🎯 CENTER ROW: Applied multi-word feedback colors to center row!");
        Debug.Log($"🎯 COLOR LEGEND:");
        Debug.Log($"🎯 ↳ Green = Correct position in dominant word");
        Debug.Log($"🎯 ↳ Yellow = Wrong position in dominant word");
        Debug.Log($"🎯 ↳ Purple = Letter from different target word (interference)");
        Debug.Log($"🎯 ↳ Gray = Not in any target word");
    }
    
    /// <summary>
    /// Manual test method to demonstrate different multi-word scenarios
    /// Call this from Unity Inspector or Console to test specific cases
    /// </summary>
    [ContextMenu("Test Multi-Word Scenarios")]
    public void TestMultiWordScenarios()
    {
        Debug.Log($"🧪 MANUAL TEST: Starting comprehensive multi-word scenario tests");
        
        // Test scenarios with different letter combinations
        string[] testScenarios = new string[]
        {
            "CATCH", // Perfect match with first word
            "CATPR", // Mix of CATCH and PREPS - C from CATCH should be dominant
            "PRCAT", // P from PREPS first, then mix
            "STARS", // Perfect match with third word
            "STARP", // STARS with interference from PREPS
            "CSTAR"  // Mixed start - C from CATCH, then STARS letters
        };
        
        string[] targetWords = new string[] { "CATCH", "PREPS", "STARS" };
        
        int testIndex = 0;
        foreach (string testScenario in testScenarios)
        {
            testIndex++;
            Debug.Log($"🧪 TEST SCENARIO {testIndex}: Testing '{testScenario}' vs [{string.Join(", ", targetWords)}]");
            
            // Simulate this scenario by temporarily setting center row letters
            SetCenterRowForTesting(testScenario);
            
            // Apply multi-word feedback
            ApplyMultiWordFeedback(testScenario, targetWords);
            
            // Wait a moment for visual inspection
            Debug.Log($"🧪 SCENARIO {testIndex} COMPLETE - Check the center row colors!");
            Debug.Log($"🧪 ----");
        }
    }
    
    /// <summary>
    /// Helper method to set center row letters for testing purposes
    /// </summary>
    private void SetCenterRowForTesting(string testWord)
    {
        int centerRow = gridSize / 2;
        
        for (int c = 0; c < gridSize && c < testWord.Length; c++)
        {
            if (gridCells[centerRow, c] != null)
            {
                // Update the grid data for this test
                gridData[centerRow, c] = CellData.CreateLetterCell(testWord[c]);
                gridCells[centerRow, c].SetCellData(gridData[centerRow, c]);
            }
        }
        
        Debug.Log($"🧪 SETUP: Set center row to '{testWord}'");
    }
    
    /// <summary>
    /// Data structure to track how well each target word matches the center row
    /// </summary>
    [System.Serializable]
    public class WordMatchAnalysis
    {
        public string targetWord;
        public int correctPositions;      // Green - exact matches
        public int wrongPositions;       // Yellow - letter exists but wrong position
        public int totalMatches;         // Total letters that exist in this word
        public float matchScore;         // Overall score for this word
        public bool hasUniqueLetters;    // Does this word have letters not in other targets?
        public int firstUniquePosition; // Position of first letter unique to this word (-1 if none)
        
        public WordMatchAnalysis(string word)
        {
            targetWord = word.ToUpper();
            correctPositions = 0;
            wrongPositions = 0;
            totalMatches = 0;
            matchScore = 0f;
            hasUniqueLetters = false;
            firstUniquePosition = -1;
        }
    }
    
    /// <summary>
    /// Check and apply advanced multi-word Wordle feedback for the center row
    /// </summary>
    private void CheckCenterRowWordleFeedback()
    {
        // Only apply Wordle feedback if we're in Wordle mode
        LevelData currentLevel = LevelManager.Instance?.CurrentLevel;
        bool isWordleStyle = currentLevel?.IsWordleStyle ?? false;
        
        if (!isWordleStyle)
        {
            Debug.Log($"🎯 CENTER ROW: Not in Wordle mode, skipping center row feedback");
            return;
        }
        
        int centerRow = gridSize / 2;
        Debug.Log($"🎯 MULTI-WORD: Analyzing center row {centerRow} for multi-word feedback");
        
        // Get the letters in the center row
        string centerRowWord = "";
        for (int c = 0; c < gridSize; c++)
        {
            if (gridCells[centerRow, c] != null)
            {
                char letter = GetLetterFromCellData(gridData[centerRow, c]);
                centerRowWord += letter;
            }
        }
        
        Debug.Log($"🎯 MULTI-WORD: Center row contains: '{centerRowWord}'");
        
        // Get target words from level data
        string[] targetWords = currentLevel?.TargetWords;
        if (targetWords == null || targetWords.Length == 0)
        {
            Debug.LogWarning($"🎯 MULTI-WORD: No target words available for feedback");
            return;
        }
        
        // If only one target word, use simple single-word logic
        if (targetWords.Length == 1)
        {
            ApplySingleWordFeedback(centerRowWord, targetWords[0]);
            return;
        }
        
        // Multi-word analysis
        ApplyMultiWordFeedback(centerRowWord, targetWords);
    }
    
    /// <summary>
    /// Apply simple single-word Wordle feedback
    /// </summary>
    private void ApplySingleWordFeedback(string centerRowWord, string targetWord)
    {
        int centerRow = gridSize / 2;
        targetWord = targetWord.ToUpper();
        
        Debug.Log($"🎯 SINGLE-WORD: Applying feedback against target word: '{targetWord}'");
        
        // Apply traditional Wordle feedback
        for (int c = 0; c < gridSize && c < centerRowWord.Length; c++)
        {
            if (gridCells[centerRow, c] != null)
            {
                char currentLetter = char.ToUpper(centerRowWord[c]);
                Color feedbackColor;
                string feedbackName;
                
                if (c < targetWord.Length && char.ToUpper(targetWord[c]) == currentLetter)
                {
                    // Correct position
                    feedbackColor = correctLetterColor;
                    feedbackName = "Correct (Green)";
                }
                else if (targetWord.Contains(currentLetter))
                {
                    // Present but wrong position
                    feedbackColor = presentLetterColor;
                    feedbackName = "Present (Yellow)";
                }
                else
                {
                    // Not in word
                    feedbackColor = absentLetterColor;
                    feedbackName = "Absent (Gray)";
                }
                
                Debug.Log($"🎯 SINGLE-WORD: Letter '{currentLetter}' at position {c} → {feedbackName}");
                gridCells[centerRow, c].SetHighlightState(true, feedbackColor);
            }
        }
    }
    
    /// <summary>
    /// Apply sophisticated multi-word feedback with dominant word detection
    /// </summary>
    private void ApplyMultiWordFeedback(string centerRowWord, string[] targetWords)
    {
        int centerRow = gridSize / 2;
        
        Debug.Log($"🎯 MULTI-WORD: Analyzing {targetWords.Length} target words: [{string.Join(", ", targetWords)}]");
        
        // Analyze each target word against the center row
        List<WordMatchAnalysis> analyses = new List<WordMatchAnalysis>();
        
        foreach (string targetWord in targetWords)
        {
            WordMatchAnalysis analysis = AnalyzeWordMatch(centerRowWord, targetWord, targetWords);
            analyses.Add(analysis);
            Debug.Log($"🎯 ANALYSIS: '{analysis.targetWord}' - Correct:{analysis.correctPositions}, Wrong:{analysis.wrongPositions}, Score:{analysis.matchScore:F2}, UniqueAt:{analysis.firstUniquePosition}");
        }
        
        // Determine dominant word using your specified logic
        WordMatchAnalysis dominantWord = DetermineDominantWord(analyses, centerRowWord);
        
        if (dominantWord == null)
        {
            Debug.Log($"🎯 MULTI-WORD: No clear dominant word found - applying neutral feedback");
            ApplyNeutralFeedback(centerRowWord, targetWords);
            return;
        }
        
        Debug.Log($"🎯 MULTI-WORD: Dominant word determined: '{dominantWord.targetWord}'");
        
        // Apply feedback based on dominant word with interference detection
        ApplyDominantWordFeedback(centerRowWord, dominantWord, targetWords);
    }
    
    /// <summary>
    /// Analyze how well a specific target word matches the center row
    /// </summary>
    private WordMatchAnalysis AnalyzeWordMatch(string centerRowWord, string targetWord, string[] allTargetWords)
    {
        WordMatchAnalysis analysis = new WordMatchAnalysis(targetWord);
        string upperCenterRow = centerRowWord.ToUpper();
        string upperTargetWord = targetWord.ToUpper();
        
        // Count correct positions and wrong positions
        for (int i = 0; i < Math.Min(upperCenterRow.Length, upperTargetWord.Length); i++)
        {
            char centerLetter = upperCenterRow[i];
            
            if (upperTargetWord[i] == centerLetter)
            {
                analysis.correctPositions++;
                analysis.totalMatches++;
            }
            else if (upperTargetWord.Contains(centerLetter))
            {
                analysis.wrongPositions++;
                analysis.totalMatches++;
            }
        }
        
        // Check for unique letters and find first unique position
        for (int i = 0; i < Math.Min(upperCenterRow.Length, upperTargetWord.Length); i++)
        {
            char centerLetter = upperCenterRow[i];
            
            // Check if this letter at this position is unique to this target word
            bool isUniqueToThisWord = true;
            foreach (string otherWord in allTargetWords)
            {
                if (otherWord.ToUpper() == upperTargetWord) continue; // Skip self
                
                if (i < otherWord.Length && otherWord.ToUpper()[i] == centerLetter)
                {
                    isUniqueToThisWord = false;
                    break;
                }
            }
            
            if (isUniqueToThisWord && upperTargetWord[i] == centerLetter)
            {
                analysis.hasUniqueLetters = true;
                if (analysis.firstUniquePosition == -1)
                {
                    analysis.firstUniquePosition = i;
                }
            }
        }
        
        // Calculate match score (weighted toward correct positions)
        analysis.matchScore = (analysis.correctPositions * 2.0f) + (analysis.wrongPositions * 1.0f);
        
        // Bonus for having unique letters early
        if (analysis.hasUniqueLetters && analysis.firstUniquePosition != -1)
        {
            analysis.matchScore += (5.0f - analysis.firstUniquePosition); // Earlier unique letters get higher bonus
        }
        
        return analysis;
    }
    
    /// <summary>
    /// Determine the dominant word based on your specified logic:
    /// 1. First unique letter determines dominance
    /// 2. If ambiguous, use next distinguishing letter or random selection
    /// </summary>
    private WordMatchAnalysis DetermineDominantWord(List<WordMatchAnalysis> analyses, string centerRowWord)
    {
        // Filter out words with no matches at all
        var viableWords = analyses.Where(w => w.totalMatches > 0).ToList();
        
        if (viableWords.Count == 0)
        {
            Debug.Log($"🎯 DOMINANT: No viable words found");
            return null;
        }
        
        if (viableWords.Count == 1)
        {
            Debug.Log($"🎯 DOMINANT: Only one viable word: '{viableWords[0].targetWord}'");
            return viableWords[0];
        }
        
        // Look for the word with the earliest unique correct letter
        var wordsWithUniqueLetters = viableWords.Where(w => w.hasUniqueLetters && w.firstUniquePosition != -1).ToList();
        
        if (wordsWithUniqueLetters.Count == 1)
        {
            Debug.Log($"🎯 DOMINANT: Single word with unique letters: '{wordsWithUniqueLetters[0].targetWord}' at position {wordsWithUniqueLetters[0].firstUniquePosition}");
            return wordsWithUniqueLetters[0];
        }
        
        if (wordsWithUniqueLetters.Count > 1)
        {
            // Find the word with the earliest unique letter
            var earliestUnique = wordsWithUniqueLetters.OrderBy(w => w.firstUniquePosition).First();
            Debug.Log($"🎯 DOMINANT: Earliest unique letter at position {earliestUnique.firstUniquePosition}: '{earliestUnique.targetWord}'");
            return earliestUnique;
        }
        
        // If no unique letters, use highest match score
        var bestMatch = viableWords.OrderByDescending(w => w.matchScore).First();
        var topMatches = viableWords.Where(w => Math.Abs(w.matchScore - bestMatch.matchScore) < 0.1f).ToList();
        
        if (topMatches.Count == 1)
        {
            Debug.Log($"🎯 DOMINANT: Best match score: '{bestMatch.targetWord}' with score {bestMatch.matchScore:F2}");
            return bestMatch;
        }
        
        // If tied, pick randomly among top matches
        var randomChoice = topMatches[UnityEngine.Random.Range(0, topMatches.Count)];
        Debug.Log($"🎯 DOMINANT: Random choice among {topMatches.Count} tied words: '{randomChoice.targetWord}'");
        return randomChoice;
    }
    
    /// <summary>
    /// Apply neutral feedback when no dominant word can be determined
    /// </summary>
    private void ApplyNeutralFeedback(string centerRowWord, string[] targetWords)
    {
        int centerRow = gridSize / 2;
        
        for (int c = 0; c < gridSize && c < centerRowWord.Length; c++)
        {
            if (gridCells[centerRow, c] != null)
            {
                char currentLetter = char.ToUpper(centerRowWord[c]);
                
                // Check if letter exists in any target word
                bool existsInAnyWord = false;
                foreach (string targetWord in targetWords)
                {
                    if (targetWord.ToUpper().Contains(currentLetter))
                    {
                        existsInAnyWord = true;
                        break;
                    }
                }
                
                Color feedbackColor = existsInAnyWord ? presentLetterColor : absentLetterColor;
                string feedbackName = existsInAnyWord ? "Present in some word (Yellow)" : "Not in any word (Gray)";
                
                Debug.Log($"🎯 NEUTRAL: Letter '{currentLetter}' at position {c} → {feedbackName}");
                gridCells[centerRow, c].SetHighlightState(true, feedbackColor);
            }
        }
    }
    
    /// <summary>
    /// Apply feedback based on the dominant word with interference detection
    /// </summary>
    private void ApplyDominantWordFeedback(string centerRowWord, WordMatchAnalysis dominantWord, string[] allTargetWords)
    {
        int centerRow = gridSize / 2;
        string dominantWordUpper = dominantWord.targetWord.ToUpper();
        
        for (int c = 0; c < gridSize && c < centerRowWord.Length; c++)
        {
            if (gridCells[centerRow, c] != null)
            {
                char currentLetter = char.ToUpper(centerRowWord[c]);
                Color feedbackColor;
                string feedbackName;
                
                // Check position in dominant word
                if (c < dominantWordUpper.Length && dominantWordUpper[c] == currentLetter)
                {
                    // Correct position in dominant word
                    feedbackColor = correctLetterColor;
                    feedbackName = "Correct (Green)";
                }
                else if (dominantWordUpper.Contains(currentLetter))
                {
                    // Present in dominant word but wrong position
                    feedbackColor = presentLetterColor;
                    feedbackName = "Present (Yellow)";
                }
                else
                {
                    // Not in dominant word - check for interference
                    bool isInterference = IsInterferenceLetter(currentLetter, c, dominantWord, allTargetWords);
                    
                    if (isInterference)
                    {
                        feedbackColor = interferenceLetterColor;
                        feedbackName = "Interference (Purple)";
                    }
                    else
                    {
                        feedbackColor = absentLetterColor;
                        feedbackName = "Absent (Gray)";
                    }
                }
                
                Debug.Log($"🎯 DOMINANT: Letter '{currentLetter}' at position {c} → {feedbackName} (vs '{dominantWord.targetWord}')");
                gridCells[centerRow, c].SetHighlightState(true, feedbackColor);
            }
        }
    }
    
    /// <summary>
    /// Check if a letter is interfering (from a different target word)
    /// </summary>
    private bool IsInterferenceLetter(char letter, int position, WordMatchAnalysis dominantWord, string[] allTargetWords)
    {
        // Letter must not be in the dominant word
        if (dominantWord.targetWord.ToUpper().Contains(letter))
        {
            return false;
        }
        
        // Check if it exists in any other target word
        foreach (string targetWord in allTargetWords)
        {
            string upperTargetWord = targetWord.ToUpper();
            if (upperTargetWord == dominantWord.targetWord.ToUpper()) continue; // Skip dominant word
            
            if (upperTargetWord.Contains(letter))
            {
                Debug.Log($"🎯 INTERFERENCE: Letter '{letter}' at position {position} belongs to '{targetWord}' (not dominant '{dominantWord.targetWord}')");
                return true;
            }
        }
        
        return false;
    }

    void PopulateGridData()
    {
        bool isWordleStyle = currentLevelData?.IsWordleStyle ?? false;
        
        Debug.Log($"📐 PopulateGridData: LevelManager={levelManager != null}, CurrentLevel={currentLevelData != null}, IsWordleStyle={isWordleStyle}");
        
        if (isWordleStyle && currentLevelData != null)
        {
            PopulateWordleStyleGrid(currentLevelData);
        }
        else
        {
            PopulateScrabbleStyleGrid();
        }
    }
    
    void PopulateScrabbleStyleGrid()
    {
        int blankCount = 0;
        int totalCells = gridSize * gridSize;
        
        Debug.Log($"📋 PopulateScrabbleStyleGrid: gridSize={gridSize}, totalCells={totalCells}");
        
        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                Vector2Int position = new Vector2Int(c, r);
                
                if (cellTypeManager != null)
                {
                    // Use CellTypeManager to generate cells (existing behavior)
                    gridData[r, c] = cellTypeManager.GenerateCell(blankCount, r * gridSize + c, position, gridSize);
                    
                    if (gridData[r, c].IsBlank)
                    {
                        blankCount++;
                    }
                }
                else
                {
                    // Fallback: Generate standard letter cells
                    char randomLetter = GetRandomLetter();
                    gridData[r, c] = CellData.CreateLetterCell(randomLetter);
                }
            }
        }
        
        Debug.Log($"📋 Scrabble Style grid populated with {blankCount} blank cells out of {totalCells} total cells");
    }
    
    void PopulateWordleStyleGrid(LevelData levelData)
    {
        // Add null check at the beginning
        if (levelData == null)
        {
            Debug.LogWarning("PopulateWordleStyleGrid called with null levelData, falling back to Scrabble style population");
            PopulateScrabbleStyleGrid();
            return;
        }
        
        int totalCells = gridSize * gridSize;
        
        // Get ALL target word letters (including duplicates) that must be guaranteed in the grid
        char[] targetLetters = levelData.GetTargetWordLetters();
        List<char> guaranteedLetters = new List<char>(targetLetters);
        
        // Use custom letter set if defined, otherwise use default alphabet
        string letterSet = !string.IsNullOrEmpty(levelData.CustomLetterSet) 
            ? levelData.CustomLetterSet.ToUpper() 
            : "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        
        Debug.Log($"🎯 Wordle Style Grid Population:");
        Debug.Log($"🎯 ↳ Target Words: [{string.Join(", ", levelData.TargetWords)}]");
        Debug.Log($"🎯 ↳ Total target letters (with duplicates): {guaranteedLetters.Count}");
        Debug.Log($"🎯 ↳ Target letters: [{string.Join(", ", guaranteedLetters)}]");
        Debug.Log($"🎯 ↳ Grid cells available: {totalCells}");
        Debug.Log($"🎯 ↳ Remaining cells for random letters: {totalCells - guaranteedLetters.Count}");
        
        // Check if we have enough space for all target letters
        if (guaranteedLetters.Count > totalCells)
        {
            Debug.LogError($"🎯 ERROR: Not enough grid space! Need {guaranteedLetters.Count} cells for target letters but only have {totalCells} total cells.");
            // Fallback: trim excess letters
            guaranteedLetters = guaranteedLetters.GetRange(0, totalCells);
            Debug.LogWarning($"🎯 Trimmed to {guaranteedLetters.Count} letters to fit grid.");
        }
        
        // Create list of positions to fill
        List<Vector2Int> positions = new List<Vector2Int>();
        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                positions.Add(new Vector2Int(c, r));
            }
        }
        
        // Shuffle positions for random placement
        for (int i = 0; i < positions.Count; i++)
        {
            var temp = positions[i];
            int randomIndex = UnityEngine.Random.Range(i, positions.Count);
            positions[i] = positions[randomIndex];
            positions[randomIndex] = temp;
        }
        
        int positionIndex = 0;
        
        // First, place ALL guaranteed target letters (including duplicates)
        foreach (char letter in guaranteedLetters)
        {
            if (positionIndex < positions.Count)
            {
                Vector2Int pos = positions[positionIndex];
                gridData[pos.y, pos.x] = CellData.CreateLetterCell(letter);
                positionIndex++;
            }
        }
        
        // Fill remaining positions with random letters from the letter set
        for (int i = positionIndex; i < positions.Count; i++)
        {
            Vector2Int pos = positions[i];
            char randomLetter = letterSet[UnityEngine.Random.Range(0, letterSet.Length)];
            gridData[pos.y, pos.x] = CellData.CreateLetterCell(randomLetter);
        }
        
        Debug.Log($"🎯 Grid populated: {guaranteedLetters.Count} target letters + {totalCells - guaranteedLetters.Count} random letters = {totalCells} total");
    }

    /// <summary>
    /// Get random letter (now delegates to CellTypeManager for consistency)
    /// </summary>
    char GetRandomLetter()
    {
        if (cellTypeManager != null)
        {
            return cellTypeManager.GetRandomLetter();
        }
        
        // Ultimate fallback if no CellTypeManager is assigned
        return (char)('A' + UnityEngine.Random.Range(0, 26));
    }

    /// <summary>
    /// Compatibility method to extract char from CellData for legacy systems
    /// </summary>
    public char GetLetterFromCellData(CellData cellData)
    {
        if (cellData.IsBlank)
            return ' '; // Return space for blank cells
        return cellData.letterValue;
    }

    /// <summary>
    /// Get cell data at specific position (new interface)
    /// </summary>
    public CellData GetCellDataAtPosition(Vector2Int position)
    {
        if (position.x >= 0 && position.x < gridSize && position.y >= 0 && position.y < gridSize)
        {
            return gridData[position.y, position.x]; // Note: gridData is [row, col] format
        }
        return CellData.CreateLetterCell(' '); // Return default empty cell
    }

    /// <summary>
    /// Get cell data at specific position (overload)
    /// </summary>
    public CellData GetCellDataAtPosition(int row, int col)
    {
        if (row >= 0 && row < gridSize && col >= 0 && col < gridSize)
        {
            return gridData[row, col];
        }
        return CellData.CreateLetterCell(' '); // Return default empty cell
    }

    public void SetRowVisualOffset(int rowIndex, float currentFrameVisualOffset)
    {
        if (rowIndex < 0 || rowIndex >= gridSize) return;
        
        // Use our wraparound scroll method
        UpdateScrollVisualsWithWrap(rowIndex, true, currentFrameVisualOffset);
    }

    public void SetColumnVisualOffset(int colIndex, float currentFrameVisualOffset)
    {
        if (colIndex < 0 || colIndex >= gridSize) return;
        
        // Use our wraparound scroll method
        UpdateScrollVisualsWithWrap(colIndex, false, currentFrameVisualOffset);
    }

    public void SnapRowToGrid(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= gridSize) return;
        if (!wraparoundInitialized) InitializeWraparoundGrid(); 

        // First, update all cell positions to their snapped state (offset 0)
        UpdateScrollVisualsWithWrap(rowIndex, true, 0f);

        // Then, ensure all letters in this row (main and wraparound) are correct based on current gridData
        // Main grid cells
        for (int c = 0; c < gridSize; c++)
        {
            if (gridCells[rowIndex, c] != null)
            {
                gridCells[rowIndex, c].SetCellData(gridData[rowIndex, c]);
            }
        }
        // Horizontal wraparound cells for this row
        for (int i = 0; i < WRAP_COUNT * 2; i++)
        {
            CellController wrapCell = _horizontalWrapCells[rowIndex, i];
            if (wrapCell != null)
            {
                int visualCol = _visualColOffsets[i];
                int dataCol = (visualCol % gridSize + gridSize) % gridSize;
                wrapCell.SetCellData(gridData[rowIndex, dataCol]);
            }
        }

        // NEW: Update vertical wraparound cells that display data from the snapped (and now shifted) row
        // These are vertical cells in other columns, whose data source row is the current rowIndex.
        for (int c = 0; c < gridSize; c++) // Iterate through each column index
        {
            for (int i = 0; i < WRAP_COUNT * 2; i++) // Iterate through all vertical wraparound cells FOR THAT COLUMN 'c'
            {
                CellController vertWrapCell = _verticalWrapCells[c, i];
                if (vertWrapCell != null)
                {
                    int visualRowForWrap = _visualRowOffsets[i]; // The visual row offset for this specific wrap cell
                    int dataRowForWrap = (visualRowForWrap % gridSize + gridSize) % gridSize; // The actual data row it's supposed to display

                    // If the data row this vertical wrap cell is supposed to display IS the row that just got shifted...
                    if (dataRowForWrap == rowIndex)
                    {
                        // ...then update its letter from the (potentially new) gridData at [rowIndex, c]
                        vertWrapCell.SetCellData(gridData[rowIndex, c]);
                    }
                }
            }
        }

        // CHECK: Apply Wordle feedback if this is the center row
        CheckCenterRowWordleFeedback();
    }

    public void SnapColumnToGrid(int colIndex)
    {
        if (colIndex < 0 || colIndex >= gridSize) return;
        if (!wraparoundInitialized) InitializeWraparoundGrid(); 

        // First, update all cell positions to their snapped state (offset 0)
        UpdateScrollVisualsWithWrap(colIndex, false, 0f);

        // Then, ensure all letters in this column (main and wraparound) are correct based on current gridData
        // Main grid cells
        for (int r = 0; r < gridSize; r++)
        {
            if (gridCells[r, colIndex] != null)
            {
                gridCells[r, colIndex].SetCellData(gridData[r, colIndex]);
            }
        }
        // Vertical wraparound cells for this column
        for (int i = 0; i < WRAP_COUNT * 2; i++)
        {
            CellController wrapCell = _verticalWrapCells[colIndex, i];
            if (wrapCell != null)
            {
                int visualRow = _visualRowOffsets[i];
                int dataRow = (visualRow % gridSize + gridSize) % gridSize;
                wrapCell.SetCellData(gridData[dataRow, colIndex]);
            }
        }

        // NEW: Update horizontal wraparound cells that display data from the snapped (and now shifted) column
        // These are horizontal cells in other rows, whose data source column is the current colIndex.
        for (int r = 0; r < gridSize; r++) // Iterate through each row index
        {
            for (int i = 0; i < WRAP_COUNT * 2; i++) // Iterate through all horizontal wraparound cells FOR THAT ROW 'r'
            {
                CellController horizWrapCell = _horizontalWrapCells[r, i];
                if (horizWrapCell != null)
                {
                    int visualColForWrap = _visualColOffsets[i]; // The visual col offset for this specific wrap cell
                    int dataColForWrap = (visualColForWrap % gridSize + gridSize) % gridSize; // The actual data col it's supposed to display

                    // If the data col this horizontal wrap cell is supposed to display IS the col that just got shifted...
                    if (dataColForWrap == colIndex)
                    {
                        // ...then update its letter from the (potentially new) gridData at [r, colIndex]
                        horizWrapCell.SetCellData(gridData[r, colIndex]);
                    }
                }
            }
        }

        // CHECK: Apply Wordle feedback if this affects the center row
        CheckCenterRowWordleFeedback();
    }

    // Shifts data AND refreshes letters on ALL relevant extended cells
    public void ShiftRowDataAndRefresh(int rowIndex, int cellsToShift)
    {
        if (rowIndex < 0 || rowIndex >= gridSize || cellsToShift == 0) return;
        if (!wraparoundInitialized) InitializeWraparoundGrid(); 

        int direction = Math.Sign(cellsToShift);
        for (int i = 0; i < Math.Abs(cellsToShift); i++)
        {
            ShiftRowDataInternal(rowIndex, direction); 
        }
        
        SnapRowToGrid(rowIndex); // This will call UpdateScrollVisualsWithWrap with offset 0
    }

    public void ShiftColumnDataAndRefresh(int colIndex, int cellsToShift)
    {
        if (colIndex < 0 || colIndex >= gridSize || cellsToShift == 0) return;
        if (!wraparoundInitialized) InitializeWraparoundGrid();

        int direction = Math.Sign(cellsToShift);
        for (int i = 0; i < Math.Abs(cellsToShift); i++)
        {
            ShiftColumnDataInternal(colIndex, direction); 
        }
        
        SnapColumnToGrid(colIndex); // This will call UpdateScrollVisualsWithWrap with offset 0
    }

    // Internal method for single step data shift
    private void ShiftRowDataInternal(int rowIndex, int direction)
    {
        if (direction == 0) return;
        CellData[] tempRow = new CellData[gridSize];
        for (int c = 0; c < gridSize; c++) tempRow[c] = gridData[rowIndex, c];

        for (int c = 0; c < gridSize; c++)
        {
            int prevC = (c - direction + gridSize) % gridSize; // direction 1 means data from left moves to current
            gridData[rowIndex, c] = tempRow[prevC];
        }
    }

    private void ShiftColumnDataInternal(int colIndex, int direction)
    {
        if (direction == 0) return;
        CellData[] tempCol = new CellData[gridSize];
        for (int r = 0; r < gridSize; r++) tempCol[r] = gridData[r, colIndex];

        for (int r = 0; r < gridSize; r++)
        {
            int prevR = (r - direction + gridSize) % gridSize; // direction 1 means data from top moves to current
            gridData[r, colIndex] = tempCol[prevR];
        }
    }

    private void RefreshCellLettersInRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= gridSize) return;
        for (int c = 0; c < gridSize; c++)
        {
            if (gridCells[rowIndex, c] != null)
            {
                gridCells[rowIndex, c].SetCellData(gridData[rowIndex, c]);
            }
        }
    }

    private void RefreshCellLettersInColumn(int colIndex)
    {
        if (colIndex < 0 || colIndex >= gridSize) return;
        for (int r = 0; r < gridSize; r++)
        {
            if (gridCells[r, colIndex] != null)
            {
                gridCells[r, colIndex].SetCellData(gridData[r, colIndex]);
            }
        }
    }

    private void ResetAnimationFlag(string reason = "")
    {
        isAnimating = false;
    }

    public void ReplaceLettersAt(List<Vector2Int> coordinates, bool fadeIn = false)
    {
        if (coordinates == null || coordinates.Count == 0) return;
        // isAnimating = true; // Potentially set this at the start of the sequence if DOTween is used.

        Sequence replacementSequence = DOTween.Sequence(); 

        foreach (var coord in coordinates)
        {
            if (coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
            {
                CellController mainCell = gridCells[coord.x, coord.y]; 
                if (mainCell != null)
                {
                    mainCell.SetHighlightState(false, mainCell.GetDefaultColor()); 

                    gridData[coord.x, coord.y] = CellData.CreateLetterCell(GetRandomLetter()); 
                    CellData newCellData = gridData[coord.x, coord.y]; 

                    if (fadeIn)
                    {
                        mainCell.SetAlpha(0f); 
                        replacementSequence.AppendCallback(() => {
                            mainCell.SetCellData(newCellData); 
                            Image bgImage = mainCell.GetComponent<Image>();
                            if (bgImage != null)
                            {
                                bgImage.color = (coord.x + coord.y) % 2 == 0 ? cellColorPrimary : cellColorAlternate;
                                mainCell.StoreDefaultColor(); 
                            }
                        });
                        replacementSequence.Append(mainCell.GetComponent<CanvasGroup>().DOFade(1f, cellFadeInDuration * 0.75f)); 
                    }
                    else
                    {
                        mainCell.SetCellData(newCellData);
                        Image bgImage = mainCell.GetComponent<Image>();
                        if (bgImage != null)
                        {
                            bgImage.color = (coord.x + coord.y) % 2 == 0 ? cellColorPrimary : cellColorAlternate;
                            mainCell.StoreDefaultColor();
                        }
                        mainCell.SetAlpha(1f); 
                    }

                    // Update corresponding wraparound cells immediately
                    if (wraparoundInitialized)
                    {
                        // Horizontal wraparound cells for the affected row
                        for (int i = 0; i < WRAP_COUNT * 2; i++)
                        {
                            CellController wrapCell = _horizontalWrapCells[coord.x, i];
                            if (wrapCell != null)
                            {
                                int visualCol = _visualColOffsets[i];
                                int mirroredDataCol = (visualCol % gridSize + gridSize) % gridSize;
                                if (mirroredDataCol == coord.y)
                                {
                                    wrapCell.SetCellData(newCellData);
                                }
                            }
                        }

                        // Vertical wraparound cells for the affected column
                        for (int i = 0; i < WRAP_COUNT * 2; i++)
                        {
                            CellController wrapCell = _verticalWrapCells[coord.y, i];
                            if (wrapCell != null)
                            {
                                int visualRow = _visualRowOffsets[i];
                                int mirroredDataRow = (visualRow % gridSize + gridSize) % gridSize;
                                if (mirroredDataRow == coord.x)
                                {
                                    wrapCell.SetCellData(newCellData);
                                }
                            }
                        }
                    }
                }
            }
        }

        replacementSequence.OnComplete(() => {
            // ResetAnimationFlag("ReplaceLettersAt"); // Assuming ResetAnimationFlag is handled elsewhere or not needed for this sequence type
            TriggerValidationCheckAndHighlightUpdate();
        });
    }

    public void TriggerValidationCheckAndHighlightUpdate()
    {
        if (wordValidator == null) return;
        if (gameManager != null && isAnimating && gameManager.CurrentStatePublic != GameManager.GameState.Initializing)
        {
            return;
        }
        
        // Check if we're in Wordle mode for special feedback handling
        LevelData currentLevel = LevelManager.Instance?.CurrentLevel;
        bool isWordleStyle = currentLevel?.IsWordleStyle ?? false;
        
        Debug.Log($"🎯 TriggerValidationCheckAndHighlightUpdate: CurrentLevel={currentLevel != null}, IsWordleStyle={isWordleStyle}");
        if (currentLevel != null)
        {
            Debug.Log($"🎯 Level details: GameMode={currentLevel.GameMode}, TargetWords=[{(currentLevel.TargetWords != null ? string.Join(", ", currentLevel.TargetWords) : "null")}]");
        }
        
        if (isWordleStyle)
        {
            // Use Wordle-style feedback highlighting
            Debug.Log($"🎯 Using Wordle-style feedback highlighting");
            TriggerWordleFeedbackHighlighting();
        }
        else
        {
            // Use regular word highlighting
            Debug.Log($"🎯 Using regular word highlighting");
            List<FoundWordData> potentialWords = wordValidator.FindAllPotentialWords();
            Dictionary<System.Guid, Color> appliedColors = HighlightPotentialWordCells(potentialWords);
            if (gameManager != null)
            {
                gameManager.UpdatePotentialWordsDisplay(potentialWords, appliedColors);
            }
        }
    }
    
    /// <summary>
    /// Apply Wordle-style feedback highlighting to cells
    /// </summary>
    private void TriggerWordleFeedbackHighlighting()
    {
        if (wordValidator == null) return;
        
        List<FoundWordData> potentialWords = wordValidator.FindAllPotentialWords();
        Dictionary<System.Guid, Color> appliedColors = new Dictionary<System.Guid, Color>();
        
        // Clear existing highlights
        ClearAllCellHighlights(false);
        
        Debug.Log($"🎯 Wordle Feedback: Processing {potentialWords.Count} potential words for feedback highlighting");
        
        // CHECK: If no level is configured, create a temporary test scenario
        LevelData currentLevel = LevelManager.Instance?.CurrentLevel;
        if (currentLevel == null || !currentLevel.IsWordleStyle || currentLevel.TargetWords == null || currentLevel.TargetWords.Length == 0)
        {
            Debug.LogWarning($"🎯 No proper Wordle level detected! Creating test scenario...");
            Debug.LogWarning($"🎯 CurrentLevel: {currentLevel != null}, IsWordleStyle: {currentLevel?.IsWordleStyle}, TargetWords: {currentLevel?.TargetWords?.Length ?? 0}");
            
            // For testing purposes, let's assume any 3+ letter word should get feedback
            // and create some dummy target words for testing
            string[] testTargetWords = new string[] { "BOOK", "COOL", "LOOP", "TOOL" };
            
            foreach (var wordData in potentialWords)
            {
                if (wordData.Coordinates == null || wordData.Coordinates.Count == 0) continue;
                if (wordData.Word.Length < 3) continue; // Skip short words
                
                Debug.Log($"🎯 TEST MODE: Analyzing word '{wordData.Word}' with test targets");
                
                // Create manual feedback for testing
                LetterFeedback[] testFeedbacks = new LetterFeedback[wordData.Word.Length];
                for (int i = 0; i < wordData.Word.Length; i++)
                {
                    char letter = char.ToUpper(wordData.Word[i]);
                    
                    // Simple test logic: if letter is in any test target word
                    bool foundInTarget = false;
                    foreach (string testTarget in testTargetWords)
                    {
                        if (testTarget.Contains(letter))
                        {
                            foundInTarget = true;
                            break;
                        }
                    }
                    
                    if (foundInTarget)
                    {
                        // For testing, make every other letter "Present" (yellow)
                        testFeedbacks[i] = (i % 2 == 0) ? LetterFeedback.Present : LetterFeedback.Correct;
                    }
                    else
                    {
                        testFeedbacks[i] = LetterFeedback.None;
                    }
                }
                
                // Apply test feedback colors
                appliedColors[wordData.ID] = validWordColor;
                for (int i = 0; i < wordData.Coordinates.Count && i < testFeedbacks.Length; i++)
                {
                    Vector2Int coord = wordData.Coordinates[i];
                    if (coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
                    {
                        CellController cell = gridCells[coord.x, coord.y];
                        if (cell != null)
                        {
                            Color feedbackColor = GetFeedbackColor(testFeedbacks[i]);
                            cell.SetHighlightState(true, feedbackColor);
                            Debug.Log($"🎯 TEST: Letter '{wordData.Word[i]}' at ({coord.x},{coord.y}) = {testFeedbacks[i]} → Color: {feedbackColor}");
                        }
                    }
                }
            }
            
            if (gameManager != null)
            {
                gameManager.UpdatePotentialWordsDisplay(potentialWords, appliedColors);
            }
            Debug.Log($"🎯 TEST MODE: Applied test feedback to {potentialWords.Count} words");
            return;
        }
        
        // Normal Wordle feedback processing
        foreach (var wordData in potentialWords)
        {
            if (wordData.Coordinates == null || wordData.Coordinates.Count == 0) continue;
            
            Debug.Log($"🎯 Wordle Feedback: Analyzing word '{wordData.Word}' with {wordData.Coordinates.Count} letters");
            
            // Get validation result with feedback for this word
            WordValidationResult validationResult = wordValidator.ValidateWordWithFeedback(wordData.Word, wordData.Coordinates);
            
            Debug.Log($"🎯 Wordle Feedback: Word '{wordData.Word}' - Valid: {validationResult.IsValid}, IsTarget: {validationResult.IsTargetWord}, HasFeedback: {validationResult.LetterFeedbacks != null}");
            
            if (validationResult.IsValid && validationResult.LetterFeedbacks != null)
            {
                appliedColors[wordData.ID] = validWordColor; // Default color for the word
                
                // Apply feedback colors to individual letters
                for (int i = 0; i < wordData.Coordinates.Count && i < validationResult.LetterFeedbacks.Length; i++)
                {
                    Vector2Int coord = wordData.Coordinates[i];
                    if (coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
                    {
                        CellController cell = gridCells[coord.x, coord.y];
                        if (cell != null)
                        {
                            Color feedbackColor = GetFeedbackColor(validationResult.LetterFeedbacks[i]);
                            cell.SetHighlightState(true, feedbackColor);
                            
                            Debug.Log($"🎯 Wordle Feedback: Letter '{wordData.Word[i]}' at ({coord.x},{coord.y}) = {validationResult.LetterFeedbacks[i]} → Color: {feedbackColor}");
                        }
                    }
                }
            }
            else if (validationResult.IsValid)
            {
                // Word is valid but no Wordle feedback - use standard highlighting
                appliedColors[wordData.ID] = validWordColor;
                foreach (var coord in wordData.Coordinates)
                {
                    if (coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
                    {
                        CellController cell = gridCells[coord.x, coord.y];
                        if (cell != null)
                        {
                            cell.SetHighlightState(true, validWordColor);
                        }
                    }
                }
                Debug.Log($"🎯 Wordle Feedback: Word '{wordData.Word}' highlighted with standard color (no feedback)");
            }
        }
        
        if (gameManager != null)
        {
            gameManager.UpdatePotentialWordsDisplay(potentialWords, appliedColors);
        }
        
        Debug.Log($"🎯 Applied Wordle feedback highlighting to {potentialWords.Count} words");
    }
    
    /// <summary>
    /// Get the appropriate color for a letter feedback type
    /// </summary>
    private Color GetFeedbackColor(LetterFeedback feedback)
    {
        Color resultColor;
        switch (feedback)
        {
            case LetterFeedback.Correct:
                resultColor = correctLetterColor;
                break;
            case LetterFeedback.Present:
                resultColor = presentLetterColor;
                break;
            case LetterFeedback.None:
            default:
                resultColor = absentLetterColor;
                break;
        }
        
        Debug.Log($"🎨 GetFeedbackColor: {feedback} → {resultColor} (Present should be {presentLetterColor})");
        return resultColor;
    }

    public Dictionary<System.Guid, Color> HighlightPotentialWordCells(List<FoundWordData> potentialWords)
    {
        ClearAllCellHighlights(false);
        Dictionary<System.Guid, Color> appliedColors = new Dictionary<System.Guid, Color>();
        
        if (potentialWords == null) return appliedColors;

        // Step 1: Find all intersecting positions
        Dictionary<Vector2Int, int> positionWordCount = new Dictionary<Vector2Int, int>();
        
        foreach (var wordData in potentialWords)
        {
            if (wordData.Coordinates == null || wordData.Coordinates.Count == 0) continue;
            
            foreach (var coord in wordData.Coordinates)
            {
                if (coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
                {
                    if (positionWordCount.ContainsKey(coord))
                        positionWordCount[coord]++;
                    else
                        positionWordCount[coord] = 1;
                }
            }
        }

        // Step 2: Apply colors based on intersection status
        foreach (var wordData in potentialWords)
        {
            if (wordData.Coordinates == null || wordData.Coordinates.Count == 0) continue;

            // All words use the same valid word color
            appliedColors[wordData.ID] = validWordColor;

            foreach (var coord in wordData.Coordinates)
            {
                if (coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
                {
                    CellController cell = gridCells[coord.x, coord.y];
                    if (cell != null)
                    {
                        // Use intersection color if this position is shared by multiple words
                        Color cellColor = (positionWordCount[coord] > 1) ? intersectionLetterColor : validWordColor;
                        cell.SetHighlightState(true, cellColor);
                    }
                }
            }
        }
        
        return appliedColors;
    }

    public void ClearHighlightForSpecificWord(FoundWordData wordDataToClear)
    {
        if (wordDataToClear.Coordinates == null) return;
        foreach (var coord in wordDataToClear.Coordinates)
        {
            if (coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
            {
                CellController cell = gridCells[coord.x, coord.y];
                if (cell != null)
                {
                    cell.SetHighlightState(false, cell.GetDefaultColor());
                }
            }
        }
    }

    public void ClearAllCellHighlights(bool fullReset = true)
    {
        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                if (gridCells[r, c] != null)
                {
                    if (fullReset) gridCells[r, c].StoreDefaultColor();
                    gridCells[r, c].SetHighlightState(false, gridCells[r, c].GetDefaultColor());
                }
            }
        }
    }

    // Modified to accept a count of moves to reduce
    public void ApplyPendingMoveReduction(int row, int col, int count = 1)
    {
        if (gameManager != null && gameManager.CurrentGameDisplayMode == GameManager.DisplayMode.Moves)
        {
            for (int i = 0; i < count; i++)
            {
                gameManager.DecrementMoves();
            }
        }
    }

    public CellController GetCellController(Vector2Int coord)
    {
        if (coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
        {
            return gridCells[coord.x, coord.y];
        }
        return null;
    }

    /// <summary>
    /// Find a cell by its unique ID and return its current position
    /// </summary>
    public Vector2Int FindCellByUniqueID(int uniqueID)
    {
        if (uniqueID == -1) return new Vector2Int(-1, -1);
        
        // Search through all main grid cells
        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                if (gridCells[r, c] != null && gridCells[r, c].uniqueID == uniqueID)
                {
                    return new Vector2Int(r, c);
                }
            }
        }
        
        return new Vector2Int(-1, -1); // Not found
    }

    public char[] GetRowData(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= gridSize)
        {
            Debug.LogError($"WGM.GetRowData: Invalid rowIndex {rowIndex}");
            return null;
        }
        char[] row = new char[gridSize];
        for (int c = 0; c < gridSize; c++)
        {
            row[c] = GetLetterFromCellData(gridData[rowIndex, c]);
        }
        return row;
    }

    public char[] GetColumnData(int colIndex)
    {
        if (colIndex < 0 || colIndex >= gridSize)
        {
            Debug.LogError($"WGM.GetColumnData: Invalid colIndex {colIndex}");
            return null;
        }
        char[] col = new char[gridSize];
        for (int r = 0; r < gridSize; r++)
        {
            col[r] = GetLetterFromCellData(gridData[r, colIndex]);
        }
        return col;
    }

    /// <summary>
    /// Get row data as CellData array (new interface)
    /// </summary>
    public CellData[] GetRowCellData(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= gridSize)
        {
            Debug.LogError($"WGM.GetRowCellData: Invalid rowIndex {rowIndex}");
            return null;
        }
        CellData[] row = new CellData[gridSize];
        for (int c = 0; c < gridSize; c++)
        {
            row[c] = gridData[rowIndex, c];
        }
        return row;
    }

    /// <summary>
    /// Get column data as CellData array (new interface)
    /// </summary>
    public CellData[] GetColumnCellData(int colIndex)
    {
        if (colIndex < 0 || colIndex >= gridSize)
        {
            Debug.LogError($"WGM.GetColumnCellData: Invalid colIndex {colIndex}");
            return null;
        }
        CellData[] col = new CellData[gridSize];
        for (int r = 0; r < gridSize; r++)
        {
            col[r] = gridData[r, colIndex];
        }
        return col;
    }

    // Initialize extended grid with wraparound cells
    public void InitializeWraparoundGrid()
    {
        if (wraparoundInitialized) return;
        
        _horizontalWrapCells = new CellController[gridSize, WRAP_COUNT * 2];
        _verticalWrapCells = new CellController[gridSize, WRAP_COUNT * 2];

        // Ensure _visualColOffsets and _visualRowOffsets are initialized
        // This is a bit redundant if Awake always runs first and gridSize doesn't change.
        // If gridSize could change, these maps would need to be dynamic properties or re-calculated.
        if (_visualColOffsets == null || _visualColOffsets.Length != WRAP_COUNT * 2 || _visualColOffsets[WRAP_COUNT] != gridSize) // Basic check
        {
            _visualColOffsets = new int[WRAP_COUNT * 2];
            _visualRowOffsets = new int[WRAP_COUNT * 2]; // Assuming _gridSize is accessible here
            for (int i = 0; i < WRAP_COUNT; i++)
            {
                _visualColOffsets[i] = -WRAP_COUNT + i; 
                _visualColOffsets[i + WRAP_COUNT] = gridSize + i; 
                _visualRowOffsets[i] = -WRAP_COUNT + i;
                _visualRowOffsets[i + WRAP_COUNT] = gridSize + i;
            }
        }


        CellController SetupWraparoundCell(string cellNamePrefix, int primaryIndex, int wrapIndexInArray, bool isHorizontalCell, char letter)
        {
            GameObject cellGO = Instantiate(letterCellPrefab, gridParent);
            CellController cell = cellGO.GetComponent<CellController>();
            
            if (cell == null)
            {
                Debug.LogError($"WGM: Cell prefab '{letterCellPrefab.name}' is missing CellController for wraparound.", this);
                Destroy(cellGO);
                return null;
            }
            
            int visualRow, visualCol;
            string name;

            if (isHorizontalCell)
            {
                visualRow = primaryIndex; // row index
                visualCol = _visualColOffsets[wrapIndexInArray];
                name = $"{cellNamePrefix}_R{visualRow}_VC{visualCol}";
            }
            else // Vertical
            {
                visualCol = primaryIndex; // col index
                visualRow = _visualRowOffsets[wrapIndexInArray];
                name = $"{cellNamePrefix}_C{visualCol}_VR{visualRow}";
            }
            
            cell.gameObject.name = name;
            
            // Assign unique ID for tracking
            cell.SetUniqueID(nextUniqueID++);
            
            RectTransform cellRT = cellGO.GetComponent<RectTransform>();
            if (cellRT != null) cellRT.sizeDelta = new Vector2(cellSize, cellSize);
            
            cell.transform.localPosition = GetBaseCellPosition(visualRow, visualCol);
            cell.SetLetter(letter);
            
            Image bgImage = cell.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.color = ((visualRow + visualCol) % 2 + 2) % 2 == 0 ? cellColorPrimary : cellColorAlternate;
            }
            cell.StoreDefaultColor();
            return cell;
        }

        for (int r = 0; r < gridSize; r++)
        {
            for (int i = 0; i < WRAP_COUNT * 2; i++) // 0..WRAP_COUNT-1 for left/top, WRAP_COUNT..WRAP_COUNT*2-1 for right/bottom
            {
                int visualCol = _visualColOffsets[i];
                // Determine the data column this visual column should mirror
                int dataCol = (visualCol % gridSize + gridSize) % gridSize;
                _horizontalWrapCells[r, i] = SetupWraparoundCell("HWrap", r, i, true, GetLetterFromCellData(gridData[r, dataCol]));
            }
        }

        for (int c = 0; c < gridSize; c++)
        {
            for (int i = 0; i < WRAP_COUNT * 2; i++)
            {
                int visualRow = _visualRowOffsets[i];
                // Determine the data row this visual row should mirror
                int dataRow = (visualRow % gridSize + gridSize) % gridSize;
                _verticalWrapCells[c, i] = SetupWraparoundCell("VWrap", c, i, false, GetLetterFromCellData(gridData[dataRow, c]));
            }
        }
        
        wraparoundInitialized = true;
    }
    
    // Get position for extended grid cell - THIS METHOD IS NO LONGER USED with the new system.
    // private Vector2 GetExtendedCellBasePosition(int r, int c) ...
    
    // Core method for the continuous scrolling effect - cells move, letters are fixed during drag
    public void UpdateScrollVisualsWithWrap(int lineIndex, bool isHorizontal, float totalOffset)
    {
        if (!wraparoundInitialized) 
        {
            InitializeWraparoundGrid(); 
            if (!wraparoundInitialized) 
            {
                Debug.LogError("WGM: Failed to initialize wraparound grid in UpdateScrollVisualsWithWrap.");
                return; 
            }
        }
        
        // float cellDimensionWithSpacing = cellSize + spacing; // Not needed here anymore for letter calculation
        // int scrollAmountInCells = Mathf.RoundToInt(totalOffset / cellDimensionWithSpacing); // Not needed here

        if (isHorizontal) 
        {
            int rowIndex = lineIndex;
            // int dataIndexAtVisualColumnZero = (scrollAmountInCells % gridSize + gridSize) % gridSize; // REMOVED

            // Update main grid cells positions for the current row
            for (int c = 0; c < gridSize; c++)
            {
                CellController cell = gridCells[rowIndex, c];
                if (cell == null) continue;

                Vector2 basePos = GetBaseCellPosition(rowIndex, c);
                cell.transform.localPosition = new Vector2(basePos.x + totalOffset, basePos.y);
                
                // int currentDataIndex = (dataIndexAtVisualColumnZero + c % gridSize + gridSize) % gridSize; // REMOVED
                // cell.SetLetter(gridData[rowIndex, currentDataIndex]); // REMOVED - Letters are set on snap
            }

            // Update horizontal wraparound cells positions for the current row
            for (int i = 0; i < WRAP_COUNT * 2; i++)
            {
                CellController wrapCell = _horizontalWrapCells[rowIndex, i];
                if (wrapCell == null) continue;

                int visualCol = _visualColOffsets[i];
                Vector2 basePos = GetBaseCellPosition(rowIndex, visualCol); 
                wrapCell.transform.localPosition = new Vector2(basePos.x + totalOffset, basePos.y);

                // int currentDataIndex = (dataIndexAtVisualColumnZero + visualCol % gridSize + gridSize) % gridSize; // REMOVED
                // wrapCell.SetLetter(gridData[rowIndex, currentDataIndex]); // REMOVED - Letters are set on snap
            }
        }
        else // Vertical scrolling
        {
            int colIndex = lineIndex;
            // int dataIndexAtVisualRowZero = (scrollAmountInCells % gridSize + gridSize) % gridSize; // REMOVED

            // Update main grid cells positions for the current column
            for (int r = 0; r < gridSize; r++)
            {
                CellController cell = gridCells[r, colIndex];
                if (cell == null) continue;

                Vector2 basePos = GetBaseCellPosition(r, colIndex);
                cell.transform.localPosition = new Vector2(basePos.x, basePos.y + totalOffset); 
                
                // int currentDataIndex = (dataIndexAtVisualRowZero + r % gridSize + gridSize) % gridSize; // REMOVED
                // cell.SetLetter(gridData[currentDataIndex, colIndex]); // REMOVED - Letters are set on snap
            }

            // Update vertical wraparound cells positions for the current column
            for (int i = 0; i < WRAP_COUNT * 2; i++)
            {
                CellController wrapCell = _verticalWrapCells[colIndex, i];
                if (wrapCell == null) continue;

                int visualRow = _visualRowOffsets[i];
                Vector2 basePos = GetBaseCellPosition(visualRow, colIndex); 
                wrapCell.transform.localPosition = new Vector2(basePos.x, basePos.y + totalOffset);
            }
        }
    }   
    
    /// <summary>
    /// Gets the color used for valid word highlighting
    /// </summary>
    public Color GetValidWordColor() => validWordColor;
    
    /// <summary>
    /// Gets the color used for intersection letter highlighting
    /// </summary>
    public Color GetIntersectionLetterColor() => intersectionLetterColor;
    
    /// <summary>
    /// Gets the color used for correct letters in Wordle feedback
    /// </summary>
    public Color GetCorrectLetterColor() => correctLetterColor;
    
    /// <summary>
    /// Gets the color used for present letters in Wordle feedback
    /// </summary>
    public Color GetPresentLetterColor() => presentLetterColor;
    
    /// <summary>
    /// Gets the color used for absent letters in Wordle feedback
    /// </summary>
    public Color GetAbsentLetterColor() => absentLetterColor;
    
    /// <summary>
    /// Manual test method to verify the Wordle feedback system
    /// Call this from the Unity console: GameObject.Find("WordGridManager").GetComponent<WordGridManager>().TestWordleFeedback();
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void TestWordleFeedback()
    {
        Debug.Log($"🧪 MANUAL TEST: Testing Wordle feedback system...");
        
        // Clear any existing highlights
        ClearAllCellHighlights(false);
        
        // Test the middle row (row 1 if grid is 3x3 or larger)
        int testRow = gridSize >= 3 ? 1 : 0;
        
        Debug.Log($"🧪 Testing row {testRow} with manual feedback patterns...");
        
        // Apply test pattern: Yellow, Green, Gray, Yellow, Green...
        for (int c = 0; c < gridSize && c < 5; c++)
        {
            if (gridCells[testRow, c] != null)
            {
                LetterFeedback testFeedback = (LetterFeedback)(c % 3); // Cycles through None, Present, Correct
                Color feedbackColor = GetFeedbackColor(testFeedback);
                
                gridCells[testRow, c].SetHighlightState(true, feedbackColor);
                
                char cellLetter = GetLetterFromCellData(gridData[testRow, c]);
                Debug.Log($"🧪 Cell ({testRow},{c}): Letter '{cellLetter}' = {testFeedback} → {feedbackColor}");
            }
        }
        
        Debug.Log($"🧪 MANUAL TEST: Applied feedback colors to row {testRow}. Check visually!");
    }

    /// <summary>
    /// Public method to manually refresh cell sizes - useful when called from other scripts
    /// </summary>
    public void ForceRefreshCellSizes()
    {
        if (enableAdaptiveSizing)
        {
            RefreshCellSizes();
        }
    }

    /// <summary>
    /// Get current calculated cell size (for debugging or external reference)
    /// </summary>
    public float GetCurrentCellSize() => cellSize;

    /// <summary>
    /// Enable or disable adaptive sizing at runtime
    /// </summary>
    public void SetAdaptiveSizing(bool enabled)
    {
        enableAdaptiveSizing = enabled;
        if (enabled)
        {
            RefreshCellSizes();
        }
    }

    /// <summary>
    /// Debug method to test cell size calculation and show detailed info
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugCellSizeCalculation()
    {
        Debug.Log("=== DEBUGGING CELL SIZE CALCULATION ===");
        Debug.Log($"🔧 Current Settings:");
        Debug.Log($"  - enableAdaptiveSizing: {enableAdaptiveSizing}");
        Debug.Log($"  - _baseCellSize: {_baseCellSize}");
        Debug.Log($"  - referencePanel: {(referencePanel != null ? referencePanel.name : "null")}");
        Debug.Log($"  - maxGridScreenPercentage: {maxGridScreenPercentage}");
        Debug.Log($"  - minCellSize: {minCellSize}, maxCellSize: {maxCellSize}");
        Debug.Log($"  - gridPadding: {gridPadding}");
        Debug.Log($"  - spacing: {spacing}");
        Debug.Log($"  - gridSize: {gridSize}");
        
        if (referencePanel != null)
        {
            Vector2 panelSize = referencePanel.rect.size;
            Debug.Log($"🖥️ Reference Panel Info: {panelSize} (Panel: {referencePanel.name})");
        }
        else
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                Vector2 canvasSize = canvasRect.rect.size;
                Debug.Log($"🖥️ Canvas Info: {canvasSize} (Canvas: {canvas.name})");
            }
            else
            {
                Debug.Log($"🖥️ No reference panel or Canvas found! Screen size: {Screen.width}×{Screen.height}");
            }
        }
        
        Debug.Log($"📏 Current cellSize property: {cellSize}");
        Debug.Log($"📏 Current _calculatedCellSize: {_calculatedCellSize}");
        
        // Force recalculation
        CalculateAdaptiveCellSize();
    }
}