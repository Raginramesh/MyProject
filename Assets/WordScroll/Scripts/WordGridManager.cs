using UnityEngine;
using TMPro; // Required for TextMeshPro components
using DG.Tweening; // Required for DOTween animations
using System.Collections.Generic; // Required for Lists
using System.Linq; // Required for Enumerable operations (like Repeat)

public class WordGridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int _gridSize = 4; // Number of cells per side
    [SerializeField] private float _cellSize = 100f; // Size of each cell UI element
    [SerializeField] private float _spacing = 10f; // Space between cells
    [SerializeField] private GameObject letterCellPrefab; // Prefab for the visual cell (must have RectTransform and TextMeshProUGUI child)
    [SerializeField] private Transform gridParent; // Parent Transform under Canvas where cells will be instantiated

    // Public readonly properties for other scripts to access grid settings
    public int gridSize => _gridSize;
    public float cellSize => _cellSize;
    public float spacing => _spacing;

    [Header("References")]
    [SerializeField] private WordValidator wordValidator; // Reference to the script that finds words
    [SerializeField] private GameManager gameManager; // Reference to the main game state manager

    // Internal grid data representation
    public char[,] gridData { get; private set; } // Stores the character in each cell (logical grid)
    public RectTransform[,] gridCellRects { get; private set; } // Stores references to the UI RectTransforms (visual grid)
    private TextMeshProUGUI[,] gridCellTextComponents; // Stores references to the Text components for updating letters
    private LetterCell[,] gridCellComponents; // Stores LetterCell component references

    // Weighted list for generating random letters based on frequency
    private List<char> WeightedLetters = new List<char>();

    // State flag to prevent input/actions during animations
    public bool isAnimating { get; private set; } = false;


    // --- Initialization and Setup ---

    void Awake()
    {
        // Initialize animation state
        isAnimating = false;

        // Attempt to find references if not set in Inspector (robustness)
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
            if (gameManager == null) Debug.LogError("WordGridManager: GameManager not found in scene!", this);
            else Debug.Log("WordGridManager: Found GameManager using FindFirstObjectByType.", this);
        }
        if (wordValidator == null)
        {
            Debug.LogError("WordGridManager: WordValidator reference not set in Inspector!", this);
            enabled = false; // Disable if critical reference is missing
            return;
        }
    }

    void Start()
    {
        // Ensure animation state is false on start
        isAnimating = false;
        // Prepare the weighted letter list for random generation
        PopulateWeightedLettersList();
        // Grid initialization is now typically triggered by GameManager entering the 'Playing' state
    }

    // Called when the component becomes enabled (e.g., after scene load or reactivation)
    void OnEnable()
    {
        // Reset animation state when enabled
        isAnimating = false;
        Debug.Log("WordGridManager Enabled, isAnimating reset to false.", this);
    }

    // Allows GameManager to explicitly set its reference (useful in some initialization orders)
    public void SetGameManager(GameManager manager)
    {
        gameManager = manager;
        if (gameManager != null) Debug.Log("WordGridManager: GameManager reference set via SetGameManager.", this);
    }

    // Builds or rebuilds the entire grid (visual and logical)
    public void InitializeGrid()
    {
        // --- Start of Initialization ---
        isAnimating = false; // Ensure animation state is false
        Debug.Log("WordGridManager: Initializing Grid... Setting isAnimating = false.", this);

        // --- Validate Prerequisites ---
        if (letterCellPrefab == null)
        {
            Debug.LogError("WordGridManager: Letter Cell Prefab is not assigned in the Inspector! Cannot initialize grid.", this);
            return;
        }
        if (gridParent == null)
        {
            Debug.LogWarning("WordGridManager: Grid Parent is not assigned in Inspector. Defaulting to this object's transform, which might not be ideal for UI positioning.", this);
            gridParent = this.transform; // Fallback, but might cause layout issues
        }

        // --- Cleanup Previous Grid (if exists) ---
        if (gridCellRects != null)
        {
            Debug.Log("Killing active tweens and destroying existing cells before re-initialization.", this);
            for (int r = 0; r < gridCellRects.GetLength(0); r++)
            {
                for (int c = 0; c < gridCellRects.GetLength(1); c++)
                {
                    if (gridCellRects[r, c] != null)
                    {
                        DOTween.Kill(gridCellRects[r, c]);
                        if (gridCellRects[r, c].gameObject != null)
                        {
                            Destroy(gridCellRects[r, c].gameObject);
                        }
                    }
                }
            }
        }

        // --- Initialize Data Structures ---
        gridData = new char[gridSize, gridSize];
        gridCellTextComponents = new TextMeshProUGUI[gridSize, gridSize];
        gridCellRects = new RectTransform[gridSize, gridSize];
        gridCellComponents = new LetterCell[gridSize, gridSize];

        // --- Populate Logical Grid ---
        PopulateGridData();

        // --- Create Visual Grid ---
        float totalGridSizeUI = gridSize * cellSize + (gridSize - 1) * spacing;
        float startOffset = -totalGridSizeUI / 2f + cellSize / 2f;
        int cellsCreated = 0;

        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                GameObject cellGO = Instantiate(letterCellPrefab, gridParent);
                cellsCreated++;

                RectTransform cellRect = cellGO.GetComponent<RectTransform>();
                TextMeshProUGUI cellText = cellGO.GetComponentInChildren<TextMeshProUGUI>(true);
                LetterCell cellComponent = cellGO.GetComponent<LetterCell>();

                // --- Sanity Checks for Prefab Structure ---
                if (cellRect == null) { Debug.LogError($"Prefab '{letterCellPrefab.name}' missing RectTransform!", cellGO); Destroy(cellGO); continue; }
                if (cellText == null) { Debug.LogError($"Prefab '{letterCellPrefab.name}' missing TextMeshProUGUI child!", cellGO); }
                if (cellComponent == null) { Debug.LogError($"Prefab '{letterCellPrefab.name}' missing LetterCell component!", cellGO); }

                float posX = startOffset + c * (cellSize + spacing);
                float posY = startOffset + (gridSize - 1 - r) * (cellSize + spacing);

                cellRect.anchoredPosition = new Vector2(posX, posY);
                cellRect.sizeDelta = new Vector2(cellSize, cellSize);
                cellRect.localScale = Vector3.one;

                if (cellText != null) { cellText.text = gridData[r, c].ToString(); }
                else { cellGO.name = $"Cell_{r}_{c}_(NoText)"; }

                gridCellTextComponents[r, c] = cellText;
                gridCellRects[r, c] = cellRect;
                gridCellComponents[r, c] = cellComponent;
            }
        }

        // --- End of Initialization ---
        Debug.Log($"WordGridManager: Grid Initialized. Created {cellsCreated} cells. isAnimating = {isAnimating}", this);
        if (cellsCreated != gridSize * gridSize) { Debug.LogWarning($"Expected {gridSize * gridSize} cells, reported {cellsCreated}.", this); }
    }

    // Fills the WeightedLetters list based on standard English letter frequencies
    void PopulateWeightedLettersList()
    {
        WeightedLetters.Clear();
        WeightedLetters.AddRange(Enumerable.Repeat('E', 12)); WeightedLetters.AddRange(Enumerable.Repeat('A', 9));
        WeightedLetters.AddRange(Enumerable.Repeat('I', 9)); WeightedLetters.AddRange(Enumerable.Repeat('O', 8));
        WeightedLetters.AddRange(Enumerable.Repeat('N', 6)); WeightedLetters.AddRange(Enumerable.Repeat('R', 6));
        WeightedLetters.AddRange(Enumerable.Repeat('T', 6)); WeightedLetters.AddRange(Enumerable.Repeat('L', 4));
        WeightedLetters.AddRange(Enumerable.Repeat('S', 4)); WeightedLetters.AddRange(Enumerable.Repeat('U', 4));
        WeightedLetters.AddRange(Enumerable.Repeat('D', 4)); WeightedLetters.AddRange(Enumerable.Repeat('G', 3));
        WeightedLetters.AddRange(Enumerable.Repeat('B', 2)); WeightedLetters.AddRange(Enumerable.Repeat('C', 2));
        WeightedLetters.AddRange(Enumerable.Repeat('M', 2)); WeightedLetters.AddRange(Enumerable.Repeat('P', 2));
        WeightedLetters.AddRange(Enumerable.Repeat('F', 2)); WeightedLetters.AddRange(Enumerable.Repeat('H', 2));
        WeightedLetters.AddRange(Enumerable.Repeat('V', 2)); WeightedLetters.AddRange(Enumerable.Repeat('W', 2));
        WeightedLetters.AddRange(Enumerable.Repeat('Y', 2)); WeightedLetters.AddRange(Enumerable.Repeat('K', 1));
        WeightedLetters.AddRange(Enumerable.Repeat('J', 1)); WeightedLetters.AddRange(Enumerable.Repeat('X', 1));
        WeightedLetters.AddRange(Enumerable.Repeat('Q', 1)); WeightedLetters.AddRange(Enumerable.Repeat('Z', 1));
    }

    // Fills the internal gridData array with random letters using the weighted list
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

    // Returns a random letter based on the weighted distribution
    char GetRandomLetter()
    {
        if (WeightedLetters.Count == 0) { Debug.LogWarning("WeightedLetters list empty! Returning '?'", this); return '?'; }
        return WeightedLetters[Random.Range(0, WeightedLetters.Count)];
    }


    // --- Scrolling Logic (Called by GridInputHandler) ---

    // Public method to initiate a row scroll
    public void RequestRowScroll(int rowIndex, int direction, float scrollAmount)
    {
        // Prevent scrolling if conditions not met
        if (!enabled || isAnimating || gameManager == null || gameManager.CurrentState != GameManager.GameState.Playing)
        {
            Debug.LogWarning($"RequestRowScroll ignored: Enabled={enabled}, isAnimating={isAnimating}, State={gameManager?.CurrentState}", this);
            return;
        }

        // --- Begin Scroll Action ---
        isAnimating = true;
        Debug.Log($"RequestRowScroll: Setting isAnimating = true. Row {rowIndex}, Dir {direction}", this);

        // --- MOVE REDUCTION REMOVED FROM HERE ---

        // Update the logical grid data first
        ShiftRowData(rowIndex, direction);

        // Start the visual animation
        AnimateRowScroll(rowIndex, direction, scrollAmount);
    }

    // Public method to initiate a column scroll
    public void RequestColumnScroll(int colIndex, int direction, float scrollAmount)
    {
        // Prevent scrolling if conditions not met
        if (!enabled || isAnimating || gameManager == null || gameManager.CurrentState != GameManager.GameState.Playing)
        {
            Debug.LogWarning($"RequestColumnScroll ignored: Enabled={enabled}, isAnimating={isAnimating}, State={gameManager?.CurrentState}", this);
            return;
        }

        // --- Begin Scroll Action ---
        isAnimating = true;
        Debug.Log($"RequestColumnScroll: Setting isAnimating = true. Col {colIndex}, Dir {direction}", this);

        // --- MOVE REDUCTION REMOVED FROM HERE ---

        // Update the logical grid data
        ShiftColumnData(colIndex, direction);

        // Start the visual animation
        AnimateColumnScroll(colIndex, direction, scrollAmount);
    }

    // --- Internal Data Shifting (Logical Grid Update) ---

    void ShiftRowData(int rowIndex, int direction)
    {
        if (rowIndex < 0 || rowIndex >= gridSize) { Debug.LogError($"ShiftRowData: Invalid rowIndex {rowIndex}.", this); return; }

        if (direction == 1) // Shift Right
        {
            char tempData = gridData[rowIndex, gridSize - 1];
            RectTransform tempRect = gridCellRects[rowIndex, gridSize - 1];
            TextMeshProUGUI tempText = gridCellTextComponents[rowIndex, gridSize - 1];
            LetterCell tempCell = gridCellComponents[rowIndex, gridSize - 1];
            for (int c = gridSize - 1; c > 0; c--)
            {
                gridData[rowIndex, c] = gridData[rowIndex, c - 1];
                gridCellRects[rowIndex, c] = gridCellRects[rowIndex, c - 1];
                gridCellTextComponents[rowIndex, c] = gridCellTextComponents[rowIndex, c - 1];
                gridCellComponents[rowIndex, c] = gridCellComponents[rowIndex, c - 1];
            }
            gridData[rowIndex, 0] = tempData;
            gridCellRects[rowIndex, 0] = tempRect;
            gridCellTextComponents[rowIndex, 0] = tempText;
            gridCellComponents[rowIndex, 0] = tempCell;
        }
        else // Shift Left
        {
            char tempData = gridData[rowIndex, 0];
            RectTransform tempRect = gridCellRects[rowIndex, 0];
            TextMeshProUGUI tempText = gridCellTextComponents[rowIndex, 0];
            LetterCell tempCell = gridCellComponents[rowIndex, 0];
            for (int c = 0; c < gridSize - 1; c++)
            {
                gridData[rowIndex, c] = gridData[rowIndex, c + 1];
                gridCellRects[rowIndex, c] = gridCellRects[rowIndex, c + 1];
                gridCellTextComponents[rowIndex, c] = gridCellTextComponents[rowIndex, c + 1];
                gridCellComponents[rowIndex, c] = gridCellComponents[rowIndex, c + 1];
            }
            gridData[rowIndex, gridSize - 1] = tempData;
            gridCellRects[rowIndex, gridSize - 1] = tempRect;
            gridCellTextComponents[rowIndex, gridSize - 1] = tempText;
            gridCellComponents[rowIndex, gridSize - 1] = tempCell;
        }
    }

    void ShiftColumnData(int colIndex, int direction)
    {
        if (colIndex < 0 || colIndex >= gridSize) { Debug.LogError($"ShiftColumnData: Invalid colIndex {colIndex}.", this); return; }

        if (direction == 1) // Shift Down
        {
            char tempData = gridData[gridSize - 1, colIndex];
            RectTransform tempRect = gridCellRects[gridSize - 1, colIndex];
            TextMeshProUGUI tempText = gridCellTextComponents[gridSize - 1, colIndex];
            LetterCell tempCell = gridCellComponents[gridSize - 1, colIndex];
            for (int r = gridSize - 1; r > 0; r--)
            {
                gridData[r, colIndex] = gridData[r - 1, colIndex];
                gridCellRects[r, colIndex] = gridCellRects[r - 1, colIndex];
                gridCellTextComponents[r, colIndex] = gridCellTextComponents[r - 1, colIndex];
                gridCellComponents[r, colIndex] = gridCellComponents[r - 1, colIndex];
            }
            gridData[0, colIndex] = tempData;
            gridCellRects[0, colIndex] = tempRect;
            gridCellTextComponents[0, colIndex] = tempText;
            gridCellComponents[0, colIndex] = tempCell;
        }
        else // Shift Up
        {
            char tempData = gridData[0, colIndex];
            RectTransform tempRect = gridCellRects[0, colIndex];
            TextMeshProUGUI tempText = gridCellTextComponents[0, colIndex];
            LetterCell tempCell = gridCellComponents[0, colIndex];
            for (int r = 0; r < gridSize - 1; r++)
            {
                gridData[r, colIndex] = gridData[r + 1, colIndex];
                gridCellRects[r, colIndex] = gridCellRects[r + 1, colIndex];
                gridCellTextComponents[r, colIndex] = gridCellTextComponents[r + 1, colIndex];
                gridCellComponents[r, colIndex] = gridCellComponents[r + 1, colIndex];
            }
            gridData[gridSize - 1, colIndex] = tempData;
            gridCellRects[gridSize - 1, colIndex] = tempRect;
            gridCellTextComponents[gridSize - 1, colIndex] = tempText;
            gridCellComponents[gridSize - 1, colIndex] = tempCell;
        }
    }


    // --- Visual Animation Logic (Using DOTween) ---

    void AnimateRowScroll(int rowIndex, int direction, float scrollAmount)
    {
        // Debug.Log($"AnimateRowScroll START. Row {rowIndex}. isAnimating should be true.", this);
        float totalMoveDistance = gridSize * (cellSize + spacing);
        float singleCellMove = direction * (cellSize + spacing);
        Sequence seq = DOTween.Sequence();

        if (gridCellRects == null || rowIndex < 0 || rowIndex >= gridCellRects.GetLength(0))
        {
            Debug.LogError($"AnimateRowScroll Error: gridCellRects null or rowIndex {rowIndex} out of bounds.", this);
            ResetAnimationFlag("RowScroll Error"); return;
        }

        for (int c = 0; c < gridSize; c++)
        {
            if (c < 0 || c >= gridCellRects.GetLength(1) || gridCellRects[rowIndex, c] == null)
            {
                Debug.LogWarning($"AnimateRowScroll Skipping animation for cell [{rowIndex},{c}] - RectTransform null or index out of bounds.", this);
                continue;
            }
            RectTransform cellRect = gridCellRects[rowIndex, c];
            Vector2 startPos = cellRect.anchoredPosition;
            seq.Join(cellRect.DOAnchorPosX(startPos.x + singleCellMove, 0.3f).SetEase(Ease.OutCubic));
        }

        int wrapIndex = (direction == 1) ? 0 : gridSize - 1;
        if (wrapIndex < 0 || wrapIndex >= gridCellRects.GetLength(1) || gridCellRects[rowIndex, wrapIndex] == null)
        {
            Debug.LogWarning($"AnimateRowScroll Skipping wrap logic for cell [{rowIndex},{wrapIndex}] - RectTransform null or index out of bounds.", this);
        }
        else
        {
            RectTransform wrapCell = gridCellRects[rowIndex, wrapIndex];
            Vector2 wrapStartPos = wrapCell.anchoredPosition;
            seq.InsertCallback(0.29f, () => {
                if (wrapCell != null) { wrapCell.anchoredPosition = new Vector2(wrapStartPos.x - direction * totalMoveDistance, wrapStartPos.y); }
                else { Debug.LogWarning("Wrap cell became null inside row scroll callback!", this); }
            });
        }

        seq.OnKill(() => { Debug.LogWarning("AnimateRowScroll Sequence KILLED.", this); ResetAnimationFlag("RowScroll Killed"); });
        seq.OnComplete(() => {
            // Debug.Log("AnimateRowScroll Sequence COMPLETE.", this);
            try { SnapToGridPositions(); } catch (System.Exception e) { Debug.LogError($"Error in SnapToGridPositions after row scroll: {e.Message}", this); }
            ResetAnimationFlag("RowScroll Complete");
        });
    }

    void AnimateColumnScroll(int colIndex, int direction, float scrollAmount)
    {
        // Debug.Log($"AnimateColumnScroll START. Col {colIndex}. isAnimating should be true.", this);
        float totalMoveDistance = gridSize * (cellSize + spacing);
        float singleCellMove = -direction * (cellSize + spacing);
        Sequence seq = DOTween.Sequence();

        if (gridCellRects == null || colIndex < 0 || colIndex >= gridCellRects.GetLength(1))
        {
            Debug.LogError($"AnimateColumnScroll Error: gridCellRects null or colIndex {colIndex} out of bounds.", this);
            ResetAnimationFlag("ColScroll Error"); return;
        }

        for (int r = 0; r < gridSize; r++)
        {
            if (r < 0 || r >= gridCellRects.GetLength(0) || gridCellRects[r, colIndex] == null)
            {
                Debug.LogWarning($"AnimateColumnScroll Skipping animation for cell [{r},{colIndex}] - RectTransform null or index out of bounds.", this);
                continue;
            }
            RectTransform cellRect = gridCellRects[r, colIndex];
            Vector2 startPos = cellRect.anchoredPosition;
            seq.Join(cellRect.DOAnchorPosY(startPos.y + singleCellMove, 0.3f).SetEase(Ease.OutCubic));
        }

        int wrapIndex = (direction == 1) ? 0 : gridSize - 1;
        if (wrapIndex < 0 || wrapIndex >= gridCellRects.GetLength(0) || gridCellRects[wrapIndex, colIndex] == null)
        {
            Debug.LogWarning($"AnimateColumnScroll Skipping wrap logic for cell [{wrapIndex},{colIndex}] - RectTransform null or index out of bounds.", this);
        }
        else
        {
            RectTransform wrapCell = gridCellRects[wrapIndex, colIndex];
            Vector2 wrapStartPos = wrapCell.anchoredPosition;
            seq.InsertCallback(0.29f, () => {
                if (wrapCell != null) { wrapCell.anchoredPosition = new Vector2(wrapStartPos.x, wrapStartPos.y + direction * totalMoveDistance); }
                else { Debug.LogWarning("Wrap cell became null inside column scroll callback!", this); }
            });
        }

        seq.OnKill(() => { Debug.LogWarning("AnimateColumnScroll Sequence KILLED.", this); ResetAnimationFlag("ColScroll Killed"); });
        seq.OnComplete(() => {
            // Debug.Log("AnimateColumnScroll Sequence COMPLETE.", this);
            try { SnapToGridPositions(); } catch (System.Exception e) { Debug.LogError($"Error in SnapToGridPositions after column scroll: {e.Message}", this); }
            ResetAnimationFlag("ColScroll Complete");
        });
    }


    // --- Utility Methods ---

    private void ResetAnimationFlag(string reason)
    {
        if (isAnimating)
        {
            isAnimating = false;
            Debug.Log($"ResetAnimationFlag: Setting isAnimating = false. Reason: {reason}", this);
        }
    }

    void SnapToGridPositions()
    {
        if (gridCellRects == null) { Debug.LogError("SnapToGridPositions: gridCellRects array is null.", this); return; }

        float totalGridSizeUI = gridSize * cellSize + (gridSize - 1) * spacing;
        float startOffset = -totalGridSizeUI / 2f + cellSize / 2f;

        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                if (gridCellRects[r, c] != null)
                {
                    float targetX = startOffset + c * (cellSize + spacing);
                    float targetY = startOffset + (gridSize - 1 - r) * (cellSize + spacing);
                    gridCellRects[r, c].anchoredPosition = new Vector2(targetX, targetY);
                }
            }
        }
    }


    // --- Letter Replacement Logic ---

    public void ReplaceLetter(int row, int col)
    {
        if (row < 0 || row >= gridSize || col < 0 || col >= gridSize) { Debug.LogWarning($"ReplaceLetter: Invalid coordinates ({row}, {col}).", this); return; }

        gridData[row, col] = GetRandomLetter();

        if (gridCellTextComponents != null && row < gridCellTextComponents.GetLength(0) && col < gridCellTextComponents.GetLength(1) && gridCellTextComponents[row, col] != null)
        {
            gridCellTextComponents[row, col].text = gridData[row, col].ToString();
            RectTransform cellRect = (gridCellRects != null && row < gridCellRects.GetLength(0) && col < gridCellRects.GetLength(1)) ? gridCellRects[row, col] : null;
            if (cellRect != null)
            {
                cellRect.localScale = Vector3.zero;
                cellRect.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
            }
        }
        else { Debug.LogWarning($"Cannot update visual text for replaced letter at [{row},{col}]", this); }
    }

    public void ReplaceLettersAt(List<Vector2Int> coordinates)
    {
        if (coordinates == null || coordinates.Count == 0) { Debug.Log("ReplaceLettersAt: Empty coordinate list.", this); return; }
        // Debug.Log($"Replacing letters at {coordinates.Count} coordinates.", this);

        // isAnimating = true; // Consider setting flag if effect takes time

        Sequence effectSequence = DOTween.Sequence();
        foreach (Vector2Int coord in coordinates)
        {
            if (coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
            {
                RectTransform cellRect = (gridCellRects != null && coord.x < gridCellRects.GetLength(0) && coord.y < gridCellRects.GetLength(1)) ? gridCellRects[coord.x, coord.y] : null;
                if (cellRect != null) { effectSequence.Join(cellRect.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack)); }
            }
            else { Debug.LogWarning($"ReplaceLettersAt: Skipping invalid coordinate ({coord.x},{coord.y}) during effect.", this); }
        }

        effectSequence.OnComplete(() => {
            // Debug.Log("Replacement effect complete. Replacing letters data/visuals.");
            foreach (Vector2Int coord in coordinates)
            {
                if (coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize) { ReplaceLetter(coord.x, coord.y); }
                else { Debug.LogWarning($"ReplaceLettersAt: Skipping invalid coordinate ({coord.x},{coord.y}) during replacement.", this); }
            }
            // Debug.Log("Replacement complete. Validation/Move Reduction triggered by Input Handler if needed.");
            // isAnimating = false; // Reset flag if set earlier
        });
    }


    // --- Public Method to Trigger Validation ---
    public void TriggerValidationCheck()
    {
        if (isAnimating) { Debug.LogWarning("TriggerValidationCheck called, but WordGridManager is still animating. Ignoring call.", this); return; }
        // Debug.Log("WordGridManager: TriggerValidationCheck received. Calling WordValidator.", this);
        if (wordValidator != null) { wordValidator.ValidateWords(); }
        else { Debug.LogError("Cannot trigger validation: WordValidator reference missing!", this); }
    }

    // --- Method to apply move reduction after grid settles ---
    /// <summary>
    /// Applies the move reduction logic to the specified row or column.
    /// Called by GridInputHandler from Update when pendingMoveReduction is true and grid is not animating.
    /// Also triggers the GameManager's move decrement.
    /// </summary>
    /// <param name="row">The row index that was scrolled (-1 if a column was scrolled).</param>
    /// <param name="col">The column index that was scrolled (-1 if a row was scrolled).</param>
    public void ApplyPendingMoveReduction(int row, int col)
    {
        if (row >= 0 && col >= 0) { Debug.LogError($"ApplyPendingMoveReduction called with both valid row ({row}) and col ({col})!", this); return; }
        if (row < 0 && col < 0) { Debug.LogWarning($"ApplyPendingMoveReduction called with invalid row ({row}) and col ({col}).", this); return; }

        bool wasRowScroll = row >= 0;
        int index = wasRowScroll ? row : col;
        string type = wasRowScroll ? "row" : "column";

        // --- Trigger GameManager Move Decrement ---
        // <<< NEW: Call DecrementMoves in GameManager for EVERY completed scroll action
        if (gameManager != null)
        {
            Debug.Log($"ApplyPendingMoveReduction: Triggering move decrement for completed scroll of {type} {index}.");
            gameManager.DecrementMoves(); // This will internally check if moves are enabled
        }
        else
        {
            Debug.LogError("ApplyPendingMoveReduction: Cannot decrement moves, GameManager reference is missing!", this);
        }


        // --- Original LetterCell Move Reduction Logic (Optional based on LetterCell settings) ---
        // This part still handles the individual moves *on* the cells, if enabled on the LetterCell component.
        // It's separate from the main game move count.
        Debug.Log($"ApplyPendingMoveReduction: Applying LetterCell moves reduction (if enabled on cells) for {type} {index}");
        bool anyCellMovesReduced = false;

        if (wasRowScroll) // Reduce moves for LetterCells in a Row
        {
            for (int c = 0; c < gridSize; c++)
            {
                LetterCell cell = GetLetterCellAt(row, c);
                // Only count if ReduceMove returns true AND the cell actually has moves enabled
                if (cell != null && cell.ReduceMove() && cell.EnableMoves) { anyCellMovesReduced = true; }
            }
        }
        else // Reduce moves for LetterCells in a Column
        {
            for (int r = 0; r < gridSize; r++)
            {
                LetterCell cell = GetLetterCellAt(r, col);
                // Only count if ReduceMove returns true AND the cell actually has moves enabled
                if (cell != null && cell.ReduceMove() && cell.EnableMoves) { anyCellMovesReduced = true; }
            }
        }

        if (anyCellMovesReduced) { Debug.Log($"ApplyPendingMoveReduction: Finished reducing LetterCell moves for {type} {index}."); }
        else { Debug.Log($"ApplyPendingMoveReduction: No LetterCell moves were reduced for {type} {index} (or cells have moves disabled)."); }
    }

    // --- Public method to get LetterCell component ---
    public LetterCell GetLetterCellAt(int row, int col)
    {
        if (gridCellComponents == null || row < 0 || row >= gridCellComponents.GetLength(0) || col < 0 || col >= gridCellComponents.GetLength(1)) { return null; }
        return gridCellComponents[row, col];
    }

} // End of WordGridManager class