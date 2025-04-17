using UnityEngine;
using TMPro; // Required for TextMeshPro elements
using System.Collections.Generic; // Might need later, but not strictly for population
using Random = UnityEngine.Random; // Specify Unity's Random to avoid ambiguity
using System.Collections;
using DG.Tweening;

public class WordGridManager : MonoBehaviour
{
    [Header("Grid Configuration")]
    [SerializeField] public int gridSize = 4; // Set to 4x4 specifically

    [Header("Word Validation")]
    [Tooltip("Assign the WordValidator script instance here")]
    [SerializeField] private WordValidator wordValidator; // Add a reference to WordValidator

    [Header("UI References")]
    [Tooltip("The UI prefab for a single letter cell (must contain TextMeshProUGUI)")]
    [SerializeField] private GameObject letterCellPrefab;

    [Tooltip("The container RectTransform where instantiated cells will be placed as children")]
    [SerializeField] private RectTransform gridContainer;

    [Header("Layout Settings")]
    [Tooltip("The dimensions (Width, Height) of each individual cell")]
    [SerializeField] private Vector2 cellSize = new Vector2(100f, 100f);

    [Tooltip("The space between adjacent cells")]
    [SerializeField] private Vector2 spacing = new Vector2(10f, 10f);

    [Header("Animation Settings")]
    [Tooltip("How long the scroll animation takes in seconds")]
    [SerializeField] private float scrollAnimationDuration = 0.2f;
    [Tooltip("The easing function to use for the scroll animation")]
    [SerializeField] public Ease scrollEaseType = Ease.OutSine; // <<< ADDED: Designer chooses ease

    // --- Public Getters for Input Handler ---
    public int GridSize => gridSize;
    public Vector2 CellSize => cellSize; // Make cell size readable
    public Vector2 Spacing => spacing;   // Make spacing readable
    public bool IsAnimating => isAnimating; // Make animation state readable

    // --- Private Grid Data ---
    private char[,] gridData;
    private TextMeshProUGUI[,] gridCellTextComponents;
    private RectTransform[,] gridCellRects; // <<< ADDED: To store RectTransforms

    // --- State ---
    private bool isAnimating = false; // <<< ADDED: Input blocking flag

    // Weighted string for more realistic letter distribution based on English frequency
    // (Adjust weights as desired)
    private const string WeightedLetters =
        "EEEEEEEEEEEEAAAAAAAAAIIIIIIIIIOOOOOOOONNNNNNRRRRRRTTTTTT" + // High frequency
        "LLLLSSSSUUUUDDDGGGBBCCMMPPFFHHVVWWYY" + // Medium frequency
        "KJXQZ"; // Low frequency

    //--------------------------------------------------------------------------
    // Initialization
    //--------------------------------------------------------------------------

    void Start()
    {
        // --- Basic Validation ---
        if (letterCellPrefab == null)
        {
            Debug.LogError("WordGridManager: Letter Cell Prefab is not assigned in the Inspector!", this);
            return;
        }
        if (gridContainer == null)
        {
            Debug.LogError("WordGridManager: Grid Container (RectTransform) is not assigned in the Inspector!", this);
            return;
        }
        // Check if prefab has the text component (optional but good)
        if (letterCellPrefab.GetComponentInChildren<TextMeshProUGUI>() == null)
        {
            Debug.LogError($"WordGridManager: Letter Cell Prefab '{letterCellPrefab.name}' and its children do not contain a TextMeshProUGUI component!", letterCellPrefab);
            return;
        }

        if (letterCellPrefab.GetComponent<RectTransform>() == null) // <<< ADDED: Check for RectTransform on prefab
        {
            Debug.LogError($"WordGridManager: Letter Cell Prefab '{letterCellPrefab.name}' is missing a RectTransform component!", letterCellPrefab);
            return;
        }

        wordValidator = GetComponent<WordValidator>(); // Get the WordValidator component
        if (wordValidator == null)
        {
            Debug.LogError("WordValidator component not found on the same GameObject as WordGridManager!", this);
        }

        // --- Initialize and Populate ---
        InitializeGridData();
        CreateGridVisuals();

        Debug.Log("Word Grid Initialized and Populated.");
    }

    // Modified Initialization to include RectTransforms
    void InitializeGridData()
    {
        gridData = new char[gridSize, gridSize];
        gridCellTextComponents = new TextMeshProUGUI[gridSize, gridSize];
        gridCellRects = new RectTransform[gridSize, gridSize]; // <<< Initialize RectTransform array

        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                gridData[r, c] = GetRandomWeightedLetter();
            }
        }
    }

    // Modified Visual Creation to store RectTransforms
    void CreateGridVisuals()
    {
        gridCellTextComponents = new TextMeshProUGUI[gridSize, gridSize]; // Ensure arrays are ready
        gridCellRects = new RectTransform[gridSize, gridSize];          // Ensure arrays are ready

        ClearExistingVisuals(); // Keep this

        Vector2 totalCellStep = cellSize + spacing;
        float startX = -(gridSize - 1) * totalCellStep.x / 2.0f;
        float startY = (gridSize - 1) * totalCellStep.y / 2.0f;

        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                GameObject newCellGO = Instantiate(letterCellPrefab, gridContainer);
                newCellGO.name = $"Cell_{r}_{c}";

                RectTransform cellRect = newCellGO.GetComponent<RectTransform>();
                if (cellRect != null)
                {
                    // Calculate position relative to the container's center (based on startX, startY)
                    float xPos = startX + c * totalCellStep.x;
                    float yPos = startY - r * totalCellStep.y; // Y decreases downwards
                    cellRect.anchoredPosition = new Vector2(xPos, yPos);

                    // --- SET THE SIZE EXPLICITLY ---
                    // Ensure this line is NOT commented out:
                    cellRect.sizeDelta = cellSize;

                    gridCellRects[r, c] = cellRect; // Store RectTransform reference
                }
                else
                {
                    Debug.LogWarning($"Cell_{r}_{c} prefab instance is missing RectTransform.", newCellGO);
                }

                // ... (error handling for missing RectTransform) ...

                TextMeshProUGUI textComponent = newCellGO.GetComponentInChildren<TextMeshProUGUI>();
                RectTransform textRectTransform = textComponent.GetComponent<RectTransform>();
                if (textComponent != null)
                {
                    textComponent.text = gridData[r, c].ToString();
                    if (textRectTransform != null)
                    {
                        textRectTransform.sizeDelta = cellSize;
                    }
                    else
                    {
                        Debug.LogWarning($"TextMeshPro component in Cell_{r}_{c} is missing a RectTransform.", textComponent);
                    }
                    gridCellTextComponents[r, c] = textComponent;
                }
                // ... (error handling for missing TextMeshProUGUI) ...
            }
        }
    }

    //--------------------------------------------------------------------------
    // Data Shifting Functions
    //--------------------------------------------------------------------------

    /// <summary>
    /// Performs a circular shift on the character data within a specific row.
    /// Does NOT update visuals.
    /// </summary>
    /// <param name="rowIndex">The index of the row to shift (0 to gridSize-1).</param>
    /// <param name="direction">The direction to shift: +1 for RIGHT, -1 for LEFT.</param>
    public void ShiftRowData(int rowIndex, int direction)
    {
        // --- Validation ---
        if (gridData == null || gridCellRects == null || gridCellTextComponents == null)
        {
            Debug.LogError("[ShiftRowData] Error: Arrays (data/rects/text) not initialized!"); return;
        }
        if (rowIndex < 0 || rowIndex >= gridSize)
        {
            Debug.LogError($"[ShiftRowData] Error: Invalid rowIndex {rowIndex}."); return;
        }
        if (direction != 1 && direction != -1)
        {
            Debug.LogWarning($"[ShiftRowData] Warning: Invalid direction {direction}."); return;
        }

        Debug.Log($"Shifting Row {rowIndex} data & REFS direction: {(direction == 1 ? "RIGHT" : "LEFT")}");

        // --- Store original data and references ---
        char[] tempRowData = new char[gridSize];
        RectTransform[] tempRowRects = new RectTransform[gridSize];
        TextMeshProUGUI[] tempRowTexts = new TextMeshProUGUI[gridSize];

        for (int c = 0; c < gridSize; c++)
        {
            tempRowData[c] = gridData[rowIndex, c];
            tempRowRects[c] = gridCellRects[rowIndex, c];
            tempRowTexts[c] = gridCellTextComponents[rowIndex, c];
        }

        // --- Perform circular shift on all three arrays ---
        for (int c = 0; c < gridSize; c++)
        {
            int sourceColIndex = (c - direction + gridSize) % gridSize;
            gridData[rowIndex, c] = tempRowData[sourceColIndex];
            gridCellRects[rowIndex, c] = tempRowRects[sourceColIndex]; // <<< SHIFT RECTS
            gridCellTextComponents[rowIndex, c] = tempRowTexts[sourceColIndex]; // <<< SHIFT TEXTS

            LetterCell cellComponent = gridCellRects[rowIndex, c].GetComponent<LetterCell>();
            if (cellComponent != null)
            {
                cellComponent.ReduceMove();
            }
            else
            {
                Debug.LogError($"LetterCell component not found on cell at Row {rowIndex}, Col {c} after shift!");
            }
        }
    }


    /// <summary>
    /// Performs a circular shift on the character data within a specific column.
    /// Does NOT update visuals.
    /// </summary>
    /// <param name="colIndex">The index of the column to shift (0 to gridSize-1).</param>
    /// <param name="direction">The direction to shift: +1 for DOWN, -1 for UP.</param>
    public void ShiftColumnData(int colIndex, int direction)
    {
        // --- Validation ---
        if (gridData == null || gridCellRects == null || gridCellTextComponents == null)
        {
            Debug.LogError("[ShiftColumnData] Error: Arrays (data/rects/text) not initialized!"); return;
        }
        if (colIndex < 0 || colIndex >= gridSize)
        {
            Debug.LogError($"[ShiftColumnData] Error: Invalid colIndex {colIndex}."); return;
        }
        if (direction != 1 && direction != -1)
        {
            Debug.LogWarning($"[ShiftColumnData] Warning: Invalid direction {direction}."); return;
        }

        Debug.Log($"Shifting Column {colIndex} data & REFS direction: {(direction == 1 ? "DOWN" : "UP")}");

        // --- Store original data and references ---
        char[] tempColData = new char[gridSize];
        RectTransform[] tempColRects = new RectTransform[gridSize];
        TextMeshProUGUI[] tempColTexts = new TextMeshProUGUI[gridSize];

        for (int r = 0; r < gridSize; r++)
        {
            tempColData[r] = gridData[r, colIndex];
            tempColRects[r] = gridCellRects[r, colIndex];
            tempColTexts[r] = gridCellTextComponents[r, colIndex];
        }

        // --- Perform circular shift on all three arrays ---
        for (int r = 0; r < gridSize; r++)
        {
            int sourceRowIndex = (r - direction + gridSize) % gridSize;
            gridData[r, colIndex] = tempColData[sourceRowIndex];
            gridCellRects[r, colIndex] = tempColRects[sourceRowIndex]; // <<< SHIFT RECTS
            gridCellTextComponents[r, colIndex] = tempColTexts[sourceRowIndex]; // <<< SHIFT TEXTS

            LetterCell cellComponent = gridCellRects[r, colIndex].GetComponent<LetterCell>();
            if (cellComponent != null)
            {
                cellComponent.ReduceMove();
            }
            else
            {
                Debug.LogError($"LetterCell component not found on cell at Row {r}, Col {colIndex} after shift!");
            }
        }
    }


    // --- Optional Helper Function for Debugging ---
    /*
    [ContextMenu("Print Grid Data to Console")] // Allows calling from Inspector context menu
    public void PrintGridData(string context = "")
    {
        if (gridData == null) {
            Debug.LogWarning("Cannot print grid data - array is null.");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"--- Grid Data {context} ({gridSize}x{gridSize}) ---");
        for (int r = 0; r < gridSize; r++)
        {
            for (int c = 0; c < gridSize; c++)
            {
                sb.Append(gridData[r, c]); // Append the character
                if (c < gridSize - 1) sb.Append(" | "); // Add separator
            }
            sb.AppendLine(); // New line for next row
             if (r < gridSize - 1) sb.AppendLine("--+---+---+--"); // Add row separator
        }
        Debug.Log(sb.ToString());
    }
    */

    //--------------------------------------------------------------------------
    // Helper Functions
    //--------------------------------------------------------------------------

    // Gets a random letter, weighted by common English frequency
    char GetRandomWeightedLetter()
    {
        int randomIndex = Random.Range(0, WeightedLetters.Length);
        return WeightedLetters[randomIndex];
    }

    // Removes any existing GameObjects that are children of the grid container
    // Helper to clear previous visuals (if any)
    void ClearExistingVisuals()
    {
        if (gridContainer == null) return;

        // Destroy children safely - loop backwards
        for (int i = gridContainer.childCount - 1; i >= 0; i--)
        {
            // Use DestroyImmediate if clearing in the Editor outside play mode,
            // otherwise Destroy is fine. Assume Play mode here.
            Destroy(gridContainer.GetChild(i).gameObject);
        }

        // Reset the UI component reference array if needed
        // gridCellTextComponents = null; // <<< REMOVE OR COMMENT OUT THIS LINE

        // gridCellTextComponents will be re-initialized by InitializeGridData() before it's needed again.
    }

    //--------------------------------------------------------------------------
    // Public Scroll Request Methods (Called by Input Handler)
    //--------------------------------------------------------------------------

    public void RequestRowScroll(int rowIndex, int direction)
    {
        if (isAnimating) { Debug.Log("Blocked scroll - animating."); return; }
        if (rowIndex < 0 || rowIndex >= gridSize || (direction != 1 && direction != -1)) { Debug.LogWarning($"Invalid scroll request: Row={rowIndex}, Dir={direction}"); return; }

        isAnimating = true;
        // 1. Shift the underlying data model first
        ShiftRowData(rowIndex, direction);

        // --- Setup Animation ---
        Sequence scrollSequence = DOTween.Sequence();
        Vector2 totalCellStep = cellSize + spacing;
        float startX = -(gridSize - 1) * totalCellStep.x / 2.0f; // Centered start X

        // --- Animate all cells in the row by one step ---
        for (int c = 0; c < gridSize; c++)
        {
            RectTransform cellRect = gridCellRects[rowIndex, c];
            TextMeshProUGUI textComp = gridCellTextComponents[rowIndex, c];

            if (cellRect != null && textComp != null)
            {
                // 2. Update text content based on NEW data BEFORE animating position
                textComp.text = gridData[rowIndex, c].ToString();

                // 3. Calculate target position (one step over from current)
                Vector2 currentPos = cellRect.anchoredPosition;
                Vector2 targetPosition = currentPos + new Vector2(direction * totalCellStep.x, 0);

                // 4. Add movement to sequence
                scrollSequence.Join(cellRect.DOAnchorPos(targetPosition, scrollAnimationDuration)
                                         .SetEase(scrollEaseType));
            }
            else
            {
                Debug.LogError($"Missing RectTransform or Text for Row {rowIndex}, Col {c} during animation setup!");
            }
        }

        // --- Post-Animation Repositioning (OnComplete) ---
        // Replace the .OnComplete block inside RequestRowScroll:
        scrollSequence.OnComplete(() => {
            // --- Post-Animation Repositioning (OnComplete) ---

            // 5. Identify the NEW logical index of the cell that just wrapped around
            int wrappedCellNewCol = (direction == 1) ? 0 : (gridSize - 1);

            // 6. Get the RectTransform reference from its NEW position in the (now shifted) array
            RectTransform rectToCorrect = gridCellRects[rowIndex, wrappedCellNewCol];

            if (rectToCorrect != null)
            {
                // 7. Calculate the CORRECT final grid position for this cell based on its NEW logical column index
                Vector2 finalGridPos = new Vector2(startX + wrappedCellNewCol * totalCellStep.x, rectToCorrect.anchoredPosition.y); // Y is unchanged for row scroll

                // 8. Instantly SNAP the cell's visual position to its correct final grid location
                rectToCorrect.anchoredPosition = finalGridPos;

                Debug.Log($"Row {rowIndex} Anim Complete. Corrected wrapped cell at new col {wrappedCellNewCol} to pos {finalGridPos}");
            }
            else { Debug.LogError($"RectTransform to correct is null for Row {rowIndex}, new col {wrappedCellNewCol}"); }

            if (wordValidator != null)
            {
                wordValidator.ValidateWords();
            }

            isAnimating = false; // Unblock input
        });

        // Optional settings
        scrollSequence.SetUpdate(UpdateType.Normal, true);
    }


    public void RequestColumnScroll(int colIndex, int direction)
    {
        if (isAnimating) { Debug.Log("Blocked scroll - animating."); return; }
        if (colIndex < 0 || colIndex >= gridSize || (direction != 1 && direction != -1)) { Debug.LogWarning($"Invalid scroll request: Col={colIndex}, Dir={direction}"); return; }

        isAnimating = true;
        // 1. Shift the underlying data model first
        ShiftColumnData(colIndex, direction);

        // --- Setup Animation ---
        Sequence scrollSequence = DOTween.Sequence();
        Vector2 totalCellStep = cellSize + spacing;
        float startY = (gridSize - 1) * totalCellStep.y / 2.0f; // Centered start Y (top position)

        // --- Animate all cells in the column by one step ---
        for (int r = 0; r < gridSize; r++)
        {
            RectTransform cellRect = gridCellRects[r, colIndex];
            TextMeshProUGUI textComp = gridCellTextComponents[r, colIndex];

            if (cellRect != null && textComp != null)
            {
                // 2. Update text content based on NEW data BEFORE animating position
                textComp.text = gridData[r, colIndex].ToString();

                // 3. Calculate target position (one step up/down from current)
                // Remember +Y is UP in anchoredPosition, but our +1 direction means DOWN
                Vector2 currentPos = cellRect.anchoredPosition;
                Vector2 targetPosition = currentPos + new Vector2(0, -direction * totalCellStep.y); // -direction because Y is inverted visually

                // 4. Add movement to sequence
                scrollSequence.Join(cellRect.DOAnchorPos(targetPosition, scrollAnimationDuration)
                                         .SetEase(scrollEaseType));
            }
            else
            {
                Debug.LogError($"Missing RectTransform or Text for Col {colIndex}, Row {r} during animation setup!");
            }
        }

        // --- Post-Animation Repositioning (OnComplete) ---
        scrollSequence.OnComplete(() => {
            // --- Post-Animation Repositioning (OnComplete) ---

            // 5. Identify the NEW logical index of the cell that just wrapped around
            int wrappedCellNewRow = (direction == 1) ? 0 : (gridSize - 1);

            // 6. Get the RectTransform reference from its NEW position in the (now shifted) array
            RectTransform rectToCorrect = gridCellRects[wrappedCellNewRow, colIndex];

            if (rectToCorrect != null)
            {
                // 7. Calculate the CORRECT final grid position for this cell based on its NEW logical row index
                Vector2 finalGridPos = new Vector2(rectToCorrect.anchoredPosition.x, startY - wrappedCellNewRow * totalCellStep.y); // X is unchanged for column scroll

                // 8. Instantly SNAP the cell's visual position to its correct final grid location
                rectToCorrect.anchoredPosition = finalGridPos;

                Debug.Log($"Col {colIndex} Anim Complete. Corrected wrapped cell at new row {wrappedCellNewRow} to pos {finalGridPos}");
            }
            else { Debug.LogError($"RectTransform to correct is null for Col {colIndex}, new row {wrappedCellNewRow}"); }

            if (wordValidator != null)
            {
                wordValidator.ValidateWords();
            }
            isAnimating = false; // Unblock input
        });

        // Optional settings
        scrollSequence.SetUpdate(UpdateType.Normal, true);
    }



    //--------------------------------------------------------------------------
    // Animation Coroutines
    //--------------------------------------------------------------------------

    private IEnumerator AnimateRowScrollVisuals(int rowIndex, int direction)
    {
        Debug.Log($"Animating Row {rowIndex} scroll visual, direction {direction}");
        Vector2 totalCellStep = cellSize + spacing;
        float startX = -(gridSize - 1) * totalCellStep.x / 2.0f;

        // --- Prepare for animation ---
        RectTransform[] rowRects = new RectTransform[gridSize];
        Vector2[] startPositions = new Vector2[gridSize];
        Vector2[] targetPositions = new Vector2[gridSize];

        // Identify the cell that needs to wrap visually
        int wrapAroundSourceCol = (direction == 1) ? gridSize - 1 : 0; // Original col index that moves off screen
        RectTransform wrappingCellRect = gridCellRects[rowIndex, wrapAroundSourceCol];

        // Calculate the position it needs to jump to *before* animating in
        float jumpToX = (direction == 1) ? startX - totalCellStep.x : startX + gridSize * totalCellStep.x;
        wrappingCellRect.anchoredPosition = new Vector2(jumpToX, wrappingCellRect.anchoredPosition.y);

        // Update Text and get Start/Target Positions for all cells in the row
        for (int c = 0; c < gridSize; c++)
        {
            // Update Text based on *new* gridData
            if (gridCellTextComponents[rowIndex, c] != null)
            {
                gridCellTextComponents[rowIndex, c].text = gridData[rowIndex, c].ToString();
            }

            // Store rects and positions
            rowRects[c] = gridCellRects[rowIndex, c];
            startPositions[c] = rowRects[c].anchoredPosition; // Current position (includes the jumped one)
            // Calculate final target position based on logical index
            targetPositions[c] = new Vector2(startX + c * totalCellStep.x, startPositions[c].y); // Y remains same
        }

        // --- Animate ---
        float elapsedTime = 0f;
        while (elapsedTime < scrollAnimationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / scrollAnimationDuration);
            // Optional: Add easing (e.g., t = Mathf.Sin(t * Mathf.PI * 0.5f); // Ease Out Sine)

            for (int c = 0; c < gridSize; c++)
            {
                if (rowRects[c] != null)
                {
                    rowRects[c].anchoredPosition = Vector2.Lerp(startPositions[c], targetPositions[c], t);
                }
            }
            yield return null; // Wait for next frame
        }

        // --- Finalize ---
        // Ensure exact positions after animation
        for (int c = 0; c < gridSize; c++)
        {
            if (rowRects[c] != null)
            {
                rowRects[c].anchoredPosition = targetPositions[c];
            }
        }

        Debug.Log($"Finished animating Row {rowIndex}");
        isAnimating = false; // Unblock input
    }


    private IEnumerator AnimateColumnScrollVisuals(int colIndex, int direction)
    {
        Debug.Log($"Animating Column {colIndex} scroll visual, direction {direction}");
        Vector2 totalCellStep = cellSize + spacing;
        float startY = (gridSize - 1) * totalCellStep.y / 2.0f;

        // --- Prepare for animation ---
        RectTransform[] colRects = new RectTransform[gridSize];
        Vector2[] startPositions = new Vector2[gridSize];
        Vector2[] targetPositions = new Vector2[gridSize];

        // Identify the cell that needs to wrap visually
        int wrapAroundSourceRow = (direction == 1) ? gridSize - 1 : 0; // Original row index that moves off screen
        RectTransform wrappingCellRect = gridCellRects[wrapAroundSourceRow, colIndex];

        // Calculate the position it needs to jump to *before* animating in
        // Remember Y decreases downwards on screen -> Positive Y is UP
        float jumpToY = (direction == 1) ? startY + totalCellStep.y : startY - gridSize * totalCellStep.y;
        wrappingCellRect.anchoredPosition = new Vector2(wrappingCellRect.anchoredPosition.x, jumpToY);


        // Update Text and get Start/Target Positions for all cells in the column
        for (int r = 0; r < gridSize; r++)
        {
            // Update Text based on *new* gridData
            if (gridCellTextComponents[r, colIndex] != null)
            {
                gridCellTextComponents[r, colIndex].text = gridData[r, colIndex].ToString();
            }

            // Store rects and positions
            colRects[r] = gridCellRects[r, colIndex];
            startPositions[r] = colRects[r].anchoredPosition; // Current position (includes the jumped one)
            // Calculate final target position based on logical index
            targetPositions[r] = new Vector2(startPositions[r].x, startY - r * totalCellStep.y); // X remains same
        }


        // --- Animate ---
        float elapsedTime = 0f;
        while (elapsedTime < scrollAnimationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / scrollAnimationDuration);
            // Optional: Add easing

            for (int r = 0; r < gridSize; r++)
            {
                if (colRects[r] != null)
                {
                    colRects[r].anchoredPosition = Vector2.Lerp(startPositions[r], targetPositions[r], t);
                }
            }
            yield return null; // Wait for next frame
        }

        // --- Finalize ---
        for (int r = 0; r < gridSize; r++)
        {
            if (colRects[r] != null)
            {
                colRects[r].anchoredPosition = targetPositions[r];
            }
        }

        Debug.Log($"Finished animating Column {colIndex}");
        isAnimating = false; // Unblock input
    }

    //--------------------------------------------------------------------------
    // Public Accessors / Future Methods (Example)
    //--------------------------------------------------------------------------

    public char GetLetterAt(int row, int col)
    {
        if (gridData != null && row >= 0 && row < gridSize && col >= 0 && col < gridSize)
        {
            return gridData[row, col];
        }
        Debug.LogError($"Invalid coordinates requested: ({row}, {col})");
        return '\0'; // Return null character for error
    }

    // Add your scrolling, word checking, and updating functions here later...
    // public void ScrollRow(int rowIndex, int direction) { /* ... */ }
    // public void ScrollColumn(int colIndex, int direction) { /* ... */ }
}