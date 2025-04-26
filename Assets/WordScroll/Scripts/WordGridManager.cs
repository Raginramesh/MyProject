using UnityEngine;
using TMPro;
using DG.Tweening; // Ensure DOTween is imported
using System.Collections.Generic;
using System.Linq;

public class WordGridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int _gridSize = 4;
    [SerializeField] private float _cellSize = 100f;
    [SerializeField] private float _spacing = 10f;
    [SerializeField] private GameObject letterCellPrefab;
    [SerializeField] private Transform gridParent;

    // Public getters for settings needed by other scripts
    public int gridSize => _gridSize;
    public float cellSize => _cellSize;
    public float spacing => _spacing;

    [Header("References")]
    [SerializeField] private WordValidator wordValidator;
    [SerializeField] private GameManager gameManager;

    // Internal grid data
    public char[,] gridData { get; private set; }
    public RectTransform[,] gridCellRects { get; private set; }
    private TextMeshProUGUI[,] gridCellTextComponents;

    // Weighted letters for random generation
    private List<char> WeightedLetters = new List<char>();

    // Flag to prevent input during animations
    // Ensure initial state is false
    public bool isAnimating { get; private set; } = false;

    void Awake()
    {
        // Reset just in case
        isAnimating = false;

        // Find GameManager if not assigned in Inspector (using newer method)
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
            if (gameManager == null)
            {
                Debug.LogError("WordGridManager: GameManager not found in scene!", this);
                // Consider disabling script if essential components are missing
                // enabled = false;
                // return;
            }
            else
            {
                Debug.Log("WordGridManager: Found GameManager using FindFirstObjectByType.", this);
            }
        }
        // Check WordValidator reference
        if (wordValidator == null)
        {
            Debug.LogError("WordGridManager: WordValidator reference not set in Inspector!", this);
            enabled = false; // Disable if validator is missing
            return;
        }
    }

    void Start()
    {
        // Reset just in case, though Awake should handle it
        isAnimating = false;
        PopulateWeightedLettersList();
        // Initialization is now triggered by GameManager.SetState(Playing)
    }

    void OnEnable()
    {
        // Reset when component is enabled (e.g., after returning to scene or restart)
        isAnimating = false;
        Debug.Log("WordGridManager Enabled, isAnimating reset to false.", this);
    }

    // Method for GameManager to set its reference if needed (e.g., if FindObject didn't work or for specific setups)
    public void SetGameManager(GameManager manager)
    {
        gameManager = manager;
        if (gameManager != null) Debug.Log("WordGridManager: GameManager reference set via SetGameManager.", this);
    }

    // Initializes or re-initializes the entire grid
    public void InitializeGrid()
    {
        // --- Reset animation flag at the START of initialization ---
        isAnimating = false;
        Debug.Log("WordGridManager: Initializing Grid... Setting isAnimating = false.", this);

        // Check essential prefab reference
        if (letterCellPrefab == null)
        {
            Debug.LogError("WordGridManager: Letter Cell Prefab is not assigned in the Inspector! Cannot initialize grid.", this);
            return; // Stop initialization if prefab is missing
        }
        // Check grid parent reference, provide fallback with warning
        if (gridParent == null)
        {
            Debug.LogWarning("WordGridManager: Grid Parent is not assigned in Inspector. Defaulting to this object's transform, which might not be ideal for UI positioning.", this);
            gridParent = this.transform;
        }

        // --- Kill any active tweens on cells before destroying/recreating ---
        // This prevents leftover OnComplete calls from firing later
        if (gridCellRects != null)
        {
            Debug.Log("Killing active tweens on existing cells before re-initialization.", this);
            for (int r = 0; r < gridCellRects.GetLength(0); r++)
            {
                for (int c = 0; c < gridCellRects.GetLength(1); c++)
                {
                    if (gridCellRects[r, c] != null)
                    {
                        // Kill any DOTween animations targeting this specific RectTransform
                        DOTween.Kill(gridCellRects[r, c]);

                        // Also destroy the associated GameObject
                        if (gridCellRects[r, c].gameObject != null)
                        {
                            Destroy(gridCellRects[r, c].gameObject);
                        }
                    }
                }
            }
        } // Else: gridCellRects is null, nothing to kill or destroy yet

        // Initialize data structures based on current grid size
        gridData = new char[gridSize, gridSize];
        gridCellTextComponents = new TextMeshProUGUI[gridSize, gridSize];
        gridCellRects = new RectTransform[gridSize, gridSize];

        // Fill the logical grid with random letters
        PopulateGridData();

        // Create the visual grid GameObjects
        float totalGridSizeUI = gridSize * cellSize + (gridSize - 1) * spacing;
        float startOffset = -totalGridSizeUI / 2f + cellSize / 2f;
        int cellsCreated = 0; // Counter for debugging

        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                // Instantiate the prefab as a child of gridParent
                GameObject cellGO = Instantiate(letterCellPrefab, gridParent);
                cellsCreated++;

                // Get components from the instantiated prefab
                RectTransform cellRect = cellGO.GetComponent<RectTransform>();
                // Find TextMeshPro component in children (robust)
                TextMeshProUGUI cellText = cellGO.GetComponentInChildren<TextMeshProUGUI>(true); // Include inactive

                // --- Essential Component Checks ---
                if (cellRect == null)
                {
                    Debug.LogError($"Instantiated Prefab '{letterCellPrefab.name}' is missing RectTransform component! Check the prefab root.", cellGO);
                    Destroy(cellGO); // Destroy invalid cell
                    continue; // Skip to next iteration
                }
                if (cellText == null)
                {
                    // Log error but maybe don't destroy, depending on requirements
                    Debug.LogError($"Instantiated Prefab '{letterCellPrefab.name}' does not have a TextMeshProUGUI component in its children! Check the prefab structure.", cellGO);
                }

                // Calculate position based on grid coordinates
                // Note: Y is inverted for typical UI top-left origin vs. bottom-left array indexing
                float posX = startOffset + c * (cellSize + spacing);
                float posY = startOffset + (gridSize - 1 - r) * (cellSize + spacing);

                // Apply position, size, and ensure scale is correct
                cellRect.anchoredPosition = new Vector2(posX, posY);
                cellRect.sizeDelta = new Vector2(cellSize, cellSize);
                cellRect.localScale = Vector3.one; // Ensure scale isn't zero

                // Set the text content if the component was found
                if (cellText != null)
                {
                    cellText.text = gridData[r, c].ToString();
                    // Ensure Auto Size is enabled on the prefab itself for text scaling
                }
                else
                {
                    // Name the cell differently if text is missing, for easier debugging
                    cellGO.name = $"Cell_{r}_{c}_(NoText)";
                }

                // Store references for later use (animation, data access)
                gridCellTextComponents[r, c] = cellText;
                gridCellRects[r, c] = cellRect;
            }
        }
        // Log completion and final state
        Debug.Log($"WordGridManager: Grid Initialized. Attempted to create {cellsCreated} cells. isAnimating = {isAnimating}", this);
        if (cellsCreated != gridSize * gridSize)
        {
            Debug.LogWarning($"Expected {gridSize * gridSize} cells, but only reported {cellsCreated} created. Some might have failed instantiation checks.", this);
        }
        // Note: Initial word validation is now handled by GameManager after calling InitializeGrid
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

    // Fills the internal gridData array with random letters
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

    // Gets a random letter based on the weighted list
    char GetRandomLetter()
    {
        if (WeightedLetters.Count == 0)
        {
            Debug.LogWarning("WeightedLetters list is empty! Returning '?'.");
            return '?'; // Fallback character
        }
        return WeightedLetters[Random.Range(0, WeightedLetters.Count)];
    }

    // --- Scroll Request Logic (Called by GridInputHandler) ---

    // Handles request to scroll a specific row
    public void RequestRowScroll(int rowIndex, int direction, float scrollAmount)
    {
        // Check conditions: Component enabled, not already animating, game is playing
        if (!enabled || isAnimating || gameManager == null || gameManager.CurrentState != GameManager.GameState.Playing)
        {
            Debug.LogWarning($"RequestRowScroll ignored: Enabled={enabled}, isAnimating={isAnimating}, State={gameManager?.CurrentState}", this);
            return; // Exit if conditions not met
        }

        // --- SET isAnimating = true ---
        // Block further input until animation completes or is killed
        isAnimating = true;
        Debug.Log($"RequestRowScroll: Setting isAnimating = true. Row {rowIndex}, Dir {direction}", this);

        // Decrement moves if the mechanic is enabled in GameManager
        if (gameManager != null) gameManager.DecrementMoves();

        // Update the internal data representation of the grid
        ShiftRowData(rowIndex, direction);

        // Start the visual animation
        AnimateRowScroll(rowIndex, direction, scrollAmount);
    }

    // Handles request to scroll a specific column
    public void RequestColumnScroll(int colIndex, int direction, float scrollAmount)
    {
        // Check conditions: Component enabled, not already animating, game is playing
        if (!enabled || isAnimating || gameManager == null || gameManager.CurrentState != GameManager.GameState.Playing)
        {
            Debug.LogWarning($"RequestColumnScroll ignored: Enabled={enabled}, isAnimating={isAnimating}, State={gameManager?.CurrentState}", this);
            return; // Exit if conditions not met
        }

        // --- SET isAnimating = true ---
        // Block further input until animation completes or is killed
        isAnimating = true;
        Debug.Log($"RequestColumnScroll: Setting isAnimating = true. Col {colIndex}, Dir {direction}", this);

        // Decrement moves if the mechanic is enabled in GameManager
        if (gameManager != null) gameManager.DecrementMoves();

        // Update the internal data representation of the grid
        ShiftColumnData(colIndex, direction);

        // Start the visual animation
        AnimateColumnScroll(colIndex, direction, scrollAmount);
    }

    // --- Data Shifting Logics (Internal) ---

    // Shifts the data and references in a specific row
    void ShiftRowData(int rowIndex, int direction)
    {
        // Use public property 'gridSize' for bounds
        if (direction == 1)
        { // Shift Right
            // Store the wrapping element
            char tempData = gridData[rowIndex, gridSize - 1];
            RectTransform tempRect = gridCellRects[rowIndex, gridSize - 1];
            TextMeshProUGUI tempText = gridCellTextComponents[rowIndex, gridSize - 1];
            // Shift elements one step to the right
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
        { // Shift Left (direction == -1)
            // Store the wrapping element
            char tempData = gridData[rowIndex, 0];
            RectTransform tempRect = gridCellRects[rowIndex, 0];
            TextMeshProUGUI tempText = gridCellTextComponents[rowIndex, 0];
            // Shift elements one step to the left
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
        // Debug.Log($"Shifted Row {rowIndex}, Direction {direction}"); // Can be spammy
    }

    // Shifts the data and references in a specific column
    void ShiftColumnData(int colIndex, int direction)
    {
        // Use public property 'gridSize' for bounds
        if (direction == 1)
        { // Shift Down
            // Store the wrapping element
            char tempData = gridData[gridSize - 1, colIndex];
            RectTransform tempRect = gridCellRects[gridSize - 1, colIndex];
            TextMeshProUGUI tempText = gridCellTextComponents[gridSize - 1, colIndex];
            // Shift elements one step down
            for (int r = gridSize - 1; r > 0; r--)
            {
                gridData[r, colIndex] = gridData[r - 1, colIndex];
                gridCellRects[r, colIndex] = gridCellRects[r - 1, colIndex];
                gridCellTextComponents[r, colIndex] = gridCellTextComponents[r - 1, colIndex];
            }
            // Place the stored element at the top
            gridData[0, colIndex] = tempData;
            gridCellRects[0, colIndex] = tempRect;
            gridCellTextComponents[0, colIndex] = tempText;
        }
        else
        { // Shift Up (direction == -1)
            // Store the wrapping element
            char tempData = gridData[0, colIndex];
            RectTransform tempRect = gridCellRects[0, colIndex];
            TextMeshProUGUI tempText = gridCellTextComponents[0, colIndex];
            // Shift elements one step up
            for (int r = 0; r < gridSize - 1; r++)
            {
                gridData[r, colIndex] = gridData[r + 1, colIndex];
                gridCellRects[r, colIndex] = gridCellRects[r + 1, colIndex];
                gridCellTextComponents[r, colIndex] = gridCellTextComponents[r + 1, colIndex];
            }
            // Place the stored element at the bottom
            gridData[gridSize - 1, colIndex] = tempData;
            gridCellRects[gridSize - 1, colIndex] = tempRect;
            gridCellTextComponents[gridSize - 1, colIndex] = tempText;
        }
        // Debug.Log($"Shifted Column {colIndex}, Direction {direction}"); // Can be spammy
    }

    // --- Visual Animation Logics (Using DOTween) ---

    // Animates the visual scroll of a row
    void AnimateRowScroll(int rowIndex, int direction, float scrollAmount)
    {
        Debug.Log($"AnimateRowScroll START. Row {rowIndex}. isAnimating should be true.", this);
        // Calculate distances based on grid settings
        float totalMove = gridSize * (cellSize + spacing); // Total distance for wrap-around
        float targetX = direction * (cellSize + spacing); // Distance for one cell shift

        // Create a DOTween sequence to manage animations together
        Sequence seq = DOTween.Sequence();

        // Add safety check: Ensure gridCellRects is not null and indices are valid before looping
        if (gridCellRects == null || rowIndex < 0 || rowIndex >= gridCellRects.GetLength(0))
        {
            Debug.LogError($"AnimateRowScroll Error: gridCellRects is null or rowIndex {rowIndex} is out of bounds.", this);
            ResetAnimationFlag("RowScroll Error"); // Reset flag if animation can't run
            return;
        }

        // Animate each cell in the row
        for (int c = 0; c < gridSize; c++)
        {
            // Check individual cell rect before animating
            if (c < 0 || c >= gridCellRects.GetLength(1) || gridCellRects[rowIndex, c] == null)
            {
                Debug.LogWarning($"AnimateRowScroll Skipping animation for cell [{rowIndex},{c}] - RectTransform is null or index out of bounds.", this);
                continue; // Skip this cell if invalid
            }
            RectTransform cellRect = gridCellRects[rowIndex, c];
            Vector2 startPos = cellRect.anchoredPosition;
            // Add the horizontal movement tween to the sequence
            seq.Join(cellRect.DOAnchorPosX(startPos.x + targetX, 0.3f).SetEase(Ease.OutCubic));
        }

        // Handle the wrapping cell's instant repositioning
        int wrapIndex = (direction == 1) ? 0 : gridSize - 1; // Cell that wrapped around logically
        // Check wrap index and cell before accessing
        if (wrapIndex < 0 || wrapIndex >= gridCellRects.GetLength(1) || gridCellRects[rowIndex, wrapIndex] == null)
        {
            Debug.LogWarning($"AnimateRowScroll Skipping wrap logic for cell [{rowIndex},{wrapIndex}] - RectTransform is null or index out of bounds.", this);
        }
        else
        {
            RectTransform wrapCell = gridCellRects[rowIndex, wrapIndex];
            Vector2 wrapStartPos = wrapCell.anchoredPosition;
            // Insert a callback slightly before the main animation ends to instantly move the wrapped cell
            seq.InsertCallback(0.29f, () => {
                // Check again inside callback in case object was destroyed
                if (wrapCell != null)
                {
                    // Move it to the opposite side from where it will animate *from*
                    wrapCell.anchoredPosition = new Vector2(wrapStartPos.x - direction * totalMove, wrapStartPos.y);
                }
                else { Debug.LogWarning("Wrap cell became null inside row scroll callback!", this); }
            });
        }

        // --- Define what happens when the sequence is killed prematurely ---
        seq.OnKill(() => {
            Debug.LogWarning("AnimateRowScroll Sequence KILLED.", this);
            // Attempt to reset the animation flag even if killed
            ResetAnimationFlag("RowScroll Killed");
            // Consider snapping to grid immediately? Might cause visual jumps.
            // SnapToGridPositions();
        });

        // --- Define what happens when the sequence completes normally ---
        seq.OnComplete(() => {
            Debug.Log("AnimateRowScroll Sequence COMPLETE.", this);
            // Ensure SnapToGridPositions runs without error after animation
            try
            {
                SnapToGridPositions(); // Snap all cells to their final calculated positions
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error during SnapToGridPositions after row scroll: {e.Message}\n{e.StackTrace}", this);
            }

            // Reset flag AFTER snapping and BEFORE validation (if validation doesn't start new animations)
            ResetAnimationFlag("RowScroll Complete"); // Use helper method

            // Trigger word validation after the visual scroll is complete
            if (wordValidator != null)
            {
                try
                {
                    wordValidator.ValidateWords();
                }
                catch (System.Exception e)
                {
                    // Catch errors during validation to prevent them from breaking game flow
                    Debug.LogError($"Error during ValidateWords after row scroll: {e.Message}\n{e.StackTrace}", this);
                }
            }
            else { Debug.LogWarning("WordValidator reference missing, cannot validate words after row scroll.", this); }
        });
    }

    // Animates the visual scroll of a column
    void AnimateColumnScroll(int colIndex, int direction, float scrollAmount)
    {
        Debug.Log($"AnimateColumnScroll START. Col {colIndex}. isAnimating should be true.", this);
        // Calculate distances
        float totalMove = gridSize * (cellSize + spacing);
        float targetY = -direction * (cellSize + spacing); // Inverted Y for UI coordinates

        // Create sequence
        Sequence seq = DOTween.Sequence();

        // Add safety check: Ensure gridCellRects is not null and indices are valid before looping
        if (gridCellRects == null || colIndex < 0 || colIndex >= gridCellRects.GetLength(1))
        {
            Debug.LogError($"AnimateColumnScroll Error: gridCellRects is null or colIndex {colIndex} is out of bounds.", this);
            ResetAnimationFlag("ColScroll Error"); // Reset flag if animation can't run
            return;
        }

        // Animate each cell in the column
        for (int r = 0; r < gridSize; r++)
        {
            // Check individual cell rect before animating
            if (r < 0 || r >= gridCellRects.GetLength(0) || gridCellRects[r, colIndex] == null)
            {
                Debug.LogWarning($"AnimateColumnScroll Skipping animation for cell [{r},{colIndex}] - RectTransform is null or index out of bounds.", this);
                continue; // Skip this cell if invalid
            }
            RectTransform cellRect = gridCellRects[r, colIndex];
            Vector2 startPos = cellRect.anchoredPosition;
            // Add the vertical movement tween
            seq.Join(cellRect.DOAnchorPosY(startPos.y + targetY, 0.3f).SetEase(Ease.OutCubic));
        }

        // Handle wrapping cell repositioning
        int wrapIndex = (direction == 1) ? 0 : gridSize - 1; // Cell that wrapped around logically
        // Check wrap index and cell before accessing
        if (wrapIndex < 0 || wrapIndex >= gridCellRects.GetLength(0) || gridCellRects[wrapIndex, colIndex] == null)
        {
            Debug.LogWarning($"AnimateColumnScroll Skipping wrap logic for cell [{wrapIndex},{colIndex}] - RectTransform is null or index out of bounds.", this);
        }
        else
        {
            RectTransform wrapCell = gridCellRects[wrapIndex, colIndex];
            Vector2 wrapStartPos = wrapCell.anchoredPosition;
            // Insert callback to instantly move the wrapped cell
            seq.InsertCallback(0.29f, () => {
                // Check again inside callback
                if (wrapCell != null)
                {
                    // Move it to the opposite side vertically
                    wrapCell.anchoredPosition = new Vector2(wrapStartPos.x, wrapStartPos.y + direction * totalMove); // Y direction calculation adjusted
                }
                else { Debug.LogWarning("Wrap cell became null inside column scroll callback!", this); }
            });
        }

        // --- Define OnKill behavior ---
        seq.OnKill(() => {
            Debug.LogWarning("AnimateColumnScroll Sequence KILLED.", this);
            ResetAnimationFlag("ColScroll Killed");
            // SnapToGridPositions(); // Optional: Snap immediately on kill
        });

        // --- Define OnComplete behavior ---
        seq.OnComplete(() => {
            Debug.Log("AnimateColumnScroll Sequence COMPLETE.", this);
            // Ensure SnapToGrid runs safely
            try
            {
                SnapToGridPositions();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error during SnapToGridPositions after column scroll: {e.Message}\n{e.StackTrace}", this);
            }

            // Reset flag AFTER snapping, BEFORE validation
            ResetAnimationFlag("ColScroll Complete"); // Use helper method

            // Trigger validation
            if (wordValidator != null)
            {
                try
                {
                    wordValidator.ValidateWords();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error during ValidateWords after column scroll: {e.Message}\n{e.StackTrace}", this);
                }
            }
            else { Debug.LogWarning("WordValidator reference missing, cannot validate words after column scroll.", this); }
        });
    }

    // Helper method to reliably reset the animation flag and log the reason
    private void ResetAnimationFlag(string reason)
    {
        // Only log the change if it was actually true, to reduce console spam
        if (isAnimating)
        {
            isAnimating = false;
            Debug.Log($"ResetAnimationFlag: Setting isAnimating = false. Reason: {reason}", this);
        }
        // Optional: Log even if it was already false, for deeper debugging
        // else { Debug.Log($"ResetAnimationFlag called but isAnimating was already false. Reason: {reason}", this); }
    }

    // Snaps all visual cells to their correct grid positions based on the current gridCellRects array
    void SnapToGridPositions()
    {
        // Added null check for gridCellRects at the beginning
        if (gridCellRects == null)
        {
            Debug.LogError("SnapToGridPositions cannot run: gridCellRects array is null.", this);
            return;
        }
        // Debug.Log("Snapping cells to final grid positions.", this); // Can be spammy

        // Recalculate grid layout parameters
        float totalGridSizeUI = gridSize * cellSize + (gridSize - 1) * spacing;
        float startOffset = -totalGridSizeUI / 2f + cellSize / 2f;

        // Loop through the grid positions
        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                // Check individual cell rect before accessing its anchoredPosition
                if (gridCellRects[r, c] != null)
                {
                    // Calculate the target position for this grid cell
                    float targetX = startOffset + c * (cellSize + spacing);
                    float targetY = startOffset + (gridSize - 1 - r) * (cellSize + spacing); // Inverted Y
                    // Instantly set the anchored position
                    gridCellRects[r, c].anchoredPosition = new Vector2(targetX, targetY);
                }
                // Optional warning if a cell reference is missing in the array
                // else { Debug.LogWarning($"SnapToGridPositions: Cell [{r},{c}] RectTransform reference is null in array.", this); }
            }
        }
        // Debug.Log("Finished snapping cells.", this); // Can be spammy
    }

    // Replaces a letter at a specific grid position (e.g., after a word is found)
    public void ReplaceLetter(int row, int col)
    {
        // Bounds check
        if (row < 0 || row >= gridSize || col < 0 || col >= gridSize)
        {
            Debug.LogWarning($"ReplaceLetter: Invalid coordinates ({row}, {col}).", this);
            return;
        }

        // Get new letter data
        gridData[row, col] = GetRandomLetter();

        // Update visual text if the component exists
        if (gridCellTextComponents != null && gridCellTextComponents[row, col] != null)
        {
            gridCellTextComponents[row, col].text = gridData[row, col].ToString();
            // Optional: Add a small visual effect (e.g., quick scale punch)
            // gridCellRects[row, col]?.DOPunchScale(Vector3.one * 0.2f, 0.2f);
        }
        else
        {
            // If text component was missing, log warning
            Debug.LogWarning($"Cannot update text for replaced letter at [{row},{col}], TextMeshProUGUI component missing or array invalid.", this);
        }
    }
}