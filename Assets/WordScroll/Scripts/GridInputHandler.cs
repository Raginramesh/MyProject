using UnityEngine;
using UnityEngine.EventSystems; // Required for drag interfaces
using UnityEngine.UI; // Required for Image component
using System.Collections.Generic; // Required for List

public class GridInputHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("References")]
    [SerializeField] private WordGridManager wordGridManager; // Reference to the grid manager
    [SerializeField] private RectTransform gridPanelRect; // The UI Panel RectTransform that receives drag events
    // <<< ADDED: Reference to UI Camera >>>
    [SerializeField] private Camera uiCamera; // Camera for ScreenPointToLocalPointInRectangle (usually Main Camera for Screen Space - Camera canvas)

    [Header("Drag Settings")]
    [SerializeField] private float dragThreshold = 20f; // Minimum distance drag before locking axis
    [SerializeField] private float scrollThresholdFactor = 0.4f; // Factor of cell size+spacing to trigger a scroll

    [Header("Highlight Settings")]
    [Tooltip("Whether to apply visual highlight on drag")]
    [SerializeField] private bool enableDragHighlight = true;
    [Tooltip("Percentage to increase cell size (e.g., 1.08 for 8%)")]
    [SerializeField] private float highlightScaleMultiplier = 1.08f;
    [Tooltip("Color to tint the cell background during drag")]
    [SerializeField] private Color highlightColor = new Color(0.9f, 0.9f, 0.9f, 1f); // Light gray default

    // Drag state variables
    private Vector2 dragStartPosition; // Where the drag started in local panel coordinates
    private bool isDragging = false; // Is a drag currently active?
    private bool axisLocked = false; // Has the drag direction (horizontal/vertical) been determined?
    private bool isHorizontalDrag = false; // Is the locked axis horizontal?
    private int targetRow = -1; // Row index under the drag start point
    private int targetCol = -1; // Column index under the drag start point
    private float accumulatedDrag = 0f; // Accumulated drag distance along the locked axis since last scroll trigger
    private float cellSizeWithSpacing; // Calculated value for scroll threshold

    // --- State flags for delayed actions ---
    private bool scrollOccurredThisDrag = false; // Tracks if ANY scroll happened during the current drag
    private bool pendingValidationCheck = false; // Flag for word validation check
    private bool pendingMoveReduction = false;   // Flag for move reduction check
    private int moveReductionRow = -1;           // Row index for pending reduction
    private int moveReductionCol = -1;           // Column index for pending reduction

    // --- Highlight tracking ---
    // <<< MODIFIED: Store CellController for highlight reset >>>
    private List<CellController> currentlyHighlightedCells = new List<CellController>();
    private List<Image> highlightedImages = new List<Image>(); // Store Image components for color reset
    private List<Color> originalColors = new List<Color>();
    private Vector3 originalScale = Vector3.one;
    private bool isHighlightApplied = false;


    void Awake() // Use Awake for finding references
    {
        // Attempt to find references if not set
        if (wordGridManager == null) wordGridManager = FindFirstObjectByType<WordGridManager>();
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>(); // <<< Assuming GameManager ref is needed, add if missing
        if (gridPanelRect == null) gridPanelRect = GetComponent<RectTransform>(); // Fallback: Assume script is on the panel itself
        if (uiCamera == null) uiCamera = Camera.main; // Default to main camera

        // Validate references
        if (wordGridManager == null) { Debug.LogError("GridInputHandler: WordGridManager reference missing!", this); enabled = false; return; }
        if (gridPanelRect == null) { Debug.LogError("GridInputHandler: Grid Panel Rect reference missing!", this); enabled = false; return; }
        if (uiCamera == null) { Debug.LogError("GridInputHandler: UI Camera reference missing!", this); enabled = false; return; } // Needed for coordinate conversion
    }

    void Start()
    {
        // Pre-calculate cell size + spacing for efficiency
        if (wordGridManager != null) // Ensure grid manager exists
        {
            cellSizeWithSpacing = wordGridManager.cellSize + wordGridManager.spacing;
            // Debug.Log($"GridInputHandler Started. CellSizeWithSpacing: {cellSizeWithSpacing}");
        }
        else
        {
            Debug.LogError("GridInputHandler: Cannot calculate cellSizeWithSpacing in Start because WordGridManager is null!", this);
            enabled = false; // Disable if essential calculation fails
            return;
        }


        // --- Highlight Initialization ---
        // Store original scale once grid is likely initialized
        // <<< MODIFIED: Get scale from CellController >>>
        if (wordGridManager != null && wordGridManager.gridSize > 0)
        {
            CellController sampleCell = wordGridManager.GetCellController(new Vector2Int(0, 0));
            if (sampleCell != null && sampleCell.RectTransform != null)
            {
                originalScale = sampleCell.RectTransform.localScale;
            }
            else
            {
                Debug.LogWarning("GridInputHandler: Could not get CellController or RectTransform at [0,0] in Start to determine original scale.", this);
                originalScale = Vector3.one; // Default
            }
        }
        else
        {
            // Debug.LogWarning("GridInputHandler: Could not get original cell scale in Start. Grid might not be initialized yet or size is 0.", this);
            originalScale = Vector3.one; // Default
        }
        isHighlightApplied = false; // Ensure reset on start

        // Ensure flags are reset on start
        pendingValidationCheck = false;
        pendingMoveReduction = false;
        scrollOccurredThisDrag = false;
        moveReductionRow = -1;
        moveReductionCol = -1;
    }

    void OnEnable()
    {
        // Reset state when the component is enabled
        isDragging = false;
        axisLocked = false;
        pendingValidationCheck = false;
        pendingMoveReduction = false;
        scrollOccurredThisDrag = false;
        moveReductionRow = -1;
        moveReductionCol = -1;

        // Reset highlight state as well
        if (isHighlightApplied) // Reset visuals only if they were applied
        {
            ForceResetHighlightVisuals(); // Use the instant reset
        }
        isHighlightApplied = false; // Ensure flag is false
        ClearHighlightLists(); // Ensure lists are cleared

        // Debug.Log("GridInputHandler Enabled");
    }

    void OnDisable()
    {
        // Reset flags on disable
        pendingValidationCheck = false;
        pendingMoveReduction = false;
        scrollOccurredThisDrag = false;

        // Reset highlight state on disable
        if (isHighlightApplied)
        {
            // Instantly reset visuals if disabled while highlighted
            ForceResetHighlightVisuals();
        }
        // No need to clear lists here, OnEnable will handle it

        // Debug.Log("GridInputHandler Disabled");
    }

    void Update()
    {
        // --- Check for Pending Move Reduction ---
        // Process only if the flag is set AND the grid manager exists AND it's not animating
        if (pendingMoveReduction && wordGridManager != null && !wordGridManager.isAnimating)
        {
            pendingMoveReduction = false; // Reset the flag FIRST to prevent re-entry
            // Debug.Log($"GridInputHandler: Grid settled. Requesting pending move reduction for Row:{moveReductionRow} Col:{moveReductionCol}.", this);
            wordGridManager.ApplyPendingMoveReduction(moveReductionRow, moveReductionCol);
            moveReductionRow = -1; // Reset indices after applying
            moveReductionCol = -1;
        }

        // --- Check for Pending Validation ---
        // Process only if the flag is set AND the grid manager exists AND it's not animating
        if (pendingValidationCheck && wordGridManager != null && !wordGridManager.isAnimating)
        {
            pendingValidationCheck = false; // Reset the flag FIRST
            // Debug.Log("GridInputHandler: Grid settled. Requesting validation check.", this);
            wordGridManager.TriggerValidationCheck();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Ignore if component disabled, grid manager missing, or animating
        if (!enabled || wordGridManager == null || wordGridManager.isAnimating)
        {
            // Debug.Log($"OnBeginDrag ignored: Enabled={enabled}, Manager={wordGridManager != null}, Animating={wordGridManager?.isAnimating}");
            isDragging = false; return;
        }

        // Convert screen point to local point within the grid panel rect
        // Use the assigned uiCamera reference
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, eventData.position, uiCamera, out dragStartPosition))
        {
            // Debug.Log("OnBeginDrag ignored: Click outside Grid Panel Rect.");
            isDragging = false; return;
        }

        // --- Start the drag ---
        isDragging = true;
        axisLocked = false;
        accumulatedDrag = 0f;
        CalculateTargetRowCol(dragStartPosition); // Determine which cell (if any) the drag started on

        // --- Reset ALL pending flags and scroll tracking ---
        pendingValidationCheck = false;
        pendingMoveReduction = false;
        scrollOccurredThisDrag = false;
        moveReductionRow = -1;
        moveReductionCol = -1;

        // --- Highlight State Reset ---
        if (isHighlightApplied) // Reset previous highlight if it was somehow stuck
        {
            ForceResetHighlightVisuals();
        }
        isHighlightApplied = false; // Reset highlight flag
        ClearHighlightLists(); // Clear lists for the new drag

        // Store/update original scale reliably here
        // <<< MODIFIED: Get scale from CellController >>>
        if (wordGridManager != null && wordGridManager.gridSize > 0)
        {
            CellController sampleCell = wordGridManager.GetCellController(new Vector2Int(0, 0));
            if (sampleCell != null && sampleCell.RectTransform != null)
            {
                originalScale = sampleCell.RectTransform.localScale;
                // Debug.Log($"OnBeginDrag: Updated originalScale to {originalScale}", this);
            }
            else { originalScale = Vector3.one; /* Default */ }
        }


        // Debug.Log($"OnBeginDrag: Started at Local Pos {dragStartPosition}. Target (R:{targetRow}, C:{targetCol}). Pending flags reset.");
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Ignore if not dragging or if grid starts animating mid-drag
        if (!isDragging || wordGridManager == null || wordGridManager.isAnimating) { return; }

        // Get current pointer position in local panel coordinates
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, eventData.position, uiCamera, out Vector2 currentLocalPos))
        {
            // Debug.Log("OnDrag: Dragged outside Grid Panel Rect, ending drag.");
            OnEndDrag(eventData); // Treat dragging outside as ending the drag
            return;
        }

        Vector2 dragVector = currentLocalPos - dragStartPosition;

        // --- Axis Locking Logic ---
        if (!axisLocked)
        {
            // Check if drag distance exceeds the threshold
            if (dragVector.magnitude > dragThreshold)
            {
                axisLocked = true;
                // Determine primary direction
                isHorizontalDrag = Mathf.Abs(dragVector.x) > Mathf.Abs(dragVector.y);
                // Debug.Log($"OnDrag: Axis Locked - {(isHorizontalDrag ? "Horizontal" : "Vertical")}. Target (R:{targetRow}, C:{targetCol})");

                // <<< Apply highlight AFTER axis is locked >>>
                if (enableDragHighlight)
                {
                    // Check if drag started on a valid cell before highlighting
                    if (targetRow != -1 && targetCol != -1)
                    {
                        if (isHorizontalDrag)
                        {
                            ApplyHighlightRow(targetRow);
                        }
                        else // Vertical drag
                        {
                            ApplyHighlightColumn(targetCol);
                        }
                    }
                    else
                    {
                        // Debug.LogWarning("OnDrag: Axis locked, but drag didn't start on a valid cell. Cannot apply highlight.");
                    }
                }
            }
            else { return; } // Don't process scroll or highlight until axis is locked
        }

        // --- Scroll Triggering Logic (Only if axis is locked) ---
        if (axisLocked)
        {
            // Use the drag amount along the locked axis
            float dragAmount = isHorizontalDrag ? dragVector.x : dragVector.y;
            float previousAccumulated = accumulatedDrag;
            accumulatedDrag = dragAmount; // Update accumulated drag for this frame

            // Calculate the threshold for triggering a scroll
            float scrollThreshold = cellSizeWithSpacing * scrollThresholdFactor;
            int scrollDirection = 0;

            // Check if the accumulated drag crossed the positive threshold
            if (previousAccumulated < scrollThreshold && accumulatedDrag >= scrollThreshold)
            {
                scrollDirection = isHorizontalDrag ? 1 : -1; // Right (X+) or Up (Y-)
                accumulatedDrag -= scrollThreshold; // Consume threshold amount
                dragStartPosition = currentLocalPos; // Reset start position for next potential scroll check
            }
            // Check if the accumulated drag crossed the negative threshold
            else if (previousAccumulated > -scrollThreshold && accumulatedDrag <= -scrollThreshold)
            {
                scrollDirection = isHorizontalDrag ? -1 : 1; // Left (X-) or Down (Y+)
                accumulatedDrag += scrollThreshold; // Consume threshold amount (add because it's negative)
                dragStartPosition = currentLocalPos; // Reset start position
            }

            // --- Request Scroll if Triggered ---
            if (scrollDirection != 0)
            {
                // Ensure drag started on a valid cell before requesting scroll
                if (targetRow == -1 || targetCol == -1)
                {
                    Debug.LogWarning($"Scroll triggered but drag start target invalid (R:{targetRow}, C:{targetCol}). Ignoring scroll request.");
                    return; // Do not proceed with scroll request
                }

                scrollOccurredThisDrag = true; // Set flag indicating a scroll happened
                // Debug.Log($"OnDrag: Scroll triggered. scrollOccurredThisDrag = true.");

                // Cancel any pending actions (safety) before requesting new animation
                if (pendingValidationCheck) { /* Debug.Log("OnDrag: Scroll triggered, resetting pendingValidationCheck.") */ pendingValidationCheck = false; }
                if (pendingMoveReduction) { /* Debug.Log("OnDrag: Scroll triggered, resetting pendingMoveReduction.") */ pendingMoveReduction = false; moveReductionRow = -1; moveReductionCol = -1; }

                // Call the scroll request method on WordGridManager
                if (isHorizontalDrag)
                {
                    // Debug.Log($"--> Requesting Row Scroll: Row {targetRow}, Dir {scrollDirection}");
                    wordGridManager.RequestRowScroll(targetRow, scrollDirection, 0f); // Pass 0 for amount (not used)
                }
                else // Vertical drag
                {
                    // Debug.Log($"--> Requesting Col Scroll: Col {targetCol}, Dir {scrollDirection}");
                    wordGridManager.RequestColumnScroll(targetCol, scrollDirection, 0f); // Pass 0 for amount (not used)
                }
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // <<< Reset highlight FIRST >>>
        if (enableDragHighlight && isHighlightApplied)
        {
            ResetHighlight(); // Instantly reset visuals
        }

        if (!isDragging) return; // Ignore if drag wasn't properly started

        // Debug.Log($"OnEndDrag: Drag finished. Target Start (R:{targetRow}, C:{targetCol}). scrollOccurredThisDrag={scrollOccurredThisDrag}", this);

        // --- Set Pending Flags (only if grid manager exists) ---
        if (wordGridManager != null)
        {
            // 1. Pending Move Reduction (IF a scroll actually occurred during the drag)
            if (scrollOccurredThisDrag)
            {
                // Determine the row/col based on the locked axis (if axis was locked)
                if (axisLocked)
                {
                    pendingMoveReduction = true; // Set flag only if scroll occurred AND axis locked
                    if (isHorizontalDrag) { moveReductionRow = targetRow; moveReductionCol = -1; }
                    else { moveReductionRow = -1; moveReductionCol = targetCol; }

                    // Debug.Log($"OnEndDrag: Scroll occurred. Set pendingMoveReduction=true for Row:{moveReductionRow} Col:{moveReductionCol}. Update() will handle.", this);
                }
                else
                {
                    // This case should ideally not happen if scrollOccurredThisDrag is true, but log a warning
                    Debug.LogWarning("OnEndDrag: Scroll occurred but axis was not locked? Cannot reliably set pending move reduction target.");
                    pendingMoveReduction = false; // Don't set flag if target is unclear
                }
            }
            // else { Debug.Log($"OnEndDrag: No scroll occurred. No move reduction pending.", this); }

            // 2. Pending Validation (Always set after a drag ends, Update() handles the check)
            pendingValidationCheck = true;
            // Debug.Log("OnEndDrag: Set pendingValidationCheck=true. Update() will handle.", this);
        }


        // --- Reset Drag State ---
        isDragging = false;
        axisLocked = false;
        accumulatedDrag = 0f;
        scrollOccurredThisDrag = false; // Reset scroll tracking for the next drag
        targetRow = -1; // Reset target cell
        targetCol = -1;
    }

    // Calculates the target row and column based on the local position within the grid panel
    void CalculateTargetRowCol(Vector2 localPosition)
    {
        if (wordGridManager == null || cellSizeWithSpacing <= 0)
        {
            targetRow = -1;
            targetCol = -1;
            // Debug.LogError("CalculateTargetRowCol: Cannot calculate - WordGridManager null or cellSizeWithSpacing invalid.");
            return;
        }

        // Calculate total size and start offset based on grid settings
        float totalGridSizeUI = wordGridManager.gridSize * wordGridManager.cellSize + (wordGridManager.gridSize - 1) * wordGridManager.spacing;
        float gridStartX = -totalGridSizeUI / 2f; // Assuming grid is centered
        float gridStartY = -totalGridSizeUI / 2f; // Assuming grid is centered

        // Calculate position relative to the grid's bottom-left corner (in local panel space)
        float relativeX = localPosition.x - gridStartX;
        float relativeY = localPosition.y - gridStartY;

        // Determine column and row based on relative position and cell size + spacing
        int calculatedCol = Mathf.FloorToInt(relativeX / cellSizeWithSpacing);
        int calculatedRow = Mathf.FloorToInt(relativeY / cellSizeWithSpacing); // Row calculation depends on grid layout origin

        // Invert Row if Y=0 is bottom (common UI setup)
        // If using default Unity UI layout where Y increases upwards:
        calculatedRow = wordGridManager.gridSize - 1 - calculatedRow;

        // Clamp values to be within valid grid indices
        targetCol = Mathf.Clamp(calculatedCol, 0, wordGridManager.gridSize - 1);
        targetRow = Mathf.Clamp(calculatedRow, 0, wordGridManager.gridSize - 1);

        // Additional check: Ensure the click is actually within the cell bounds, not just the spacing area
        // This requires checking the local point within the specific cell's RectTransform, which is more complex here.
        // For simplicity, this basic calculation assumes clicking anywhere within the 'slot' corresponds to the cell.
        // A more precise check would involve GetCellCoordinatesFromScreenPoint logic if needed.

        // Check if calculated indices fall outside the grid bounds *before* clamping,
        // indicating a click in the panel but outside the grid cells area.
        if (calculatedCol < 0 || calculatedCol >= wordGridManager.gridSize || calculatedRow < 0 || calculatedRow >= wordGridManager.gridSize)
        {
            // Drag started outside the actual cell area
            targetRow = -1;
            targetCol = -1;
            // Debug.Log("CalculateTargetRowCol: Drag started outside grid cell bounds.");
        }
    }


    // --- Highlight Methods (Using CellController) ---

    private void ApplyHighlightRow(int rowIndex)
    {
        if (isHighlightApplied || wordGridManager == null || rowIndex < 0 || rowIndex >= wordGridManager.gridSize) return;
        // Debug.Log($"Applying highlight to row {rowIndex}");

        ClearHighlightLists(); // Ensure lists are empty before adding

        for (int c = 0; c < wordGridManager.gridSize; c++)
        {
            CellController cellController = wordGridManager.GetCellController(new Vector2Int(rowIndex, c));
            if (cellController == null || cellController.RectTransform == null) continue;

            RectTransform cellRect = cellController.RectTransform;

            // --- Apply Scale ---
            cellRect.localScale = originalScale * highlightScaleMultiplier;
            currentlyHighlightedCells.Add(cellController); // Store CellController

            // --- Apply Color ---
            // Try to get Image component from the CellController's GameObject or its children
            Image cellImage = cellController.GetComponent<Image>() ?? cellController.GetComponentInChildren<Image>();
            if (cellImage != null)
            {
                highlightedImages.Add(cellImage);
                originalColors.Add(cellImage.color); // Store original color
                cellImage.color = highlightColor;    // Apply highlight color
            }
            else
            {
                highlightedImages.Add(null); // Keep lists aligned even if no image found
                originalColors.Add(Color.clear); // Add placeholder color
                // Debug.LogWarning($"Could not find Image component on cell [{rowIndex},{c}] for highlight.", cellController.gameObject);
            }
        }
        isHighlightApplied = true;
    }

    private void ApplyHighlightColumn(int colIndex)
    {
        if (isHighlightApplied || wordGridManager == null || colIndex < 0 || colIndex >= wordGridManager.gridSize) return;
        // Debug.Log($"Applying highlight to column {colIndex}");

        ClearHighlightLists(); // Ensure lists are empty

        for (int r = 0; r < wordGridManager.gridSize; r++)
        {
            CellController cellController = wordGridManager.GetCellController(new Vector2Int(r, colIndex));
            if (cellController == null || cellController.RectTransform == null) continue;

            RectTransform cellRect = cellController.RectTransform;

            // --- Apply Scale ---
            cellRect.localScale = originalScale * highlightScaleMultiplier;
            currentlyHighlightedCells.Add(cellController); // Store CellController

            // --- Apply Color ---
            Image cellImage = cellController.GetComponent<Image>() ?? cellController.GetComponentInChildren<Image>();
            if (cellImage != null)
            {
                highlightedImages.Add(cellImage);
                originalColors.Add(cellImage.color);
                cellImage.color = highlightColor;
            }
            else
            {
                highlightedImages.Add(null); // Keep lists aligned
                originalColors.Add(Color.clear);
                // Debug.LogWarning($"Could not find Image component on cell [{r},{colIndex}] for highlight.", cellController.gameObject);
            }
        }
        isHighlightApplied = true;
    }

    // --- Reset methods (Instant) ---
    private void ResetHighlight()
    {
        ForceResetHighlightVisuals(); // Use the instant reset method
    }

    // Instantly resets visuals (used by ResetHighlight and OnDisable/OnEnable)
    private void ForceResetHighlightVisuals()
    {
        if (!isHighlightApplied) return; // Only reset if applied
        // Debug.Log($"Instantly resetting highlight visuals for {currentlyHighlightedCells.Count} cells.");

        // Use the stored CellControllers to reset scale and color
        for (int i = 0; i < currentlyHighlightedCells.Count; i++)
        {
            CellController cellController = currentlyHighlightedCells[i];
            if (cellController != null && cellController.RectTransform != null)
            {
                // Set Scale Instantly
                cellController.RectTransform.localScale = originalScale;

                // Set Color Instantly using stored Image and original Color
                if (i < highlightedImages.Count && highlightedImages[i] != null && i < originalColors.Count)
                {
                    highlightedImages[i].color = originalColors[i];
                }
            }
        }
        isHighlightApplied = false; // Mark as not applied AFTER resetting all
        ClearHighlightLists(); // Clear lists after resetting
    }


    private void ClearHighlightLists()
    {
        currentlyHighlightedCells.Clear();
        highlightedImages.Clear();
        originalColors.Clear();
    }

    // <<< Reference to GameManager, if needed for state checks (add to Header if required) >>>
    [SerializeField] private GameManager gameManager;

} // End of GridInputHandler class