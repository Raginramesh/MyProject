using UnityEngine;
using UnityEngine.UI; // Required for Image component
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

    [Header("Appearance")]
    [Tooltip("Duration for new letters fading in after replacement.")]
    [SerializeField] private float cellFadeInDuration = 0.3f;
    /// <summary>Gets the configured duration for cell fade-in animations.</summary>
    public float CellFadeInDuration => cellFadeInDuration;

    // <<< NEW: Add these color fields >>>
    [Tooltip("Primary color for grid cells.")]
    [SerializeField] private Color cellColorPrimary = Color.white;
    [Tooltip("Alternate color for grid cells (for checkerboard pattern).")]
    [SerializeField] private Color cellColorAlternate = new Color(0.9f, 0.9f, 0.9f, 1f); // A light gray

    [Header("References")]
    [SerializeField] private WordValidator wordValidator; // Reference to the script that finds words
    [SerializeField] private GameManager gameManager; // Reference to the main game state manager

    // Internal grid data representation
    public char[,] gridData { get; private set; } // Stores the character in each cell (logical grid)
    private CellController[,] gridCells; // Stores CellController references
    private LetterCell[,] gridCellComponents; // Stores LetterCell component references (kept from base for move reduction logic)

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
        if (wordValidator == null) wordValidator = FindFirstObjectByType<WordValidator>(); // Also try to find validator

        if (wordValidator == null) { Debug.LogError("WordGridManager: WordValidator reference not set or found!", this); enabled = false; return; }
        // GameManager might be set via SetGameManager, so warning is sufficient here
        if (gameManager == null) Debug.LogWarning("WordGridManager: GameManager not found in Awake (may be set later).", this);
    }

    void Start()
    {
        isAnimating = false;
        PopulateWeightedLettersList();
        // Grid initialization triggered by GameManager
    }

    void OnEnable()
    {
        isAnimating = false;
    }

    public void SetGameManager(GameManager manager) { gameManager = manager; }

    public void InitializeGrid()
    {
        isAnimating = false;
        // Debug.Log("WordGridManager: Initializing Grid... Setting isAnimating = false.", this);

        if (letterCellPrefab == null) { Debug.LogError("WGM: Letter Cell Prefab missing!", this); return; }
        if (gridParent == null) { Debug.LogWarning("WGM: Grid Parent missing, using this transform.", this); gridParent = this.transform; }

        // --- Cleanup Previous Grid (if exists) ---
        if (gridCells != null)
        {
            // Debug.Log("Killing active tweens and destroying existing cells before re-initialization.", this);
            // Iterate through the potential size of the old grid
            for (int r = 0; r < gridCells.GetLength(0); r++)
            {
                for (int c = 0; c < gridCells.GetLength(1); c++)
                {
                    // Check if a CellController exists at this position
                    if (gridCells[r, c] != null)
                    {
                        // Kill DOTween animations associated with the cell's transform
                        DOTween.Kill(gridCells[r, c].transform);
                        // Try to get CanvasGroup and kill its tweens too
                        if (gridCells[r, c].TryGetComponent<CanvasGroup>(out var cg)) { DOTween.Kill(cg); }
                        // Check if the GameObject still exists before destroying
                        if (gridCells[r, c].gameObject != null)
                        {
                            // Use DestroyImmediate if in Editor and not playing, otherwise use Destroy
                            if (Application.isEditor && !Application.isPlaying) { DestroyImmediate(gridCells[r, c].gameObject); }
                            else { Destroy(gridCells[r, c].gameObject); }
                        }
                    }
                }
            }
            // Nullify the arrays after cleanup
            gridCells = null;
            gridCellComponents = null;
            gridData = null;
        }

        // --- Initialize Data Structures ---
        gridData = new char[gridSize, gridSize];
        gridCells = new CellController[gridSize, gridSize];
        gridCellComponents = new LetterCell[gridSize, gridSize];

        PopulateGridData(); // Fill logical grid

        // --- Create Visual Grid ---
        float totalGridSizeUI = gridSize * cellSize + (gridSize - 1) * spacing;
        float startOffset = -totalGridSizeUI / 2f + cellSize / 2f; // Center the grid
        int cellsCreated = 0;

        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                // Instantiate the prefab as a child of the gridParent
                GameObject cellGO = Instantiate(letterCellPrefab, gridParent);
                cellsCreated++;

                // --- Get Components ---
                CellController cellController = cellGO.GetComponent<CellController>();
                LetterCell cellComponent = cellGO.GetComponent<LetterCell>(); // Get LetterCell if exists
                Image cellImage = cellGO.GetComponent<Image>(); // <<< NEW: Get the Image component

                // --- Sanity Checks ---
                if (cellController == null) { Debug.LogError($"Prefab \'{letterCellPrefab.name}\' missing CellController component!", cellGO); Destroy(cellGO); continue; }
                if (cellController.RectTransform == null) { Debug.LogError($"CellController on prefab \'{letterCellPrefab.name}\' missing RectTransform!", cellGO); Destroy(cellGO); continue; }
                // <<< NEW: Check for Image component >>>
                if (cellImage == null) { Debug.LogWarning($"Prefab \'{letterCellPrefab.name}\' missing Image component. Cannot set alternating colors.", cellGO); }


                // --- Position and Scale ---
                RectTransform cellRect = cellController.RectTransform;
                float posX = startOffset + c * (cellSize + spacing);
                float posY = startOffset + (gridSize - 1 - r) * (cellSize + spacing); // Origin assumed bottom-left for calculation, visually top-left
                cellRect.anchoredPosition = new Vector2(posX, posY);
                cellRect.sizeDelta = new Vector2(cellSize, cellSize); // Set cell dimensions
                cellRect.localScale = Vector3.one; // Ensure correct scale

                // --- Set Initial State ---
                char letter = gridData[r, c];
                cellController.SetLetter(letter); // Set the visual letter
                cellController.SetAlpha(1f); // Ensure fully visible

                // <<< NEW: Apply alternating color >>>
                if (cellImage != null)
                {
                    if ((r + c) % 2 == 0)
                    {
                        cellImage.color = cellColorPrimary;
                    }
                    else
                    {
                        cellImage.color = cellColorAlternate;
                    }
                }

                // --- Store References ---
                gridCells[r, c] = cellController;
                gridCellComponents[r, c] = cellComponent; // Store LetterCell (can be null if prefab doesn't have it)
            }
        }
        // --- End of Initialization ---
        // Debug.Log($"WordGridManager: Grid Initialized. Created {cellsCreated} cells. isAnimating = {isAnimating}", this);
        if (cellsCreated != gridSize * gridSize) { Debug.LogWarning($"Expected {gridSize * gridSize} cells, but reported {cellsCreated} created.", this); }
    }

    // Fills the WeightedLetters list based on standard English letter frequencies
    void PopulateWeightedLettersList()
    {
        WeightedLetters.Clear();
        // Standard English letter frequencies (approximation) - Expanded List
        WeightedLetters.AddRange(Enumerable.Repeat('E', 12));
        WeightedLetters.AddRange(Enumerable.Repeat('A', 9));
        WeightedLetters.AddRange(Enumerable.Repeat('I', 9));
        WeightedLetters.AddRange(Enumerable.Repeat('O', 8));
        WeightedLetters.AddRange(Enumerable.Repeat('N', 6));
        WeightedLetters.AddRange(Enumerable.Repeat('R', 6));
        WeightedLetters.AddRange(Enumerable.Repeat('T', 6));
        WeightedLetters.AddRange(Enumerable.Repeat('L', 4));
        WeightedLetters.AddRange(Enumerable.Repeat('S', 4));
        WeightedLetters.AddRange(Enumerable.Repeat('U', 4));
        WeightedLetters.AddRange(Enumerable.Repeat('D', 4));
        WeightedLetters.AddRange(Enumerable.Repeat('G', 3));
        WeightedLetters.AddRange(Enumerable.Repeat('B', 2));
        WeightedLetters.AddRange(Enumerable.Repeat('C', 2));
        WeightedLetters.AddRange(Enumerable.Repeat('M', 2));
        WeightedLetters.AddRange(Enumerable.Repeat('P', 2));
        WeightedLetters.AddRange(Enumerable.Repeat('F', 2));
        WeightedLetters.AddRange(Enumerable.Repeat('H', 2));
        WeightedLetters.AddRange(Enumerable.Repeat('V', 2));
        WeightedLetters.AddRange(Enumerable.Repeat('W', 2));
        WeightedLetters.AddRange(Enumerable.Repeat('Y', 2));
        WeightedLetters.AddRange(Enumerable.Repeat('K', 1));
        WeightedLetters.AddRange(Enumerable.Repeat('J', 1));
        WeightedLetters.AddRange(Enumerable.Repeat('X', 1));
        WeightedLetters.AddRange(Enumerable.Repeat('Q', 1));
        WeightedLetters.AddRange(Enumerable.Repeat('Z', 1));
        // Debug.Log($"Populated WeightedLetters list with {WeightedLetters.Count} entries.");
    }

    // Fills the internal gridData array with random letters using the weighted list
    void PopulateGridData()
    {
        // Check if gridData array is initialized
        if (gridData == null)
        {
            Debug.LogError("PopulateGridData: gridData array is null! Cannot populate.", this);
            return;
        }
        // Iterate through each cell in the grid
        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                // Assign a random letter to the logical grid position
                gridData[r, c] = GetRandomLetter();
            }
        }
        // Debug.Log("Populated gridData with random letters.");
    }

    // Returns a random letter based on the weighted distribution
    char GetRandomLetter()
    {
        // Check if the weighted list is populated
        if (WeightedLetters == null || WeightedLetters.Count == 0)
        {
            Debug.LogWarning("WeightedLetters list is null or empty! Returning '?'", this);
            // Attempt to repopulate if empty (optional fallback)
            // PopulateWeightedLettersList();
            // if (WeightedLetters.Count == 0) return '?'; // Return '?' if still empty
            return '?';
        }
        // Return a random character from the list
        return WeightedLetters[Random.Range(0, WeightedLetters.Count)];
    }


    // --- Scrolling Logic ---
    public void RequestRowScroll(int rowIndex, int direction, float scrollAmount)
    {
        // Prevent scrolling if conditions not met
        if (!enabled || isAnimating || gameManager == null || gameManager.CurrentStatePublic != GameManager.GameState.Playing)
        {
            // Debug.LogWarning($"RequestRowScroll ignored: Enabled={enabled}, isAnimating={isAnimating}, State={gameManager?.CurrentState}", this);
            return;
        }
        isAnimating = true;
        // Debug.Log($"RequestRowScroll: Setting isAnimating = true. Row {rowIndex}, Dir {direction}", this);
        ShiftRowData(rowIndex, direction);
        AnimateRowScroll(rowIndex, direction, scrollAmount);
    }
    public void RequestColumnScroll(int colIndex, int direction, float scrollAmount)
    {
        // Prevent scrolling if conditions not met
        if (!enabled || isAnimating || gameManager == null || gameManager.CurrentStatePublic != GameManager.GameState.Playing)
        {
            // Debug.LogWarning($"RequestColumnScroll ignored: Enabled={enabled}, isAnimating={isAnimating}, State={gameManager?.CurrentState}", this);
            return;
        }
        isAnimating = true;
        // Debug.Log($"RequestColumnScroll: Setting isAnimating = true. Col {colIndex}, Dir {direction}", this);
        ShiftColumnData(colIndex, direction);
        AnimateColumnScroll(colIndex, direction, scrollAmount);
    }
    void ShiftRowData(int rowIndex, int direction)
    {
        // Check bounds
        if (rowIndex < 0 || rowIndex >= gridSize) { Debug.LogError($"ShiftRowData: Invalid rowIndex {rowIndex}.", this); return; }
        // Check if arrays are valid
        if (gridData == null || gridCells == null || gridCellComponents == null) { Debug.LogError("ShiftRowData: Grid arrays not initialized!", this); return; }


        if (direction == 1) // Shift Right
        {
            // Store the wrapping element's data
            char tempData = gridData[rowIndex, gridSize - 1];
            CellController tempCellController = gridCells[rowIndex, gridSize - 1]; // Get CellController
            LetterCell tempLetterCell = gridCellComponents[rowIndex, gridSize - 1]; // Get LetterCell

            // Shift elements from right to left (excluding the first column)
            for (int c = gridSize - 1; c > 0; c--)
            {
                gridData[rowIndex, c] = gridData[rowIndex, c - 1];
                gridCells[rowIndex, c] = gridCells[rowIndex, c - 1]; // Shift CellController
                gridCellComponents[rowIndex, c] = gridCellComponents[rowIndex, c - 1]; // Shift LetterCell
            }
            // Place the wrapped element at the beginning
            gridData[rowIndex, 0] = tempData;
            gridCells[rowIndex, 0] = tempCellController; // Assign wrapped CellController
            gridCellComponents[rowIndex, 0] = tempLetterCell; // Assign wrapped LetterCell
        }
        else // Shift Left (direction assumed -1 or other non-1 value)
        {
            // Store the wrapping element's data
            char tempData = gridData[rowIndex, 0];
            CellController tempCellController = gridCells[rowIndex, 0];
            LetterCell tempLetterCell = gridCellComponents[rowIndex, 0];

            // Shift elements from left to right (excluding the last column)
            for (int c = 0; c < gridSize - 1; c++)
            {
                gridData[rowIndex, c] = gridData[rowIndex, c + 1];
                gridCells[rowIndex, c] = gridCells[rowIndex, c + 1];
                gridCellComponents[rowIndex, c] = gridCellComponents[rowIndex, c + 1];
            }
            // Place the wrapped element at the end
            gridData[rowIndex, gridSize - 1] = tempData;
            gridCells[rowIndex, gridSize - 1] = tempCellController;
            gridCellComponents[rowIndex, gridSize - 1] = tempLetterCell;
        }
        // Debug.Log($"Shifted Row {rowIndex} data direction {direction}");
    }
    void ShiftColumnData(int colIndex, int direction)
    {
        // Check bounds
        if (colIndex < 0 || colIndex >= gridSize) { Debug.LogError($"ShiftColumnData: Invalid colIndex {colIndex}.", this); return; }
        // Check if arrays are valid
        if (gridData == null || gridCells == null || gridCellComponents == null) { Debug.LogError("ShiftColumnData: Grid arrays not initialized!", this); return; }


        if (direction == 1) // Shift Down
        {
            // Store the wrapping element's data (from the bottom row)
            char tempData = gridData[gridSize - 1, colIndex];
            CellController tempCellController = gridCells[gridSize - 1, colIndex];
            LetterCell tempLetterCell = gridCellComponents[gridSize - 1, colIndex];

            // Shift elements from bottom up (excluding the top row)
            for (int r = gridSize - 1; r > 0; r--)
            {
                gridData[r, colIndex] = gridData[r - 1, colIndex];
                gridCells[r, colIndex] = gridCells[r - 1, colIndex];
                gridCellComponents[r, colIndex] = gridCellComponents[r - 1, colIndex];
            }
            // Place the wrapped element at the top
            gridData[0, colIndex] = tempData;
            gridCells[0, colIndex] = tempCellController;
            gridCellComponents[0, colIndex] = tempLetterCell;
        }
        else // Shift Up (direction assumed -1 or other non-1 value)
        {
            // Store the wrapping element's data (from the top row)
            char tempData = gridData[0, colIndex];
            CellController tempCellController = gridCells[0, colIndex];
            LetterCell tempLetterCell = gridCellComponents[0, colIndex];

            // Shift elements from top down (excluding the bottom row)
            for (int r = 0; r < gridSize - 1; r++)
            {
                gridData[r, colIndex] = gridData[r + 1, colIndex];
                gridCells[r, colIndex] = gridCells[r + 1, colIndex];
                gridCellComponents[r, colIndex] = gridCellComponents[r + 1, colIndex];
            }
            // Place the wrapped element at the bottom
            gridData[gridSize - 1, colIndex] = tempData;
            gridCells[gridSize - 1, colIndex] = tempCellController;
            gridCellComponents[gridSize - 1, colIndex] = tempLetterCell;
        }
        // Debug.Log($"Shifted Column {colIndex} data direction {direction}");
    }
    void AnimateRowScroll(int rowIndex, int direction, float scrollAmount)
    {
        // Debug.Log($"AnimateRowScroll START. Row {rowIndex}. isAnimating should be true.", this);
        float totalMoveDistance = gridSize * (cellSize + spacing); // Total width/height of the grid including spacing
        float singleCellMove = direction * (cellSize + spacing); // Distance one cell moves
        Sequence seq = DOTween.Sequence(); // Create a DOTween sequence to manage animations together

        // Check if gridCells is valid and rowIndex is within bounds
        if (gridCells == null || rowIndex < 0 || rowIndex >= gridCells.GetLength(0))
        {
            Debug.LogError($"AnimateRowScroll Error: gridCells null or rowIndex {rowIndex} out of bounds.", this);
            ResetAnimationFlag("RowScroll Error"); return; // Exit if invalid
        }

        // Animate each cell in the specified row
        for (int c = 0; c < gridSize; c++)
        {
            // Check if the column index is valid and the cell controller exists
            if (c < 0 || c >= gridCells.GetLength(1) || gridCells[rowIndex, c] == null)
            {
                // Debug.LogWarning($"AnimateRowScroll Skipping animation for cell [{rowIndex},{c}] - Controller null or index out of bounds.", this);
                continue; // Skip this cell if invalid
            }
            RectTransform cellRect = gridCells[rowIndex, c].RectTransform; // Get RectTransform from CellController
            Vector2 startPos = cellRect.anchoredPosition; // Get current position
            // Add a horizontal move animation to the sequence
            seq.Join(cellRect.DOAnchorPosX(startPos.x + singleCellMove, 0.3f).SetEase(Ease.OutCubic));
        }

        // Handle the wrapping cell
        int wrapIndex = (direction == 1) ? 0 : gridSize - 1; // Index of the cell that wraps around
        // Check if wrapIndex is valid and the wrapping cell controller exists
        if (wrapIndex < 0 || wrapIndex >= gridCells.GetLength(1) || gridCells[rowIndex, wrapIndex] == null)
        {
            // Debug.LogWarning($"AnimateRowScroll Skipping wrap logic for cell [{rowIndex},{wrapIndex}] - Controller null or index out of bounds.", this);
        }
        else
        {
            RectTransform wrapCellRect = gridCells[rowIndex, wrapIndex].RectTransform; // Get RectTransform of the wrapping cell
            Vector2 wrapStartPos = wrapCellRect.anchoredPosition; // Get its starting position
            // Use InsertCallback to reposition the wrapping cell just before the animation visually completes
            // This makes the wrap appear seamless
            seq.InsertCallback(0.29f, () => {
                // Check if wrapCellRect is still valid inside the callback
                if (wrapCellRect != null)
                {
                    // Calculate the position on the opposite side of the grid
                    wrapCellRect.anchoredPosition = new Vector2(wrapStartPos.x - direction * totalMoveDistance, wrapStartPos.y);
                }
                else { Debug.LogWarning("Wrap cell became null inside row scroll callback!", this); }
            });
        }

        // Set callbacks for sequence events
        seq.OnKill(() => { Debug.LogWarning("AnimateRowScroll Sequence KILLED.", this); ResetAnimationFlag("RowScroll Killed"); });
        seq.OnComplete(() => {
            // Debug.Log("AnimateRowScroll Sequence COMPLETE.", this);
            try { SnapToGridPositions(); } // Ensure cells are perfectly aligned after animation
            catch (System.Exception e) { Debug.LogError($"Error in SnapToGridPositions after row scroll: {e.Message}", this); }
            ResetAnimationFlag("RowScroll Complete"); // Reset animation flag
        });
    }
    void AnimateColumnScroll(int colIndex, int direction, float scrollAmount)
    {
        // Debug.Log($"AnimateColumnScroll START. Col {colIndex}. isAnimating should be true.", this);
        float totalMoveDistance = gridSize * (cellSize + spacing); // Total height of the grid including spacing
        float singleCellMove = -direction * (cellSize + spacing); // Negative because Y increases upwards in UI coordinates
        Sequence seq = DOTween.Sequence(); // Create a DOTween sequence

        // Check if gridCells is valid and colIndex is within bounds
        if (gridCells == null || colIndex < 0 || colIndex >= gridCells.GetLength(1))
        {
            Debug.LogError($"AnimateColumnScroll Error: gridCells null or colIndex {colIndex} out of bounds.", this);
            ResetAnimationFlag("ColScroll Error"); return; // Exit if invalid
        }

        // Animate each cell in the specified column
        for (int r = 0; r < gridSize; r++)
        {
            // Check if the row index is valid and the cell controller exists
            if (r < 0 || r >= gridCells.GetLength(0) || gridCells[r, colIndex] == null)
            {
                // Debug.LogWarning($"AnimateColumnScroll Skipping animation for cell [{r},{colIndex}] - Controller null or index out of bounds.", this);
                continue; // Skip this cell if invalid
            }
            RectTransform cellRect = gridCells[r, colIndex].RectTransform; // Get RectTransform from CellController
            Vector2 startPos = cellRect.anchoredPosition; // Get current position
            // Add a vertical move animation to the sequence
            seq.Join(cellRect.DOAnchorPosY(startPos.y + singleCellMove, 0.3f).SetEase(Ease.OutCubic));
        }

        // Handle the wrapping cell
        int wrapIndex = (direction == 1) ? 0 : gridSize - 1; // Index of the cell that wraps (0 for down scroll, size-1 for up scroll)
        // Check if wrapIndex is valid and the wrapping cell controller exists
        if (wrapIndex < 0 || wrapIndex >= gridCells.GetLength(0) || gridCells[wrapIndex, colIndex] == null)
        {
            // Debug.LogWarning($"AnimateColumnScroll Skipping wrap logic for cell [{wrapIndex},{colIndex}] - Controller null or index out of bounds.", this);
        }
        else
        {
            RectTransform wrapCellRect = gridCells[wrapIndex, colIndex].RectTransform; // Get RectTransform of the wrapping cell
            Vector2 wrapStartPos = wrapCellRect.anchoredPosition; // Get its starting position
            // Use InsertCallback to reposition the wrapping cell just before the animation visually completes
            seq.InsertCallback(0.29f, () => {
                // Check if wrapCellRect is still valid inside the callback
                if (wrapCellRect != null)
                {
                    // Calculate the position on the opposite side of the grid
                    wrapCellRect.anchoredPosition = new Vector2(wrapStartPos.x, wrapStartPos.y + direction * totalMoveDistance);
                }
                else { Debug.LogWarning("Wrap cell became null inside column scroll callback!", this); }
            });
        }

        // Set callbacks for sequence events
        seq.OnKill(() => { Debug.LogWarning("AnimateColumnScroll Sequence KILLED.", this); ResetAnimationFlag("ColScroll Killed"); });
        seq.OnComplete(() => {
            // Debug.Log("AnimateColumnScroll Sequence COMPLETE.", this);
            try { SnapToGridPositions(); } // Ensure cells are perfectly aligned
            catch (System.Exception e) { Debug.LogError($"Error in SnapToGridPositions after column scroll: {e.Message}", this); }
            ResetAnimationFlag("ColScroll Complete"); // Reset animation flag
        });
    }


    // --- Utility Methods ---
    private void ResetAnimationFlag(string reason)
    {
        // Only change the flag and log if it was actually true
        if (isAnimating)
        {
            isAnimating = false;
            // Debug.Log($"ResetAnimationFlag: Setting isAnimating = false. Reason: {reason}", this);
        }
    }
    void SnapToGridPositions()
    {
        // Check if gridCells array is initialized
        if (gridCells == null) { Debug.LogError("SnapToGridPositions: gridCells array is null. Cannot snap positions.", this); return; }

        // Recalculate grid dimensions and starting offset (same as in InitializeGrid)
        float totalGridSizeUI = gridSize * cellSize + (gridSize - 1) * spacing;
        float startOffset = -totalGridSizeUI / 2f + cellSize / 2f;

        // Iterate through each cell
        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                // Check if the cell controller exists
                if (gridCells[r, c] != null)
                {
                    // Calculate the exact target position for this cell
                    float targetX = startOffset + c * (cellSize + spacing);
                    float targetY = startOffset + (gridSize - 1 - r) * (cellSize + spacing); // Y calculation depends on grid origin
                    // Get the RectTransform via the CellController
                    RectTransform cellRect = gridCells[r, c].RectTransform;
                    // Directly set the anchoredPosition to the calculated target
                    if (cellRect != null)
                    {
                        cellRect.anchoredPosition = new Vector2(targetX, targetY);
                    }
                    else
                    {
                        Debug.LogWarning($"SnapToGridPositions: RectTransform null for cell [{r},{c}].", this);
                    }
                }
            }
        }
        // Debug.Log("SnapToGridPositions completed.");
    }


    // --- Letter Replacement Logic ---
    // Internal helper method - potentially used for single replacements or testing
    void ReplaceLetter(int row, int col)
    {
        // Bounds check
        if (row < 0 || row >= gridSize || col < 0 || col >= gridSize) { Debug.LogWarning($"ReplaceLetter: Invalid coordinates ({row}, {col}).", this); return; }
        // Check if gridData is valid
        if (gridData == null) { Debug.LogError("ReplaceLetter: gridData is null!", this); return; }

        gridData[row, col] = GetRandomLetter(); // Update logical data

        CellController cellController = GetCellController(new Vector2Int(row, col)); // Get the controller

        if (cellController != null)
        {
            cellController.SetLetter(gridData[row, col]); // Update visual letter

            // Optional: Add a visual effect like a punch or fade-in/out
            // Check current scale to decide animation (e.g., if called after FadeOut)
            if (cellController.RectTransform.localScale == Vector3.zero)
            {
                // If scale is zero (likely after a fade out), scale it back in
                cellController.RectTransform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
            }
            else
            {
                // If already visible, maybe just a quick punch effect
                cellController.RectTransform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 10, 1);
            }
            cellController.SetAlpha(1f); // Ensure it's fully opaque
        }
        else { Debug.LogWarning($"Cannot update visual for replaced letter at [{row},{col}] - CellController not found.", this); }
    }

    // Public method called by GameManager to replace multiple letters
    public void ReplaceLettersAt(List<Vector2Int> coordinates, bool fadeIn = false)
    {
        // Check if list is valid
        if (coordinates == null || coordinates.Count == 0)
        {
            // Debug.Log("ReplaceLettersAt: Coordinate list is null or empty.");
            return;
        }
        // Check if gridData is valid
        if (gridData == null) { Debug.LogError("ReplaceLettersAt: gridData is null!", this); return; }

        // Iterate through each coordinate in the provided list
        foreach (Vector2Int coord in coordinates)
        {
            // Check if the coordinate is within the grid bounds
            if (coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
            {
                // Get a new random letter
                char newLetter = GetRandomLetter();
                // Update the logical grid data
                gridData[coord.x, coord.y] = newLetter;
                // Get the corresponding CellController
                CellController cellController = GetCellController(coord);
                if (cellController != null)
                {
                    // Update the visual letter on the CellController
                    cellController.SetLetter(newLetter);
                    // Handle visual appearance (fade-in or instant)
                    if (fadeIn)
                    {
                        // Delegate the fade-in animation to the CellController
                        cellController.FadeIn(cellFadeInDuration);
                    }
                    else
                    {
                        // Make the cell instantly visible
                        cellController.SetAlpha(1f);
                    }
                }
                else { Debug.LogWarning($"ReplaceLettersAt: Cannot find CellController at {coord}"); }
            }
            else { Debug.LogWarning($"ReplaceLettersAt: Skipping invalid coordinate {coord}"); }
        }
        // Debug.Log($"Replaced letters at {coordinates.Count} coordinates. FadeIn: {fadeIn}");
    }


    // --- Public Method to Trigger Validation ---
    /// <summary>
    /// Triggers the WordValidator to check the grid and passes the results to GameManager.
    /// Only proceeds if the game is playing and no animations are active.
    /// </summary>
    public void TriggerValidationCheck() // <<< FINAL VERSION >>>
    {
        // Check prerequisites: GameManager and WordValidator must exist
        if (gameManager == null || wordValidator == null) { Debug.LogError("WGM: Cannot TriggerValidationCheck - missing refs (GameManager or WordValidator)!", this); return; }
        // Check game state: Must be 'Playing'
        if (gameManager.CurrentStatePublic != GameManager.GameState.Playing)
        {
            // Debug.Log("TriggerValidationCheck skipped: Game not in Playing state.");
            return;
        }
        // Check animation state: No major animations should be running
        if (gameManager.IsAnyAnimationPlaying)
        {
            // Debug.Log("TriggerValidationCheck skipped: An animation is currently playing.");
            return;
        }

        // Debug.Log("WordGridManager: Triggering validation and handling results.");

        // Call the validator to get the list of found words for this pass
        List<FoundWordData> foundWords = wordValidator.ValidateWords();

        // Pass the results (list of found words) to the GameManager
        // GameManager is responsible for processing this list sequentially
        gameManager.HandleValidationResult(foundWords);
    }


    // --- Method to apply move reduction after grid settles ---
    /// <summary>
    /// Applies the move reduction logic to the specified row or column.
    /// Called by GridInputHandler from Update when pendingMoveReduction is true and grid is not animating.
    /// Also triggers the GameManager's move decrement.
    /// </summary>
    /// <param name="row">The row index that was scrolled (-1 if a column was scrolled).</param>
    /// <param name="col">The column index that was scrolled (-1 if a column was scrolled).</param>
    public void ApplyPendingMoveReduction(int row, int col)
    {
        // Validate input: exactly one of row or col should be valid index
        if (row >= 0 && col >= 0) { Debug.LogError($"ApplyPendingMoveReduction called with both valid row ({row}) and col ({col})!", this); return; }
        if (row < 0 && col < 0) { Debug.LogWarning($"ApplyPendingMoveReduction called with invalid row ({row}) and col ({col}).", this); return; }

        bool wasRowScroll = row >= 0; // Determine if it was a row or column scroll
        int index = wasRowScroll ? row : col; // Get the index of the scrolled line
        string type = wasRowScroll ? "row" : "column"; // Get the type for logging

        // --- Trigger GameManager Move Decrement ---
        if (gameManager != null)
        {
            // Debug.Log($"ApplyPendingMoveReduction: Triggering move decrement for completed scroll of {type} {index}.");
            gameManager.DecrementMoves(); // Tell GameManager a move was completed
        }
        else
        {
            Debug.LogError("ApplyPendingMoveReduction: Cannot decrement moves, GameManager reference is missing!", this);
        }


        // --- Original LetterCell Move Reduction Logic (Optional based on LetterCell settings) ---
        // This part interacts with the optional LetterCell component if it exists and has moves enabled
        // Debug.Log($"ApplyPendingMoveReduction: Applying LetterCell moves reduction (if enabled on cells) for {type} {index}");
        bool anyCellMovesReduced = false; // Flag to track if any LetterCell moves were reduced

        if (wasRowScroll) // Reduce moves for LetterCells in the scrolled Row
        {
            for (int c = 0; c < gridSize; c++)
            {
                LetterCell cell = GetLetterCellAt(row, c); // Get the LetterCell component
                // Check if the cell exists, has moves enabled, and successfully reduces a move
                if (cell != null && cell.EnableMoves && cell.ReduceMove())
                {
                    anyCellMovesReduced = true; // Mark that at least one move was reduced
                }
            }
        }
        else // Reduce moves for LetterCells in the scrolled Column
        {
            for (int r = 0; r < gridSize; r++)
            {
                LetterCell cell = GetLetterCellAt(r, col); // Get the LetterCell component
                // Check if the cell exists, has moves enabled, and successfully reduces a move
                if (cell != null && cell.EnableMoves && cell.ReduceMove())
                {
                    anyCellMovesReduced = true; // Mark that at least one move was reduced
                }
            }
        }

        // Optional logging based on whether any LetterCell moves were affected
        // if (anyCellMovesReduced) { Debug.Log($"ApplyPendingMoveReduction: Finished reducing LetterCell moves for {type} {index}."); }
        // else { Debug.Log($"ApplyPendingMoveReduction: No LetterCell moves were reduced for {type} {index} (or cells have moves disabled)."); }
    }

    // --- Public method to get LetterCell component ---
    /// <summary>
    /// Gets the LetterCell component at the specified grid coordinate.
    /// Returns null if out of bounds or if the cell prefab doesn't have a LetterCell component.
    /// </summary>
    public LetterCell GetLetterCellAt(int row, int col)
    {
        // Check if the gridCellComponents array is initialized and coordinates are within bounds
        if (gridCellComponents == null || row < 0 || row >= gridCellComponents.GetLength(0) || col < 0 || col >= gridCellComponents.GetLength(1))
        {
            // Debug.LogWarning($"GetLetterCellAt: Coordinates ({row},{col}) out of bounds or array not initialized.");
            return null;
        }
        // Return the stored LetterCell component (which might be null if the prefab didn't have one)
        return gridCellComponents[row, col];
    }

    // --- Public method to get CellController component ---
    /// <summary>
    /// Gets the CellController at the specified grid coordinate.
    /// Returns null if out of bounds or if the cell controller is missing.
    /// </summary>
    public CellController GetCellController(Vector2Int coord)
    {
        // Check if the gridCells array is initialized and coordinates are within bounds
        if (gridCells != null && coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
        {
            // Return the stored CellController component
            return gridCells[coord.x, coord.y];
        }
        // Log warning if coordinates are invalid or array is null
        // Debug.LogWarning($"WGM: GetCellController - Coordinate out of bounds or gridCells null: {coord}");
        return null;
    }

} // End of WordGridManager class