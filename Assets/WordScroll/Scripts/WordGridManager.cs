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
    [SerializeField] private GameObject letterCellPrefab; // Prefab for the visual cell (MUST have CellController, RectTransform, CanvasGroup, and optionally TextMeshProUGUI child, LetterCell)
    [SerializeField] private Transform gridParent; // Parent Transform under Canvas where cells will be instantiated

    // Public readonly properties for other scripts to access grid settings
    public int gridSize => _gridSize;
    public float cellSize => _cellSize;
    public float spacing => _spacing;

    [Header("Appearance")] // <<< NEW HEADER
    [Tooltip("Duration for new letters fading in after replacement.")]
    [SerializeField] private float cellFadeInDuration = 0.3f; // <<< NEW FIELD

    [Header("References")]
    [SerializeField] private WordValidator wordValidator; // Reference to the script that finds words
    [SerializeField] private GameManager gameManager; // Reference to the main game state manager

    // Internal grid data representation
    public char[,] gridData { get; private set; } // Stores the character in each cell (logical grid)
    private CellController[,] gridCells; // <<< MODIFIED: Stores CellController references
    private LetterCell[,] gridCellComponents; // Stores LetterCell component references (kept from base for move reduction logic)

    // Removed: gridCellRects and gridCellTextComponents - managed by CellController

    // Weighted list for generating random letters based on frequency
    private List<char> WeightedLetters = new List<char>();

    // State flag to prevent input/actions during animations (kept from base)
    public bool isAnimating { get; private set; } = false;


    // --- Initialization and Setup ---

    void Awake()
    {
        isAnimating = false;
        // Attempt to find references if not set in Inspector
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (wordValidator == null) { Debug.LogError("WordGridManager: WordValidator reference not set in Inspector!", this); enabled = false; return; }
        if (gameManager == null) Debug.LogError("WordGridManager: GameManager not found in scene!", this);
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
        // Debug.Log("WordGridManager Enabled, isAnimating reset to false.", this);
    }

    // Allows GameManager to explicitly set its reference (useful in some initialization orders)
    public void SetGameManager(GameManager manager)
    {
        gameManager = manager;
        // if (gameManager != null) Debug.Log("WordGridManager: GameManager reference set via SetGameManager.", this);
    }

    // Builds or rebuilds the entire grid (visual and logical)
    public void InitializeGrid()
    {
        // --- Start of Initialization ---
        isAnimating = false; // Ensure animation state is false
        // Debug.Log("WordGridManager: Initializing Grid... Setting isAnimating = false.", this);

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
        if (gridCells != null) // <<< MODIFIED check
        {
            // Debug.Log("Killing active tweens and destroying existing cells before re-initialization.", this);
            for (int r = 0; r < gridCells.GetLength(0); r++)
            {
                for (int c = 0; c < gridCells.GetLength(1); c++)
                {
                    if (gridCells[r, c] != null)
                    {
                        // Kill tweens associated with the GameObject or specific components if needed
                        DOTween.Kill(gridCells[r, c].transform); // Kill tweens on transform
                        if (gridCells[r, c].TryGetComponent<CanvasGroup>(out var cg))
                        {
                            DOTween.Kill(cg); // Kill tweens on CanvasGroup
                        }
                        if (gridCells[r, c].gameObject != null)
                        {
                            Destroy(gridCells[r, c].gameObject);
                        }
                    }
                }
            }
        }

        // --- Initialize Data Structures ---
        gridData = new char[gridSize, gridSize];
        gridCells = new CellController[gridSize, gridSize]; // <<< MODIFIED type
        gridCellComponents = new LetterCell[gridSize, gridSize]; // Kept for LetterCell logic

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

                // --- Get Components ---
                CellController cellController = cellGO.GetComponent<CellController>();
                LetterCell cellComponent = cellGO.GetComponent<LetterCell>(); // Get LetterCell if exists

                // --- Sanity Checks ---
                if (cellController == null) { Debug.LogError($"Prefab '{letterCellPrefab.name}' missing CellController component!", cellGO); Destroy(cellGO); continue; }
                // Optional check for LetterCell if it's strictly required by other logic
                // if (cellComponent == null) { Debug.LogWarning($"Prefab '{letterCellPrefab.name}' missing LetterCell component.", cellGO); }

                // --- Position and Scale ---
                RectTransform cellRect = cellController.RectTransform; // Get RectTransform via CellController
                float posX = startOffset + c * (cellSize + spacing);
                float posY = startOffset + (gridSize - 1 - r) * (cellSize + spacing);
                cellRect.anchoredPosition = new Vector2(posX, posY);
                cellRect.sizeDelta = new Vector2(cellSize, cellSize);
                cellRect.localScale = Vector3.one;

                // --- Set Initial State ---
                char letter = gridData[r, c];
                cellController.SetLetter(letter);
                cellController.SetAlpha(1f); // Ensure visible

                // --- Store References ---
                gridCells[r, c] = cellController;
                gridCellComponents[r, c] = cellComponent; // Store LetterCell
            }
        }

        // --- End of Initialization ---
        // Debug.Log($"WordGridManager: Grid Initialized. Created {cellsCreated} cells. isAnimating = {isAnimating}", this);
        if (cellsCreated != gridSize * gridSize) { Debug.LogWarning($"Expected {gridSize * gridSize} cells, reported {cellsCreated}.", this); }
    }

    // Fills the WeightedLetters list based on standard English letter frequencies
    void PopulateWeightedLettersList()
    {
        WeightedLetters.Clear();
        // Standard English letter frequencies (approximation)
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
            // Debug.LogWarning($"RequestRowScroll ignored: Enabled={enabled}, isAnimating={isAnimating}, State={gameManager?.CurrentState}", this);
            return;
        }

        // --- Begin Scroll Action ---
        isAnimating = true;
        // Debug.Log($"RequestRowScroll: Setting isAnimating = true. Row {rowIndex}, Dir {direction}", this);

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
            // Debug.LogWarning($"RequestColumnScroll ignored: Enabled={enabled}, isAnimating={isAnimating}, State={gameManager?.CurrentState}", this);
            return;
        }

        // --- Begin Scroll Action ---
        isAnimating = true;
        // Debug.Log($"RequestColumnScroll: Setting isAnimating = true. Col {colIndex}, Dir {direction}", this);

        // Update the logical grid data
        ShiftColumnData(colIndex, direction);

        // Start the visual animation
        AnimateColumnScroll(colIndex, direction, scrollAmount);
    }

    // --- Internal Data Shifting (Logical Grid Update) ---

    void ShiftRowData(int rowIndex, int direction) // <<< MODIFIED
    {
        if (rowIndex < 0 || rowIndex >= gridSize) { Debug.LogError($"ShiftRowData: Invalid rowIndex {rowIndex}.", this); return; }

        if (direction == 1) // Shift Right
        {
            char tempData = gridData[rowIndex, gridSize - 1];
            CellController tempCellController = gridCells[rowIndex, gridSize - 1]; // Get CellController
            LetterCell tempLetterCell = gridCellComponents[rowIndex, gridSize - 1]; // Get LetterCell
            for (int c = gridSize - 1; c > 0; c--)
            {
                gridData[rowIndex, c] = gridData[rowIndex, c - 1];
                gridCells[rowIndex, c] = gridCells[rowIndex, c - 1]; // Shift CellController
                gridCellComponents[rowIndex, c] = gridCellComponents[rowIndex, c - 1]; // Shift LetterCell
            }
            gridData[rowIndex, 0] = tempData;
            gridCells[rowIndex, 0] = tempCellController; // Assign wrapped CellController
            gridCellComponents[rowIndex, 0] = tempLetterCell; // Assign wrapped LetterCell
        }
        else // Shift Left
        {
            char tempData = gridData[rowIndex, 0];
            CellController tempCellController = gridCells[rowIndex, 0];
            LetterCell tempLetterCell = gridCellComponents[rowIndex, 0];
            for (int c = 0; c < gridSize - 1; c++)
            {
                gridData[rowIndex, c] = gridData[rowIndex, c + 1];
                gridCells[rowIndex, c] = gridCells[rowIndex, c + 1];
                gridCellComponents[rowIndex, c] = gridCellComponents[rowIndex, c + 1];
            }
            gridData[rowIndex, gridSize - 1] = tempData;
            gridCells[rowIndex, gridSize - 1] = tempCellController;
            gridCellComponents[rowIndex, gridSize - 1] = tempLetterCell;
        }
    }

    void ShiftColumnData(int colIndex, int direction) // <<< MODIFIED
    {
        if (colIndex < 0 || colIndex >= gridSize) { Debug.LogError($"ShiftColumnData: Invalid colIndex {colIndex}.", this); return; }

        if (direction == 1) // Shift Down
        {
            char tempData = gridData[gridSize - 1, colIndex];
            CellController tempCellController = gridCells[gridSize - 1, colIndex];
            LetterCell tempLetterCell = gridCellComponents[gridSize - 1, colIndex];
            for (int r = gridSize - 1; r > 0; r--)
            {
                gridData[r, colIndex] = gridData[r - 1, colIndex];
                gridCells[r, colIndex] = gridCells[r - 1, colIndex];
                gridCellComponents[r, colIndex] = gridCellComponents[r - 1, colIndex];
            }
            gridData[0, colIndex] = tempData;
            gridCells[0, colIndex] = tempCellController;
            gridCellComponents[0, colIndex] = tempLetterCell;
        }
        else // Shift Up
        {
            char tempData = gridData[0, colIndex];
            CellController tempCellController = gridCells[0, colIndex];
            LetterCell tempLetterCell = gridCellComponents[0, colIndex];
            for (int r = 0; r < gridSize - 1; r++)
            {
                gridData[r, colIndex] = gridData[r + 1, colIndex];
                gridCells[r, colIndex] = gridCells[r + 1, colIndex];
                gridCellComponents[r, colIndex] = gridCellComponents[r + 1, colIndex];
            }
            gridData[gridSize - 1, colIndex] = tempData;
            gridCells[gridSize - 1, colIndex] = tempCellController;
            gridCellComponents[gridSize - 1, colIndex] = tempLetterCell;
        }
    }


    // --- Visual Animation Logic (Using DOTween) ---

    void AnimateRowScroll(int rowIndex, int direction, float scrollAmount) // <<< MODIFIED
    {
        // Debug.Log($"AnimateRowScroll START. Row {rowIndex}. isAnimating should be true.", this);
        float totalMoveDistance = gridSize * (cellSize + spacing);
        float singleCellMove = direction * (cellSize + spacing);
        Sequence seq = DOTween.Sequence();

        if (gridCells == null || rowIndex < 0 || rowIndex >= gridCells.GetLength(0))
        {
            Debug.LogError($"AnimateRowScroll Error: gridCells null or rowIndex {rowIndex} out of bounds.", this);
            ResetAnimationFlag("RowScroll Error"); return;
        }

        for (int c = 0; c < gridSize; c++)
        {
            if (c < 0 || c >= gridCells.GetLength(1) || gridCells[rowIndex, c] == null)
            {
                // Debug.LogWarning($"AnimateRowScroll Skipping animation for cell [{rowIndex},{c}] - Controller null or index out of bounds.", this);
                continue;
            }
            RectTransform cellRect = gridCells[rowIndex, c].RectTransform; // Get Rect from CellController
            Vector2 startPos = cellRect.anchoredPosition;
            seq.Join(cellRect.DOAnchorPosX(startPos.x + singleCellMove, 0.3f).SetEase(Ease.OutCubic));
        }

        int wrapIndex = (direction == 1) ? 0 : gridSize - 1;
        if (wrapIndex < 0 || wrapIndex >= gridCells.GetLength(1) || gridCells[rowIndex, wrapIndex] == null)
        {
            // Debug.LogWarning($"AnimateRowScroll Skipping wrap logic for cell [{rowIndex},{wrapIndex}] - Controller null or index out of bounds.", this);
        }
        else
        {
            RectTransform wrapCellRect = gridCells[rowIndex, wrapIndex].RectTransform; // Get Rect from CellController
            Vector2 wrapStartPos = wrapCellRect.anchoredPosition;
            // Use InsertCallback to ensure position is set just before the visual wrap would occur
            seq.InsertCallback(0.29f, () => {
                if (wrapCellRect != null) { wrapCellRect.anchoredPosition = new Vector2(wrapStartPos.x - direction * totalMoveDistance, wrapStartPos.y); }
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

    void AnimateColumnScroll(int colIndex, int direction, float scrollAmount) // <<< MODIFIED
    {
        // Debug.Log($"AnimateColumnScroll START. Col {colIndex}. isAnimating should be true.", this);
        float totalMoveDistance = gridSize * (cellSize + spacing);
        float singleCellMove = -direction * (cellSize + spacing); // Negative because Y increases upwards in UI
        Sequence seq = DOTween.Sequence();

        if (gridCells == null || colIndex < 0 || colIndex >= gridCells.GetLength(1))
        {
            Debug.LogError($"AnimateColumnScroll Error: gridCells null or colIndex {colIndex} out of bounds.", this);
            ResetAnimationFlag("ColScroll Error"); return;
        }

        for (int r = 0; r < gridSize; r++)
        {
            if (r < 0 || r >= gridCells.GetLength(0) || gridCells[r, colIndex] == null)
            {
                // Debug.LogWarning($"AnimateColumnScroll Skipping animation for cell [{r},{colIndex}] - Controller null or index out of bounds.", this);
                continue;
            }
            RectTransform cellRect = gridCells[r, colIndex].RectTransform; // Get Rect from CellController
            Vector2 startPos = cellRect.anchoredPosition;
            seq.Join(cellRect.DOAnchorPosY(startPos.y + singleCellMove, 0.3f).SetEase(Ease.OutCubic));
        }

        int wrapIndex = (direction == 1) ? 0 : gridSize - 1; // Down scroll means index 0 wraps to bottom
        if (wrapIndex < 0 || wrapIndex >= gridCells.GetLength(0) || gridCells[wrapIndex, colIndex] == null)
        {
            // Debug.LogWarning($"AnimateColumnScroll Skipping wrap logic for cell [{wrapIndex},{colIndex}] - Controller null or index out of bounds.", this);
        }
        else
        {
            RectTransform wrapCellRect = gridCells[wrapIndex, colIndex].RectTransform; // Get Rect from CellController
            Vector2 wrapStartPos = wrapCellRect.anchoredPosition;
            // Use InsertCallback to set position just before visual wrap
            seq.InsertCallback(0.29f, () => {
                if (wrapCellRect != null) { wrapCellRect.anchoredPosition = new Vector2(wrapStartPos.x, wrapStartPos.y + direction * totalMoveDistance); }
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
            // Debug.Log($"ResetAnimationFlag: Setting isAnimating = false. Reason: {reason}", this);
        }
    }

    void SnapToGridPositions() // <<< MODIFIED
    {
        if (gridCells == null) { Debug.LogError("SnapToGridPositions: gridCells array is null.", this); return; }

        float totalGridSizeUI = gridSize * cellSize + (gridSize - 1) * spacing;
        float startOffset = -totalGridSizeUI / 2f + cellSize / 2f;

        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                if (gridCells[r, c] != null)
                {
                    float targetX = startOffset + c * (cellSize + spacing);
                    float targetY = startOffset + (gridSize - 1 - r) * (cellSize + spacing); // Y calculation depends on grid origin (bottom-left or top-left)
                    gridCells[r, c].RectTransform.anchoredPosition = new Vector2(targetX, targetY); // Use CellController's Rect
                }
            }
        }
        // Debug.Log("SnapToGridPositions completed.");
    }


    // --- Letter Replacement Logic ---

    // Internal helper method - likely less used now, but kept for potential direct calls.
    void ReplaceLetter(int row, int col) // <<< MODIFIED to use CellController
    {
        if (row < 0 || row >= gridSize || col < 0 || col >= gridSize) { Debug.LogWarning($"ReplaceLetter: Invalid coordinates ({row}, {col}).", this); return; }

        gridData[row, col] = GetRandomLetter(); // Update logical data

        CellController cellController = GetCellController(new Vector2Int(row, col)); // Get the controller

        if (cellController != null)
        {
            cellController.SetLetter(gridData[row, col]); // Update visual letter

            // Re-apply scale animation if this method is called directly
            // Assumes the cell might be at scale zero if called after a list replacement
            if (cellController.RectTransform.localScale == Vector3.zero)
            {
                cellController.RectTransform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
            }
            else // If called directly on a visible cell, maybe just a quick pop?
            {
                cellController.RectTransform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 10, 1);
            }

            // Ensure it's visible (might be redundant if scale anim handles it)
            cellController.SetAlpha(1f);
        }
        else { Debug.LogWarning($"Cannot update visual for replaced letter at [{row},{col}]", this); }
    }

    // Public method called by GameManager <<< MODIFIED >>>
    /// <summary>
    /// Replaces letters at specified coordinates with new random letters.
    /// Can optionally fade the new letters in. Assumes original cells are already faded out.
    /// </summary>
    /// <param name="coordinates">List of grid coordinates to replace.</param>
    /// <param name="fadeIn">If true, new letters will fade in; otherwise, they appear instantly.</param>
    public void ReplaceLettersAt(List<Vector2Int> coordinates, bool fadeIn = false)
    {
        if (coordinates == null || coordinates.Count == 0) { /* Debug.Log("ReplaceLettersAt: Empty coordinate list.") */ return; }
        // Debug.Log($"Replacing letters at {coordinates.Count} coordinates. FadeIn: {fadeIn}");

        // This method now focuses on setting new data and triggering the fade IN via CellController.

        foreach (Vector2Int coord in coordinates)
        {
            // Check bounds
            if (coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
            {
                char newLetter = GetRandomLetter();
                gridData[coord.x, coord.y] = newLetter; // Update data model

                CellController cellController = GetCellController(coord); // Get the controller for this cell
                if (cellController != null)
                {
                    cellController.SetLetter(newLetter); // Update visual letter

                    if (fadeIn)
                    {
                        // Start the fade-in animation via CellController
                        // Assumes the cell is currently at alpha 0 (faded out by GameManager)
                        cellController.FadeIn(cellFadeInDuration);
                    }
                    else
                    {
                        // Ensure it's instantly visible if not fading
                        cellController.SetAlpha(1f);
                    }
                }
                else { Debug.LogWarning($"ReplaceLettersAt: Cannot find CellController at {coord}"); }
            }
            else { Debug.LogWarning($"ReplaceLettersAt: Skipping invalid coordinate {coord}"); }
        }
        // No sequence or isAnimating flag needed here as fade-in is handled per cell by CellController
        // Validation should be triggered externally by GameManager or InputHandler if needed after replacement
    }


    // --- Public Method to Trigger Validation ---
    // Called by GridInputHandler after animation completes
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
        if (gameManager != null)
        {
            // Debug.Log($"ApplyPendingMoveReduction: Triggering move decrement for completed scroll of {type} {index}.");
            gameManager.DecrementMoves(); // GameManager checks if move mode is active
        }
        else
        {
            Debug.LogError("ApplyPendingMoveReduction: Cannot decrement moves, GameManager reference is missing!", this);
        }


        // --- Original LetterCell Move Reduction Logic (Optional based on LetterCell settings) ---
        // Debug.Log($"ApplyPendingMoveReduction: Applying LetterCell moves reduction (if enabled on cells) for {type} {index}");
        bool anyCellMovesReduced = false;

        if (wasRowScroll) // Reduce moves for LetterCells in a Row
        {
            for (int c = 0; c < gridSize; c++)
            {
                LetterCell cell = GetLetterCellAt(row, c); // Use existing method to get LetterCell
                // Only count if ReduceMove returns true AND the cell actually has moves enabled
                if (cell != null && cell.EnableMoves && cell.ReduceMove()) { anyCellMovesReduced = true; }
            }
        }
        else // Reduce moves for LetterCells in a Column
        {
            for (int r = 0; r < gridSize; r++)
            {
                LetterCell cell = GetLetterCellAt(r, col); // Use existing method to get LetterCell
                // Only count if ReduceMove returns true AND the cell actually has moves enabled
                if (cell != null && cell.EnableMoves && cell.ReduceMove()) { anyCellMovesReduced = true; }
            }
        }

        //if (anyCellMovesReduced) { Debug.Log($"ApplyPendingMoveReduction: Finished reducing LetterCell moves for {type} {index}."); }
        //else { Debug.Log($"ApplyPendingMoveReduction: No LetterCell moves were reduced for {type} {index} (or cells have moves disabled)."); }
    }

    // --- Public method to get LetterCell component (Kept from base) ---
    public LetterCell GetLetterCellAt(int row, int col)
    {
        if (gridCellComponents == null || row < 0 || row >= gridCellComponents.GetLength(0) || col < 0 || col >= gridCellComponents.GetLength(1)) { return null; }
        return gridCellComponents[row, col];
    }

    // --- <<< NEW: Public method to get CellController component >>> ---
    /// <summary>
    /// Gets the CellController at the specified coordinate. Returns null if out of bounds or cell missing.
    /// </summary>
    public CellController GetCellController(Vector2Int coord)
    {
        if (gridCells != null && coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
        {
            return gridCells[coord.x, coord.y];
        }
        // Debug.LogWarning($"WGM: GetCellController - Coordinate out of bounds or gridCells null: {coord}");
        return null;
    }

} // End of WordGridManager class