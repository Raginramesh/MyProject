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
                        // Kill any DOTween animations targeting this cell's RectTransform
                        // Prevents callbacks from firing after the object is destroyed
                        DOTween.Kill(gridCellRects[r, c]);

                        // Destroy the GameObject associated with the cell
                        if (gridCellRects[r, c].gameObject != null)
                        {
                            Destroy(gridCellRects[r, c].gameObject);
                        }
                    }
                }
            }
        } // else: First initialization, no cleanup needed

        // --- Initialize Data Structures ---
        // Create new arrays based on the current gridSize setting
        gridData = new char[gridSize, gridSize];
        gridCellTextComponents = new TextMeshProUGUI[gridSize, gridSize];
        gridCellRects = new RectTransform[gridSize, gridSize];

        // --- Populate Logical Grid ---
        PopulateGridData(); // Fill gridData with random letters

        // --- Create Visual Grid ---
        // Calculate layout parameters
        float totalGridSizeUI = gridSize * cellSize + (gridSize - 1) * spacing; // Total width/height of the grid UI
        float startOffset = -totalGridSizeUI / 2f + cellSize / 2f; // Starting position offset for centering
        int cellsCreated = 0; // Debug counter

        // Loop through grid positions to instantiate cells
        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                // Instantiate the prefab as a child of the designated parent
                GameObject cellGO = Instantiate(letterCellPrefab, gridParent);
                cellsCreated++;

                // Get necessary components from the instantiated object
                RectTransform cellRect = cellGO.GetComponent<RectTransform>();
                // Find TextMeshPro component in children (more robust than GetComponent)
                // 'true' includes inactive components, just in case
                TextMeshProUGUI cellText = cellGO.GetComponentInChildren<TextMeshProUGUI>(true);

                // --- Sanity Checks for Prefab Structure ---
                if (cellRect == null)
                {
                    Debug.LogError($"Instantiated Prefab '{letterCellPrefab.name}' is missing RectTransform component! Check the prefab root.", cellGO);
                    Destroy(cellGO); continue; // Skip this cell if invalid
                }
                if (cellText == null)
                {
                    // Log error, but might still be usable visually depending on design
                    Debug.LogError($"Instantiated Prefab '{letterCellPrefab.name}' does not have a TextMeshProUGUI component in its children! Check the prefab structure.", cellGO);
                }

                // Calculate and apply position based on row and column
                float posX = startOffset + c * (cellSize + spacing);
                float posY = startOffset + (gridSize - 1 - r) * (cellSize + spacing); // Invert Y for UI coordinates

                cellRect.anchoredPosition = new Vector2(posX, posY);
                cellRect.sizeDelta = new Vector2(cellSize, cellSize); // Set cell size
                cellRect.localScale = Vector3.one; // Ensure scale is not zero

                // Set the displayed letter if Text component exists
                if (cellText != null)
                {
                    cellText.text = gridData[r, c].ToString();
                    // Note: Text auto-sizing should be enabled on the prefab itself
                }
                else
                {
                    // Rename for easier identification in Hierarchy if text is missing
                    cellGO.name = $"Cell_{r}_{c}_(NoText)";
                }

                // Store references to the components for later access
                gridCellTextComponents[r, c] = cellText;
                gridCellRects[r, c] = cellRect;
            }
        }

        // --- End of Initialization ---
        Debug.Log($"WordGridManager: Grid Initialized. Attempted to create {cellsCreated} cells. isAnimating = {isAnimating}", this);
        // Sanity check for cell count
        if (cellsCreated != gridSize * gridSize)
        {
            Debug.LogWarning($"Expected {gridSize * gridSize} cells, but only reported {cellsCreated} created. Some might have failed instantiation checks.", this);
        }
        // Initial word validation is now typically called by GameManager after this method completes
    }

    // Fills the WeightedLetters list based on standard English letter frequencies
    void PopulateWeightedLettersList()
    {
        WeightedLetters.Clear(); // Ensure list is empty before adding
        // Adjust counts as needed for desired gameplay balance
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
        // Debug.Log($"Populated WeightedLetters list with {WeightedLetters.Count} entries.");
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
        if (WeightedLetters.Count == 0)
        {
            Debug.LogWarning("WeightedLetters list is empty! Returning '?'.", this);
            return '?'; // Fallback character
        }
        // Select a random index from the weighted list
        return WeightedLetters[Random.Range(0, WeightedLetters.Count)];
    }


    // --- Scrolling Logic (Called by GridInputHandler) ---

    // Public method to initiate a row scroll
    public void RequestRowScroll(int rowIndex, int direction, float scrollAmount)
    {
        // Prevent scrolling if already animating, not playing, or component disabled
        if (!enabled || isAnimating || gameManager == null || gameManager.CurrentState != GameManager.GameState.Playing)
        {
            Debug.LogWarning($"RequestRowScroll ignored: Enabled={enabled}, isAnimating={isAnimating}, State={gameManager?.CurrentState}", this);
            return;
        }

        // --- Begin Scroll Action ---
        isAnimating = true; // Block further input
        Debug.Log($"RequestRowScroll: Setting isAnimating = true. Row {rowIndex}, Dir {direction}", this);

        // Update game state (e.g., decrement moves)
        if (gameManager != null) gameManager.DecrementMoves();

        // Update the logical grid data first
        ShiftRowData(rowIndex, direction);

        // Start the visual animation
        AnimateRowScroll(rowIndex, direction, scrollAmount);
    }

    // Public method to initiate a column scroll
    public void RequestColumnScroll(int colIndex, int direction, float scrollAmount)
    {
        // Prevent scrolling if already animating, not playing, or component disabled
        if (!enabled || isAnimating || gameManager == null || gameManager.CurrentState != GameManager.GameState.Playing)
        {
            Debug.LogWarning($"RequestColumnScroll ignored: Enabled={enabled}, isAnimating={isAnimating}, State={gameManager?.CurrentState}", this);
            return;
        }

        // --- Begin Scroll Action ---
        isAnimating = true; // Block further input
        Debug.Log($"RequestColumnScroll: Setting isAnimating = true. Col {colIndex}, Dir {direction}", this);

        // Update game state
        if (gameManager != null) gameManager.DecrementMoves();

        // Update the logical grid data
        ShiftColumnData(colIndex, direction);

        // Start the visual animation
        AnimateColumnScroll(colIndex, direction, scrollAmount);
    }

    // --- Internal Data Shifting (Logical Grid Update) ---

    // Shifts the data and references in gridData, gridCellRects, gridCellTextComponents for a row
    void ShiftRowData(int rowIndex, int direction)
    {
        // Boundary checks on row index (robustness)
        if (rowIndex < 0 || rowIndex >= gridSize)
        {
            Debug.LogError($"ShiftRowData: Invalid rowIndex {rowIndex}.", this); return;
        }

        if (direction == 1)
        { // Shift Right (->)
            // Store the last element that will wrap around
            char tempData = gridData[rowIndex, gridSize - 1];
            RectTransform tempRect = gridCellRects[rowIndex, gridSize - 1];
            TextMeshProUGUI tempText = gridCellTextComponents[rowIndex, gridSize - 1];
            // Shift elements from left-to-right
            for (int c = gridSize - 1; c > 0; c--)
            {
                gridData[rowIndex, c] = gridData[rowIndex, c - 1];
                gridCellRects[rowIndex, c] = gridCellRects[rowIndex, c - 1];
                gridCellTextComponents[rowIndex, c] = gridCellTextComponents[rowIndex, c - 1];
            }
            // Place the stored element at the beginning
            gridData[rowIndex, 0] = tempData;
            gridCellRects[rowIndex, 0] = tempRect;
            gridCellTextComponents[rowIndex, 0] = tempText;
        }
        else
        { // Shift Left (<-) (direction == -1)
            // Store the first element that will wrap around
            char tempData = gridData[rowIndex, 0];
            RectTransform tempRect = gridCellRects[rowIndex, 0];
            TextMeshProUGUI tempText = gridCellTextComponents[rowIndex, 0];
            // Shift elements from right-to-left
            for (int c = 0; c < gridSize - 1; c++)
            {
                gridData[rowIndex, c] = gridData[rowIndex, c + 1];
                gridCellRects[rowIndex, c] = gridCellRects[rowIndex, c + 1];
                gridCellTextComponents[rowIndex, c] = gridCellTextComponents[rowIndex, c + 1];
            }
            // Place the stored element at the end
            gridData[rowIndex, gridSize - 1] = tempData;
            gridCellRects[rowIndex, gridSize - 1] = tempRect;
            gridCellTextComponents[rowIndex, gridSize - 1] = tempText;
        }
        // Debug.Log($"Shifted Row {rowIndex} Data, Direction {direction}"); // Optional log
    }

    // Shifts the data and references for a column
    void ShiftColumnData(int colIndex, int direction)
    {
        // Boundary checks on column index
        if (colIndex < 0 || colIndex >= gridSize)
        {
            Debug.LogError($"ShiftColumnData: Invalid colIndex {colIndex}.", this); return;
        }

        if (direction == 1)
        { // Shift Down (v)
            // Store the bottom element
            char tempData = gridData[gridSize - 1, colIndex];
            RectTransform tempRect = gridCellRects[gridSize - 1, colIndex];
            TextMeshProUGUI tempText = gridCellTextComponents[gridSize - 1, colIndex];
            // Shift elements from top-to-bottom
            for (int r = gridSize - 1; r > 0; r--)
            {
                gridData[r, colIndex] = gridData[r - 1, colIndex];
                gridCellRects[r, colIndex] = gridCellRects[r - 1, colIndex];
                gridCellTextComponents[r, colIndex] = gridCellTextComponents[r - 1, colIndex];
            }
            // Place stored element at the top
            gridData[0, colIndex] = tempData;
            gridCellRects[0, colIndex] = tempRect;
            gridCellTextComponents[0, colIndex] = tempText;
        }
        else
        { // Shift Up (^) (direction == -1)
            // Store the top element
            char tempData = gridData[0, colIndex];
            RectTransform tempRect = gridCellRects[0, colIndex];
            TextMeshProUGUI tempText = gridCellTextComponents[0, colIndex];
            // Shift elements from bottom-to-top
            for (int r = 0; r < gridSize - 1; r++)
            {
                gridData[r, colIndex] = gridData[r + 1, colIndex];
                gridCellRects[r, colIndex] = gridCellRects[r + 1, colIndex];
                gridCellTextComponents[r, colIndex] = gridCellTextComponents[r + 1, colIndex];
            }
            // Place stored element at the bottom
            gridData[gridSize - 1, colIndex] = tempData;
            gridCellRects[gridSize - 1, colIndex] = tempRect;
            gridCellTextComponents[gridSize - 1, colIndex] = tempText;
        }
        // Debug.Log($"Shifted Column {colIndex} Data, Direction {direction}"); // Optional log
    }


    // --- Visual Animation Logic (Using DOTween) ---

    // Animates the visual movement of cells in a row
    void AnimateRowScroll(int rowIndex, int direction, float scrollAmount)
    {
        Debug.Log($"AnimateRowScroll START. Row {rowIndex}. isAnimating should be true.", this);
        // Calculate movement distances
        float totalMoveDistance = gridSize * (cellSize + spacing); // For wrap-around
        float singleCellMove = direction * (cellSize + spacing); // Distance for one step

        // Create a DOTween Sequence for coordinated animation
        Sequence seq = DOTween.Sequence();

        // Safety check for row index and array existence
        if (gridCellRects == null || rowIndex < 0 || rowIndex >= gridCellRects.GetLength(0))
        {
            Debug.LogError($"AnimateRowScroll Error: gridCellRects is null or rowIndex {rowIndex} is out of bounds.", this);
            ResetAnimationFlag("RowScroll Error"); // Reset flag if animation cannot proceed
            return;
        }

        // Animate each cell in the specified row
        for (int c = 0; c < gridSize; c++)
        {
            // Safety check for column index and individual cell reference
            if (c < 0 || c >= gridCellRects.GetLength(1) || gridCellRects[rowIndex, c] == null)
            {
                Debug.LogWarning($"AnimateRowScroll Skipping animation for cell [{rowIndex},{c}] - RectTransform is null or index out of bounds.", this);
                continue;
            }
            RectTransform cellRect = gridCellRects[rowIndex, c];
            Vector2 startPos = cellRect.anchoredPosition;
            // Add movement tween to the sequence. 'Join' allows tweens to run concurrently.
            seq.Join(cellRect.DOAnchorPosX(startPos.x + singleCellMove, 0.3f).SetEase(Ease.OutCubic));
        }

        // Handle the visual wrap-around for the cell that moved off-screen logically
        int wrapIndex = (direction == 1) ? 0 : gridSize - 1; // Logical index of the cell that wrapped
        // Safety check for wrap index and cell reference
        if (wrapIndex < 0 || wrapIndex >= gridCellRects.GetLength(1) || gridCellRects[rowIndex, wrapIndex] == null)
        {
            Debug.LogWarning($"AnimateRowScroll Skipping wrap logic for cell [{rowIndex},{wrapIndex}] - RectTransform is null or index out of bounds.", this);
        }
        else
        {
            RectTransform wrapCell = gridCellRects[rowIndex, wrapIndex];
            Vector2 wrapStartPos = wrapCell.anchoredPosition;
            // Use InsertCallback to instantly move the wrapping cell just before the main animation ends visually
            seq.InsertCallback(0.29f, () => {
                // Re-check reference inside callback as object might be destroyed
                if (wrapCell != null)
                {
                    // Move it off-screen on the opposite side, ready to slide in
                    wrapCell.anchoredPosition = new Vector2(wrapStartPos.x - direction * totalMoveDistance, wrapStartPos.y);
                }
                else { Debug.LogWarning("Wrap cell became null inside row scroll callback!", this); }
            });
        }

        // --- Define Sequence Callbacks ---

        // OnKill: Called if the sequence is interrupted (e.g., by DOTween.Kill)
        seq.OnKill(() => {
            Debug.LogWarning("AnimateRowScroll Sequence KILLED.", this);
            // Ensure the animation flag is reset even if killed
            ResetAnimationFlag("RowScroll Killed");
            // Optionally snap to final positions immediately on kill?
            // SnapToGridPositions();
        });

        // OnComplete: Called when the sequence finishes normally
        seq.OnComplete(() => {
            Debug.Log("AnimateRowScroll Sequence COMPLETE.", this);
            // Snap all cells to their exact calculated grid positions to correct minor tween inaccuracies
            try
            {
                SnapToGridPositions();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error during SnapToGridPositions after row scroll: {e.Message}\n{e.StackTrace}", this);
            }

            // Reset the animation flag AFTER snapping is done
            ResetAnimationFlag("RowScroll Complete");

            // --- VALIDATION CALL REMOVED FROM HERE ---
            // Validation will now be triggered by GridInputHandler when the grid settles after a drag ends.
        });
    }

    // Animates the visual movement of cells in a column
    void AnimateColumnScroll(int colIndex, int direction, float scrollAmount)
    {
        Debug.Log($"AnimateColumnScroll START. Col {colIndex}. isAnimating should be true.", this);
        // Calculate movement distances
        float totalMoveDistance = gridSize * (cellSize + spacing);
        float singleCellMove = -direction * (cellSize + spacing); // Inverted Y for UI

        // Create sequence
        Sequence seq = DOTween.Sequence();

        // Safety check for column index and array existence
        if (gridCellRects == null || colIndex < 0 || colIndex >= gridCellRects.GetLength(1))
        {
            Debug.LogError($"AnimateColumnScroll Error: gridCellRects is null or colIndex {colIndex} is out of bounds.", this);
            ResetAnimationFlag("ColScroll Error");
            return;
        }

        // Animate each cell in the column
        for (int r = 0; r < gridSize; r++)
        {
            // Safety check for row index and cell reference
            if (r < 0 || r >= gridCellRects.GetLength(0) || gridCellRects[r, colIndex] == null)
            {
                Debug.LogWarning($"AnimateColumnScroll Skipping animation for cell [{r},{colIndex}] - RectTransform is null or index out of bounds.", this);
                continue;
            }
            RectTransform cellRect = gridCellRects[r, colIndex];
            Vector2 startPos = cellRect.anchoredPosition;
            // Add vertical movement tween
            seq.Join(cellRect.DOAnchorPosY(startPos.y + singleCellMove, 0.3f).SetEase(Ease.OutCubic));
        }

        // Handle visual wrap-around
        int wrapIndex = (direction == 1) ? 0 : gridSize - 1; // Logical index of the wrapped cell
        // Safety check for wrap index and cell reference
        if (wrapIndex < 0 || wrapIndex >= gridCellRects.GetLength(0) || gridCellRects[wrapIndex, colIndex] == null)
        {
            Debug.LogWarning($"AnimateColumnScroll Skipping wrap logic for cell [{wrapIndex},{colIndex}] - RectTransform is null or index out of bounds.", this);
        }
        else
        {
            RectTransform wrapCell = gridCellRects[wrapIndex, colIndex];
            Vector2 wrapStartPos = wrapCell.anchoredPosition;
            // Insert callback to instantly move the wrapping cell
            seq.InsertCallback(0.29f, () => {
                // Re-check reference inside callback
                if (wrapCell != null)
                {
                    // Move it off-screen vertically
                    wrapCell.anchoredPosition = new Vector2(wrapStartPos.x, wrapStartPos.y + direction * totalMoveDistance); // Note: Y calculation direction adjusted
                }
                else { Debug.LogWarning("Wrap cell became null inside column scroll callback!", this); }
            });
        }

        // --- Define Sequence Callbacks ---

        seq.OnKill(() => {
            Debug.LogWarning("AnimateColumnScroll Sequence KILLED.", this);
            ResetAnimationFlag("ColScroll Killed");
            // SnapToGridPositions(); // Optional immediate snap
        });

        seq.OnComplete(() => {
            Debug.Log("AnimateColumnScroll Sequence COMPLETE.", this);
            // Snap cells to final positions
            try
            {
                SnapToGridPositions();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error during SnapToGridPositions after column scroll: {e.Message}\n{e.StackTrace}", this);
            }

            // Reset animation flag
            ResetAnimationFlag("ColScroll Complete");

            // --- VALIDATION CALL REMOVED FROM HERE ---
            // Validation will now be triggered by GridInputHandler when the grid settles after a drag ends.
        });
    }

    // --- Utility Methods ---

    // Helper method to consistently reset the animation flag and log the reason
    private void ResetAnimationFlag(string reason)
    {
        // Only log if the state actually changed, reduces console noise
        if (isAnimating)
        {
            isAnimating = false;
            Debug.Log($"ResetAnimationFlag: Setting isAnimating = false. Reason: {reason}", this);
        }
        // Optional: Log even if it was already false for deeper debugging
        // else { Debug.Log($"ResetAnimationFlag called but isAnimating was already false. Reason: {reason}", this); }
    }

    // Instantly sets the anchoredPosition of all cells to their calculated correct grid locations
    // Useful after animations to correct any floating-point inaccuracies
    void SnapToGridPositions()
    {
        // Safety check for the main array
        if (gridCellRects == null)
        {
            Debug.LogError("SnapToGridPositions cannot run: gridCellRects array is null.", this);
            return;
        }
        // Debug.Log("Snapping cells to final grid positions.", this); // Optional log

        // Recalculate layout parameters (in case grid size/spacing changed dynamically, though unlikely here)
        float totalGridSizeUI = gridSize * cellSize + (gridSize - 1) * spacing;
        float startOffset = -totalGridSizeUI / 2f + cellSize / 2f;

        // Loop through all grid positions
        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                // Safety check for individual cell reference
                if (gridCellRects[r, c] != null)
                {
                    // Calculate the exact target position
                    float targetX = startOffset + c * (cellSize + spacing);
                    float targetY = startOffset + (gridSize - 1 - r) * (cellSize + spacing); // Inverted Y
                    // Set the position directly
                    gridCellRects[r, c].anchoredPosition = new Vector2(targetX, targetY);
                }
                // else { Debug.LogWarning($"SnapToGridPositions: Cell [{r},{c}] RectTransform reference is null in array.", this); } // Optional warning
            }
        }
        // Debug.Log("Finished snapping cells.", this); // Optional log
    }


    // --- Letter Replacement Logic ---

    // Replaces the letter in a SINGLE specified cell (logical and visual)
    // This is now primarily used internally by ReplaceLettersAt
    public void ReplaceLetter(int row, int col)
    {
        // --- Bounds Check ---
        if (row < 0 || row >= gridSize || col < 0 || col >= gridSize)
        {
            Debug.LogWarning($"ReplaceLetter: Invalid coordinates ({row}, {col}). Cannot replace.", this);
            return;
        }

        // --- Update Logical Grid ---
        gridData[row, col] = GetRandomLetter(); // Get a new random letter

        // --- Update Visual Grid ---
        // Check if text component array and specific component exist
        if (gridCellTextComponents != null && row < gridCellTextComponents.GetLength(0) && col < gridCellTextComponents.GetLength(1) &&
            gridCellTextComponents[row, col] != null)
        {
            // Update the text display
            gridCellTextComponents[row, col].text = gridData[row, col].ToString();

            // --- Visual Feedback for New Letter ---
            // Get the RectTransform for animation (with safety checks)
            RectTransform cellRect = (gridCellRects != null && row < gridCellRects.GetLength(0) && col < gridCellRects.GetLength(1)) ? gridCellRects[row, col] : null;
            if (cellRect != null)
            {
                // Example: Pop-in animation using DOTween
                cellRect.localScale = Vector3.zero; // Start scaled down (invisible)
                // Animate scale back to 1 with an overshoot effect (Ease.OutBack)
                cellRect.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
            }

        }
        else
        {
            // Log warning if the text component couldn't be updated
            Debug.LogWarning($"Cannot update visual text for replaced letter at [{row},{col}], TextMeshProUGUI component missing or array invalid.", this);
        }
    }

    // --- Replaces letters at MULTIPLE specified coordinates ---
    // Called by WordValidator after finding valid words
    public void ReplaceLettersAt(List<Vector2Int> coordinates)
    {
        // Check if the input list is valid
        if (coordinates == null || coordinates.Count == 0)
        {
            Debug.Log("ReplaceLettersAt called with empty or null coordinate list. No action taken.", this);
            return;
        }

        Debug.Log($"Replacing letters at {coordinates.Count} coordinates.", this);

        // --- Optional: Block Input During Replacement Animation ---
        // If the replacement involves significant animation, you might want to set isAnimating = true here
        // bool wasAnimating = isAnimating; // Store previous state if needed
        // isAnimating = true;

        // --- Visual Effect Sequence (Optional) ---
        // Example: Make cells scale down before replacement
        Sequence effectSequence = DOTween.Sequence();
        foreach (Vector2Int coord in coordinates)
        {
            // Bounds check coordinates from the list
            if (coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
            {
                // Get RectTransform with safety checks
                RectTransform cellRect = (gridCellRects != null && coord.x < gridCellRects.GetLength(0) && coord.y < gridCellRects.GetLength(1)) ? gridCellRects[coord.x, coord.y] : null;
                if (cellRect != null)
                {
                    // Add a scale-down animation to the sequence
                    effectSequence.Join(cellRect.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack));
                }
            }
            else { Debug.LogWarning($"ReplaceLettersAt: Skipping invalid coordinate ({coord.x},{coord.y}) during effect.", this); }
        }

        // --- Action After Effect Sequence Completes ---
        effectSequence.OnComplete(() => {
            Debug.Log("Replacement effect complete. Now replacing letters data and visuals.");
            // Iterate through the coordinates again to perform the actual replacement
            foreach (Vector2Int coord in coordinates)
            {
                // Call the single replace method (which handles data, text, and pop-in effect)
                // Need to re-check bounds here as ReplaceLetter does its own check
                if (coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize)
                {
                    ReplaceLetter(coord.x, coord.y);
                }
                else { Debug.LogWarning($"ReplaceLettersAt: Skipping invalid coordinate ({coord.x},{coord.y}) during replacement.", this); }
            }

            // --- REMOVED IMMEDIATE RE-VALIDATION ---
            // Validation will now be triggered by GridInputHandler after grid settles.
            Debug.Log("Replacement complete. Validation will be triggered by Input Handler if grid is settled.");

            // --- Optional: Restore Input State ---
            // isAnimating = wasAnimating; // Restore previous animation state if changed
            // Debug.Log($"Finished replacing letters. isAnimating set back to {isAnimating}");
        });

        // --- Alternative: Immediate Replacement (If no effects sequence is used) ---
        /*
        foreach (Vector2Int coord in coordinates)
        {
            if (coord.x >= 0 && coord.x < gridSize && coord.y >= 0 && coord.y < gridSize) {
                ReplaceLetter(coord.x, coord.y);
            } else { Debug.LogWarning($"ReplaceLettersAt: Skipping invalid coordinate ({coord.x},{coord.y}) during replacement.", this); }
        }
        // Validation would need to be triggered externally after this block completes.
        */
    }

    // --- NEW Public Method to Trigger Validation ---
    // Called by GridInputHandler when the drag ends and animations stop.
    public void TriggerValidationCheck()
    {
        // Safety check: Only validate if not currently animating.
        // This prevents accidental validation if this method is called while an animation
        // (like the replacement pop-in) is still technically running.
        if (isAnimating)
        {
            Debug.LogWarning("TriggerValidationCheck called, but WordGridManager is still animating (isAnimating=true). Ignoring call.", this);
            return;
        }

        Debug.Log("WordGridManager: TriggerValidationCheck received. Calling WordValidator.", this);
        if (wordValidator != null)
        {
            // Call the main validation method in WordValidator
            wordValidator.ValidateWords();
        }
        else
        {
            Debug.LogError("Cannot trigger validation: WordValidator reference is missing!", this);
        }
    }

    // Keep TriggerValidation helper only if used with Invoke for delays (less likely needed now)
    // private void TriggerValidation() { if (wordValidator != null) { wordValidator.ValidateWords(); } }

} // End of WordGridManager class