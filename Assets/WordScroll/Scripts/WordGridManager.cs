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
    [SerializeField] private float cellFadeInDuration = 0.3f;
    public float CellFadeInDuration => cellFadeInDuration;
    [SerializeField] private Color cellColorPrimary = Color.white;
    [SerializeField] private Color cellColorAlternate = Color.grey;

    [Header("Highlighting")]
    [Tooltip("Colors to cycle through for highlighting different potential words. Define at least one.")]
    [SerializeField]
    private Color[] wordHighlightPalette = { Color.yellow, Color.cyan, Color.magenta, Color.green, Color.blue, Color.red };

    [Header("References")]
    [SerializeField] private WordValidator wordValidator;
    [SerializeField] private GameManager gameManager;

    private CellController[,] gridCells;
    public char[,] gridData { get; private set; }

    public bool isAnimating { get; private set; } = false;

    private List<char> WeightedLetters = new List<char>();
    private Vector2 gridCenterOffset;

    // Define the number of extra cells for wrap-around effect
    private const int WRAP_PADDING = 3;
    
    // Expanded cell pool for wraparound (includes hidden cells outside visible grid)
    private CellController[,] extendedGridCells;
    
    // Track if cells are initialized for wraparound
    private bool wraparoundInitialized = false;

    void Awake()
    {
        if (letterCellPrefab == null) { Debug.LogError("WGM: Letter Cell Prefab not assigned!", this); enabled = false; return; }
        if (gridParent == null) { Debug.LogError("WGM: Grid Parent not assigned!", this); enabled = false; return; }
        if (wordValidator == null) wordValidator = FindFirstObjectByType<WordValidator>();
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (wordValidator == null) Debug.LogWarning("WGM: WordValidator not found!", this);
        if (gameManager == null) Debug.LogWarning("WGM: GameManager not found!", this);

        gridCells = new CellController[gridSize, gridSize];
        gridData = new char[gridSize, gridSize];
        PopulateWeightedLettersList();
        CalculateGridCenterOffset();
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
                cell.SetLetter(gridData[r, c]);
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

    void PopulateWeightedLettersList()
    {
        WeightedLetters.Clear();
        AddLetters("E", 12); AddLetters("A", 9); AddLetters("I", 9); AddLetters("O", 8);
        AddLetters("N", 6); AddLetters("R", 6); AddLetters("T", 6); AddLetters("L", 4);
        AddLetters("S", 4); AddLetters("U", 4); AddLetters("D", 4); AddLetters("G", 3);
        AddLetters("B", 2); AddLetters("C", 2); AddLetters("M", 2); AddLetters("P", 2);
        AddLetters("F", 2); AddLetters("H", 2); AddLetters("V", 2); AddLetters("W", 2); AddLetters("Y", 2);
        AddLetters("K", 1); AddLetters("J", 1); AddLetters("X", 1); AddLetters("Q", 1); AddLetters("Z", 1);
    }

    void AddLetters(string letters, int count)
    {
        foreach (char letter in letters)
        {
            for (int i = 0; i < count; i++) { WeightedLetters.Add(letter); }
        }
    }

    void PopulateGridData()
    {
        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                gridData[r, c] = GetRandomLetter();
            }
        }
    }

    char GetRandomLetter()
    {
        if (WeightedLetters.Count == 0) return 'A';
        return WeightedLetters[UnityEngine.Random.Range(0, WeightedLetters.Count)];
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

        // Ensure letters are set according to current gridData before snapping position
        for (int extendedCol = 0; extendedCol < gridSize + WRAP_PADDING * 2; extendedCol++)
        {
            CellController cell = extendedGridCells[rowIndex + WRAP_PADDING, extendedCol];
            if (cell == null) continue;
            int virtualCol = extendedCol - WRAP_PADDING;
            int dataColIndex = (virtualCol % gridSize + gridSize) % gridSize;
            cell.SetLetter(gridData[rowIndex, dataColIndex]);
        }
        UpdateScrollVisualsWithWrap(rowIndex, true, 0f); // Then snap positions
    }

    public void SnapColumnToGrid(int colIndex)
    {
        if (colIndex < 0 || colIndex >= gridSize) return;
        if (!wraparoundInitialized) InitializeWraparoundGrid();

        for (int extendedRow = 0; extendedRow < gridSize + WRAP_PADDING * 2; extendedRow++)
        {
            CellController cell = extendedGridCells[extendedRow, colIndex + WRAP_PADDING];
            if (cell == null) continue;
            int virtualRow = extendedRow - WRAP_PADDING;
            int dataRowIndex = (virtualRow % gridSize + gridSize) % gridSize;
            cell.SetLetter(gridData[dataRowIndex, colIndex]);
        }
        UpdateScrollVisualsWithWrap(colIndex, false, 0f);
    }

    // Shifts data AND refreshes letters on ALL relevant extended cells
    public void ShiftRowDataAndRefresh(int rowIndex, int cellsToShift)
    {
        if (rowIndex < 0 || rowIndex >= gridSize || cellsToShift == 0) return;
        if (!wraparoundInitialized) InitializeWraparoundGrid(); // Ensure extended grid is ready

        int direction = Math.Sign(cellsToShift);
        for (int i = 0; i < Math.Abs(cellsToShift); i++)
        {
            ShiftRowDataInternal(rowIndex, direction); // This modifies gridData
        }

        // Now, update the letters on all extended cells for this row based on the NEW gridData
        for (int extendedCol = 0; extendedCol < gridSize + WRAP_PADDING * 2; extendedCol++)
        {
            CellController cell = extendedGridCells[rowIndex + WRAP_PADDING, extendedCol];
            if (cell == null) continue;

            // 'virtualCol' is the cell's 0-indexed position in the conceptual infinite strip of cells
            // (e.g., -3, -2, -1 for left padding, 0 to gridSize-1 for main grid, gridSize to gridSize+2 for right padding)
            int virtualCol = extendedCol - WRAP_PADDING; 
            
            // Map this virtualCol to an index in the gridData array.
            // This ensures that the cell at a specific visual position in the extended strip
            // always shows the letter from the corresponding wrapped index of the current gridData.
            int dataColIndex = (virtualCol % gridSize + gridSize) % gridSize;
            
            cell.SetLetter(gridData[rowIndex, dataColIndex]);
        }
        
        // After letters are authoritatively set, snap visual positions to 0 offset.
        // This call to UpdateScrollVisualsWithWrap will now only move cells (as SetLetter is removed from it).
        UpdateScrollVisualsWithWrap(rowIndex, true, 0f); 
    }

    public void ShiftColumnDataAndRefresh(int colIndex, int cellsToShift)
    {
        if (colIndex < 0 || colIndex >= gridSize || cellsToShift == 0) return;
        if (!wraparoundInitialized) InitializeWraparoundGrid();

        int direction = Math.Sign(cellsToShift);
        for (int i = 0; i < Math.Abs(cellsToShift); i++)
        {
            ShiftColumnDataInternal(colIndex, direction); // Modifies gridData
        }

        // Now, update the letters on all extended cells for this column based on the NEW gridData
        for (int extendedRow = 0; extendedRow < gridSize + WRAP_PADDING * 2; extendedRow++)
        {
            CellController cell = extendedGridCells[extendedRow, colIndex + WRAP_PADDING];
            if (cell == null) continue;

            int virtualRow = extendedRow - WRAP_PADDING;
            int dataRowIndex = (virtualRow % gridSize + gridSize) % gridSize;
            cell.SetLetter(gridData[dataRowIndex, colIndex]);
        }
        
        UpdateScrollVisualsWithWrap(colIndex, false, 0f);
    }

    // Internal method for single step data shift
    private void ShiftRowDataInternal(int rowIndex, int direction)
    {
        if (direction == 0) return;
        char[] tempRow = new char[gridSize];
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
        char[] tempCol = new char[gridSize];
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
                gridCells[rowIndex, c].SetLetter(gridData[rowIndex, c]);
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
                gridCells[r, colIndex].SetLetter(gridData[r, colIndex]);
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
        isAnimating = true; // Potentially set this at the start of the sequence if DOTween is used.

        Sequence replacementSequence = DOTween.Sequence(); // Good for managing sequential fades

        foreach (var coord in coordinates)
        {
            if (coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
            {
                CellController cell = gridCells[coord.x, coord.y]; // Assuming gridCells holds the primary, visible cells
                if (cell != null)
                {
                    // 1. Reset visual state BEFORE setting new letter and fading in
                    cell.SetHighlightState(false, cell.GetDefaultColor()); // Resets color and scale

                    gridData[coord.x, coord.y] = GetRandomLetter(); // Get new letter for the data grid

                    if (fadeIn)
                    {
                        cell.SetAlpha(0f); // Ensure it's fully transparent before fade
                        replacementSequence.AppendCallback(() => {
                            cell.SetLetter(gridData[coord.x, coord.y]); // Set new letter
                            // Re-apply the correct primary/alternate background color based on its position
                            Image bgImage = cell.GetComponent<Image>();
                            if (bgImage != null)
                            {
                                bgImage.color = (coord.x + coord.y) % 2 == 0 ? cellColorPrimary : cellColorAlternate;
                                cell.StoreDefaultColor(); // Store this new base color as its default
                            }
                        });
                        replacementSequence.Append(cell.GetComponent<CanvasGroup>().DOFade(1f, cellFadeInDuration * 0.75f)); // Slightly faster fade for replacement
                    }
                    else
                    {
                        cell.SetLetter(gridData[coord.x, coord.y]);
                        // Re-apply the correct primary/alternate background color
                        Image bgImage = cell.GetComponent<Image>();
                        if (bgImage != null)
                        {
                            bgImage.color = (coord.x + coord.y) % 2 == 0 ? cellColorPrimary : cellColorAlternate;
                            cell.StoreDefaultColor();
                        }
                        cell.SetAlpha(1f); // Ensure it's visible
                    }
                }
            }
        }

        replacementSequence.OnComplete(() => {
            ResetAnimationFlag("ReplaceLettersAt");
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
        int colorIndex = 0;

        if (potentialWords == null) return appliedColors;

        foreach (var wordData in potentialWords)
        {
            if (wordData.Coordinates == null || wordData.Coordinates.Count == 0) continue;

            Color highlightColor = (wordHighlightPalette.Length > 0) ? wordHighlightPalette[colorIndex % wordHighlightPalette.Length] : Color.yellow;
            appliedColors[wordData.ID] = highlightColor;

            foreach (var coord in wordData.Coordinates)
            {
                if (coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
                {
                    CellController cell = gridCells[coord.x, coord.y];
                    if (cell != null)
                    {
                        cell.SetHighlightState(true, highlightColor);
                    }
                }
            }
            colorIndex++;
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
            row[c] = gridData[rowIndex, c];
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
            col[r] = gridData[r, colIndex];
        }
        return col;
    }

    // Initialize extended grid with wraparound cells
    public void InitializeWraparoundGrid()
    {
        if (wraparoundInitialized) return;
        
        // Create expanded grid
        extendedGridCells = new CellController[gridSize + WRAP_PADDING*2, gridSize + WRAP_PADDING*2];
        
        // Copy existing cells to center of extended grid
        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                extendedGridCells[r + WRAP_PADDING, c + WRAP_PADDING] = gridCells[r, c];
            }
        }
        
        // Create wraparound cells (left/right edges, top/bottom edges, and corners)
        for (int r = 0; r < gridSize + WRAP_PADDING*2; r++)
        {
            for (int c = 0; c < gridSize + WRAP_PADDING*2; c++)
            {
                // Skip the main grid area
                if (r >= WRAP_PADDING && r < WRAP_PADDING + gridSize && 
                    c >= WRAP_PADDING && c < WRAP_PADDING + gridSize) continue;
                
                // Map to the corresponding cell in the main grid
                int mappedRow = ((r % gridSize) + gridSize) % gridSize;
                int mappedCol = ((c % gridSize) + gridSize) % gridSize;
                
                // Create a new cell for the wraparound position
                GameObject cellGO = Instantiate(letterCellPrefab, gridParent);
                CellController cell = cellGO.GetComponent<CellController>();
                
                if (cell == null)
                {
                    Debug.LogError($"WGM: Cell prefab is missing CellController script.", this);
                    Destroy(cellGO);
                    continue;
                }
                
                extendedGridCells[r, c] = cell;
                
                // Set properties
                cell.gameObject.name = $"WrapCell_{r}_{c}";
                
                // Size the cell
                RectTransform cellRT = cellGO.GetComponent<RectTransform>();
                if (cellRT != null)
                {
                    cellRT.sizeDelta = new Vector2(cellSize, cellSize);
                }
                
                // Position outside the visible area (will be moved during scrolling)
                cell.transform.localPosition = GetExtendedCellBasePosition(r, c);
                
                // Set letter from the mapped position in gridData - this ensures wrap cells match the data pattern
                cell.SetLetter(gridData[mappedRow, mappedCol]);
                
                // Set the appearance and make it initially invisible
                Image bgImage = cell.GetComponent<Image>();
                if (bgImage != null)
                {
                    bgImage.color = (mappedRow + mappedCol) % 2 == 0 ? cellColorPrimary : cellColorAlternate;
                }
                cell.StoreDefaultColor();
                cell.SetAlpha(0f); // Hide initially
            }
        }
        
        wraparoundInitialized = true;
    }
    
    // Get position for extended grid cell
    private Vector2 GetExtendedCellBasePosition(int r, int c)
    {
        // Convert extended grid indices to relative positions
        int relativeRow = r - WRAP_PADDING;
        int relativeCol = c - WRAP_PADDING;
        
        float xPos = relativeCol * (cellSize + spacing) - gridCenterOffset.x;
        float yPos = -(relativeRow * (cellSize + spacing) - gridCenterOffset.y);
        return new Vector2(xPos, yPos);
    }
    
    // Core method for the continuous scrolling effect - cells move, letters are fixed during drag
    public void UpdateScrollVisualsWithWrap(int lineIndex, bool isHorizontal, float totalOffset)
    {
        if (!wraparoundInitialized) InitializeWraparoundGrid();
        
        float cellDimensionWithSpacing = cellSize + spacing; 
        
        if (isHorizontal) // Row scrolling
        {
            int rowIndex = lineIndex;
            
            for (int extendedCol = 0; extendedCol < gridSize + WRAP_PADDING*2; extendedCol++)
            {
                CellController cell = extendedGridCells[rowIndex + WRAP_PADDING, extendedCol];
                if (cell == null) continue;
                
                // Position the cell with the full totalOffset - directly follow finger
                Vector2 basePos = GetExtendedCellBasePosition(rowIndex + WRAP_PADDING, extendedCol);
                cell.transform.localPosition = new Vector3(basePos.x + totalOffset, basePos.y, 0);
                
                // Visibility logic
                int virtualCol = extendedCol - WRAP_PADDING;
                float scrollOffsetInCells = totalOffset / cellDimensionWithSpacing;
                bool isVisible = (virtualCol + scrollOffsetInCells >= -WRAP_PADDING) && 
                                 (virtualCol + scrollOffsetInCells < gridSize + WRAP_PADDING);

                if (isVisible && cell.GetComponent<CanvasGroup>().alpha < 1f)
                {
                    cell.SetAlpha(1f);
                }
                else if (!isVisible && cell.GetComponent<CanvasGroup>().alpha > 0f)
                {
                    cell.SetAlpha(0f);
                }
                // DO NOT SET LETTER HERE - Letters are fixed to their specific CellController instance during drag.
            }
        }
        else // Column scrolling
        {
            int colIndex = lineIndex;
            
            for (int extendedRow = 0; extendedRow < gridSize + WRAP_PADDING*2; extendedRow++)
            {
                CellController cell = extendedGridCells[extendedRow, colIndex + WRAP_PADDING];
                if (cell == null) continue;
                
                RectTransform cellRect = cell.GetComponent<RectTransform>();
                if (cellRect != null) {
                    cellRect.DOKill(true);
                    cellRect.localScale = Vector3.one;
                }
                
                Vector2 basePos = GetExtendedCellBasePosition(extendedRow, colIndex + WRAP_PADDING);
                cell.transform.localPosition = new Vector3(basePos.x, basePos.y + totalOffset, 0);
                
                int virtualRow = extendedRow - WRAP_PADDING;
                float scrollOffsetInCells = totalOffset / cellDimensionWithSpacing;
                bool isVisible = (virtualRow + scrollOffsetInCells >= -WRAP_PADDING) && 
                                 (virtualRow + scrollOffsetInCells < gridSize + WRAP_PADDING);
                
                if (isVisible && cell.GetComponent<CanvasGroup>().alpha < 1f)
                {
                    cell.SetAlpha(1f);
                }
                else if (!isVisible && cell.GetComponent<CanvasGroup>().alpha > 0f)
                {
                    cell.SetAlpha(0f);
                }
                // DO NOT SET LETTER HERE
            }
        }
    }   
}