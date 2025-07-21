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
    [SerializeField] private int _gridSize = 7;
    public int gridSize => _gridSize;
    [SerializeField] private float _cellSize = 100f;
    public float cellSize => _cellSize;
    [SerializeField] private float _spacing = 10f;
    public float spacing => _spacing;   
    [SerializeField] private GameObject letterCellPrefab;
    [SerializeField] private Transform gridParent;

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

        gridCells = new CellController[gridSize, gridSize];
        gridData = new CellData[gridSize, gridSize]; // Changed from char[,] to CellData[,]
        // Note: PopulateWeightedLettersList() removed - now handled by CellTypeManager
        CalculateGridCenterOffset();

        // Initialize visual offset maps based on WRAP_COUNT and gridSize
        // This needs gridSize, so it's tricky if gridSize can change after Awake.
        // For now, assume gridSize is fixed after Awake.
        // If gridSize can change, these might need to be properties or recomputed.
        _visualColOffsets = new int[WRAP_COUNT * 2];
        _visualRowOffsets = new int[WRAP_COUNT * 2];
        for (int i = 0; i < WRAP_COUNT; i++)
        {
            _visualColOffsets[i] = -WRAP_COUNT + i; // e.g., -2, -1 for WRAP_COUNT=2
            _visualColOffsets[i + WRAP_COUNT] = _gridSize + i; // e.g., gridSize, gridSize+1 for WRAP_COUNT=2

            _visualRowOffsets[i] = -WRAP_COUNT + i;
            _visualRowOffsets[i + WRAP_COUNT] = _gridSize + i;
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

    public void InitializeGrid()
    {
        isAnimating = true; // Temporarily set for initial fade-in
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
        }, false);
    }

    void PopulateGridData()
    {
        int blankCount = 0;
        int totalCells = gridSize * gridSize;
        
        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                Vector2Int position = new Vector2Int(c, r);
                
                if (cellTypeManager != null)
                {
                    // Use CellTypeManager to generate cells
                    gridData[r, c] = cellTypeManager.GenerateCell(blankCount, r * gridSize + c, position, gridSize);
                    
                    if (gridData[r, c].IsBlank)
                    {
                        blankCount++;
                    }
                }
                else
                {
                    // Fallback: Generate standard letter cells using CellTypeManager for letters
                    char randomLetter = GetRandomLetter();
                    gridData[r, c] = CellData.CreateLetterCell(randomLetter);
                }
            }
        }
        
        Debug.Log($"📋 Grid populated with {blankCount} blank cells out of {totalCells} total cells");
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
        List<FoundWordData> potentialWords = wordValidator.FindAllPotentialWords();
        Dictionary<System.Guid, Color> appliedColors = HighlightPotentialWordCells(potentialWords);
        if (gameManager != null)
        {
            gameManager.UpdatePotentialWordsDisplay(potentialWords, appliedColors);
        }
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
                _visualColOffsets[i + WRAP_COUNT] = _gridSize + i; 
                _visualRowOffsets[i] = -WRAP_COUNT + i;
                _visualRowOffsets[i + WRAP_COUNT] = _gridSize + i;
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
}