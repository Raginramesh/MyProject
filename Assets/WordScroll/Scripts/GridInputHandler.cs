using UnityEngine;
using UnityEngine.EventSystems; // Required for drag interfaces (IBeginDragHandler, etc.)
using UnityEngine.UI; // Required for Image component (for highlight color)
using System.Collections.Generic; // Required for List (for highlight tracking)

public class GridInputHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("References")]
    [SerializeField] private WordGridManager wordGridManager; // Manages the grid data and animations
    [SerializeField] private GameManager gameManager; // Manages game state and animation flags
    [SerializeField] private RectTransform gridPanelRect; // The UI Panel RectTransform that receives drag events
    [SerializeField] private Camera uiCamera; // Camera for ScreenPointToLocalPointInRectangle (usually Main Camera for Screen Space - Camera canvas)

    [Header("Drag Settings")]
    [Tooltip("Minimum screen distance the pointer must move before a drag direction is locked.")]
    [SerializeField] private float dragThreshold = 20f;
    [Tooltip("Factor of (cell size + spacing) the pointer must move along the locked axis to trigger a scroll action.")]
    [SerializeField] private float scrollThresholdFactor = 0.4f;

    [Header("Highlight Settings")]
    [Tooltip("If true, the dragged row/column will be visually highlighted.")]
    [SerializeField] private bool enableDragHighlight = true;
    [Tooltip("Scale multiplier applied to highlighted cells (e.g., 1.0 means no change, 1.1 is 10% bigger).")]
    [SerializeField] private float highlightScaleMultiplier = 1.08f;
    [Tooltip("Color tint applied to the Image component of highlighted cells.")]
    [SerializeField] private Color highlightColor = new Color(0.9f, 0.9f, 0.9f, 1f); // Light gray default

    // --- Drag State Variables ---
    private Vector2 dragStartPosition; // Where the drag started in local panel coordinates
    private bool isDragging = false; // Is a drag currently active?
    private bool axisLocked = false; // Has the drag direction (horizontal/vertical) been determined for this drag?
    private bool isHorizontalDrag = false; // Is the locked axis horizontal? (Only valid if axisLocked is true)
    private int targetRow = -1; // Grid row index under the drag start point (-1 if not on grid)
    private int targetCol = -1; // Grid column index under the drag start point (-1 if not on grid)
    private float accumulatedDrag = 0f; // Accumulated drag distance along the locked axis since the last scroll trigger
    private float cellSizeWithSpacing; // Cached value: wordGridManager.cellSize + wordGridManager.spacing

    // --- State Flags for Delayed Actions (Processed in Update) ---
    private bool scrollOccurredThisDrag = false; // Tracks if ANY scroll happened during the current drag session
    private bool pendingValidationCheck = false; // Flag: Trigger word validation when grid settles?
    private bool pendingMoveReduction = false;   // Flag: Apply move reduction when grid settles?
    private int moveReductionRow = -1;           // Target row for pending move reduction
    private int moveReductionCol = -1;           // Target column for pending move reduction

    // --- Highlight Tracking Variables ---
    private List<CellController> currentlyHighlightedCells = new List<CellController>(); // Stores controllers for reset
    private List<Image> highlightedImages = new List<Image>(); // Stores Image components for color reset
    private List<Color> originalColors = new List<Color>();    // Stores original colors of images
    private Vector3 originalScale = Vector3.one;               // Original scale of cells (assumed uniform)
    private bool isHighlightApplied = false;                   // Is highlight currently active?


    // --- Initialization ---
    void Awake()
    {
        // Attempt to find references if not assigned in Inspector
        if (wordGridManager == null) wordGridManager = FindFirstObjectByType<WordGridManager>();
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (gridPanelRect == null) gridPanelRect = GetComponent<RectTransform>(); // Assume script is on the panel if not assigned
        if (uiCamera == null) uiCamera = Camera.main; // Default to main camera

        // --- Validate Critical References ---
        if (wordGridManager == null) { Debug.LogError("GridInputHandler: WordGridManager reference missing!", this); enabled = false; return; }
        if (gameManager == null) { Debug.LogError("GridInputHandler: GameManager reference missing!", this); enabled = false; return; }
        if (gridPanelRect == null) { Debug.LogError("GridInputHandler: Grid Panel Rect reference missing!", this); enabled = false; return; }
        if (uiCamera == null) { Debug.LogError("GridInputHandler: UI Camera reference missing!", this); enabled = false; return; }
    }

    void Start()
    {
        // Pre-calculate cell size + spacing (ensure WordGridManager is available)
        if (wordGridManager != null)
        {
            cellSizeWithSpacing = wordGridManager.cellSize + wordGridManager.spacing;
            // Debug.Log($"GridInputHandler Started. CellSizeWithSpacing: {cellSizeWithSpacing}");
        }
        else
        {
            Debug.LogError("GridInputHandler: Cannot calculate cellSizeWithSpacing in Start - WordGridManager is null!", this);
            enabled = false; // Disable if critical calculation fails
            return;
        }

        // --- Highlight Initialization: Get Original Scale ---
        UpdateOriginalScale(); // Get initial scale
        isHighlightApplied = false; // Ensure reset on start

        // Ensure flags are reset on start
        ResetPendingFlags();
        scrollOccurredThisDrag = false;
    }

    // --- Enable / Disable Handling ---
    void OnEnable()
    {
        // Reset state when the component is enabled (e.g., after scene load or re-activation)
        ResetDragState();
        ResetPendingFlags();
        scrollOccurredThisDrag = false;

        // Reset highlight state as well
        if (isHighlightApplied) ForceResetHighlightVisuals(); // Reset visuals if enabled while highlighted
        isHighlightApplied = false;
        ClearHighlightLists();

        // Debug.Log("GridInputHandler Enabled");
    }

    void OnDisable()
    {
        // Reset flags and state on disable
        ResetDragState();
        ResetPendingFlags();
        scrollOccurredThisDrag = false;

        // Reset highlight state instantly if disabled while highlighted
        if (isHighlightApplied) ForceResetHighlightVisuals();
        // No need to clear lists here, OnEnable handles it

        // Debug.Log("GridInputHandler Disabled");
    }

    // --- Update Loop for Delayed Actions ---
    void Update()
    {
        // --- IMPORTANT: Check Game State and Animation ---
        // Do not process pending actions if game isn't playing or animations are running
        if (gameManager == null || gameManager.CurrentState != GameManager.GameState.Playing || gameManager.IsAnyAnimationPlaying)
        {
            return;
        }
        // --- END CHECK ---


        // --- Process Pending Move Reduction ---
        // Check flag AND ensure WordGridManager exists and is NOT animating its scroll
        if (pendingMoveReduction && wordGridManager != null && !wordGridManager.isAnimating)
        {
            pendingMoveReduction = false; // Reset the flag FIRST to prevent re-entry
            // Debug.Log($"GridInputHandler Update: Applying pending move reduction for Row:{moveReductionRow} Col:{moveReductionCol}.", this);
            wordGridManager.ApplyPendingMoveReduction(moveReductionRow, moveReductionCol);
            moveReductionRow = -1; // Reset target indices
            moveReductionCol = -1;
        }

        // --- Process Pending Validation Check ---
        // Check flag AND ensure WordGridManager exists and is NOT animating its scroll
        if (pendingValidationCheck && wordGridManager != null && !wordGridManager.isAnimating)
        {
            pendingValidationCheck = false; // Reset the flag FIRST
            // Debug.Log("GridInputHandler Update: Applying pending validation check.", this);
            wordGridManager.TriggerValidationCheck();
        }
    }


    // --- Input Handling (Event System Interfaces) ---

    public void OnBeginDrag(PointerEventData eventData)
    {
        // --- Pre-Drag Checks ---
        if (!enabled || wordGridManager == null || gameManager == null ||
            gameManager.CurrentState != GameManager.GameState.Playing || gameManager.IsAnyAnimationPlaying)
        {
            isDragging = false;
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, eventData.position, uiCamera, out dragStartPosition))
        {
            isDragging = false; return;
        }

        // --- Start the Drag Process ---
        isDragging = true;
        axisLocked = false;
        accumulatedDrag = 0f;
        CalculateTargetRowCol(dragStartPosition);

        // --- Reset ONLY Pending Action Flags and Scroll Tracking ---
        ResetPendingFlags();
        scrollOccurredThisDrag = false;

        // --- REMOVED Highlight Reset from here ---
        // if (isHighlightApplied) ForceResetHighlightVisuals(); // REMOVED
        // isHighlightApplied = false;                           // REMOVED
        // ClearHighlightLists();                                // REMOVED
        UpdateOriginalScale(); // Keep this - ensures correct scale is used for highlighting

        // Debug.Log($"OnBeginDrag: Started at Local Pos {dragStartPosition}. Target (R:{targetRow}, C:{targetCol}). Pending flags reset.");
    }

    public void OnDrag(PointerEventData eventData)
    {
        // --- Pre-Drag Checks ---
        // Ignore if not dragging or critical refs missing
        if (!isDragging || wordGridManager == null || gameManager == null)
        {
            // If not dragging, ensure state is clean (though should be handled by OnEndDrag)
            if (!isDragging) ResetDragState();
            return;
        }

        // --- <<< CHANGE HERE >>> ---
        // If game isn't playing OR an animation is currently running,
        // *ignore* further drag updates for this frame, but DON'T end the drag yet.
        // The actual OnEndDrag event will handle cleanup when the user releases.
        if (gameManager.CurrentState != GameManager.GameState.Playing || gameManager.IsAnyAnimationPlaying)
        {
            // Debug.Log($"OnDrag: Ignoring drag update (State={gameManager.CurrentState}, Animating={gameManager.IsAnyAnimationPlaying})");
            return; // Just return, don't call OnEndDrag
        }
        // --- <<< END CHANGE >>> ---


        // Get current pointer position in local panel coordinates
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, eventData.position, uiCamera, out Vector2 currentLocalPos))
        {
            // Debug.Log("OnDrag: Dragged outside Grid Panel Rect, ending drag.");
            // If dragged outside, THEN we can end the drag prematurely.
            OnEndDrag(eventData);
            return;
        }

        Vector2 dragVector = currentLocalPos - dragStartPosition; // Vector from start to current position

        // --- Axis Locking Logic ---
        // (Rest of the OnDrag method remains the same as before)
        if (!axisLocked)
        {
            if (dragVector.magnitude > dragThreshold)
            {
                axisLocked = true;
                isHorizontalDrag = Mathf.Abs(dragVector.x) > Mathf.Abs(dragVector.y);
                if (enableDragHighlight)
                {
                    if (targetRow != -1 && targetCol != -1)
                    {
                        if (isHorizontalDrag) ApplyHighlightRow(targetRow);
                        else ApplyHighlightColumn(targetCol);
                    }
                }
            }
            else { return; }
        }

        // --- Scroll Triggering Logic (Only if axis is locked) ---
        if (axisLocked)
        {
            float dragAmount = isHorizontalDrag ? dragVector.x : dragVector.y;
            float previousAccumulated = accumulatedDrag;
            accumulatedDrag = dragAmount;
            float scrollTriggerDistance = cellSizeWithSpacing * scrollThresholdFactor;
            int scrollDirection = 0;

            if (previousAccumulated < scrollTriggerDistance && accumulatedDrag >= scrollTriggerDistance)
            {
                scrollDirection = isHorizontalDrag ? 1 : -1;
                accumulatedDrag -= scrollTriggerDistance;
                dragStartPosition = currentLocalPos;
            }
            else if (previousAccumulated > -scrollTriggerDistance && accumulatedDrag <= -scrollTriggerDistance)
            {
                scrollDirection = isHorizontalDrag ? -1 : 1;
                accumulatedDrag += scrollTriggerDistance;
                dragStartPosition = currentLocalPos;
            }

            if (scrollDirection != 0)
            {
                if (targetRow == -1 || targetCol == -1) { return; } // Ignore if start target invalid
                scrollOccurredThisDrag = true;
                ResetPendingFlags(); // Cancel pending actions before new animation

                if (isHorizontalDrag)
                {
                    wordGridManager.RequestRowScroll(targetRow, scrollDirection, 0f);
                }
                else
                {
                    wordGridManager.RequestColumnScroll(targetCol, scrollDirection, 0f);
                }
            }
        }
    } // End of OnDrag

    public void OnEndDrag(PointerEventData eventData)
    {
        // --- Reset Highlight FIRST ---
        // Always reset highlight when drag ends, regardless of other conditions
        if (enableDragHighlight && isHighlightApplied)
        {
            ResetHighlight(); // Instantly reset visuals
        }

        // Ignore if drag wasn't properly started or refs missing
        if (!isDragging || wordGridManager == null)
        {
            // Ensure drag state is reset even if ignored early
            ResetDragState();
            return;
        }

        // Debug.Log($"OnEndDrag: Drag finished. Target Start (R:{targetRow}, C:{targetCol}). scrollOccurredThisDrag={scrollOccurredThisDrag}", this);

        // --- Set Pending Flags (based on whether a scroll happened) ---
        // These flags will be processed by Update() when animations are complete

        // 1. Pending Move Reduction (Only if a scroll actually occurred)
        if (scrollOccurredThisDrag)
        {
            // Determine the target row/col based on the locked axis
            if (axisLocked) // Ensure axis was determined
            {
                pendingMoveReduction = true; // Set flag
                if (isHorizontalDrag) { moveReductionRow = targetRow; moveReductionCol = -1; }
                else { moveReductionRow = -1; moveReductionCol = targetCol; }
                // Debug.Log($"OnEndDrag: Scroll occurred. Set pendingMoveReduction=true for Row:{moveReductionRow} Col:{moveReductionCol}. Update() will handle.", this);
            }
            else { Debug.LogWarning("OnEndDrag: Scroll occurred but axis wasn't locked? Cannot set pending move reduction target."); }
        }
        // else { Debug.Log($"OnEndDrag: No scroll occurred. No move reduction pending.", this); }

        // 2. Pending Validation Check (Always set after drag ends)
        // Update() will wait for animations to finish before triggering validation.
        pendingValidationCheck = true;
        // Debug.Log("OnEndDrag: Set pendingValidationCheck=true. Update() will handle.", this);


        // --- Reset Drag State Variables ---
        ResetDragState();
        scrollOccurredThisDrag = false; // Reset scroll tracking for the next drag session
    }


    // --- Helper Methods ---

    // Calculates the target row and column based on the local position within the grid panel
    void CalculateTargetRowCol(Vector2 localPosition)
    {
        // Reset target first
        targetRow = -1;
        targetCol = -1;

        if (wordGridManager == null || cellSizeWithSpacing <= 0)
        {
            // Debug.LogError("CalculateTargetRowCol: Cannot calculate - WordGridManager null or cellSizeWithSpacing invalid.");
            return;
        }

        // Calculate total size and start offset based on grid settings (assuming centered grid)
        float totalGridSizeUI = wordGridManager.gridSize * wordGridManager.cellSize + (wordGridManager.gridSize - 1) * wordGridManager.spacing;
        float gridOriginOffsetX = -totalGridSizeUI / 2f; // Offset from panel center to grid logical bottom-left
        float gridOriginOffsetY = -totalGridSizeUI / 2f;

        // Calculate position relative to the grid's logical bottom-left corner
        float relativeX = localPosition.x - gridOriginOffsetX;
        float relativeY = localPosition.y - gridOriginOffsetY;

        // Determine column and row based on relative position
        int calculatedCol = Mathf.FloorToInt(relativeX / cellSizeWithSpacing);
        int calculatedRowBasedOnBottom = Mathf.FloorToInt(relativeY / cellSizeWithSpacing);

        // Check if the calculated indices are outside the valid range [0, gridSize-1]
        if (calculatedCol < 0 || calculatedCol >= wordGridManager.gridSize || calculatedRowBasedOnBottom < 0 || calculatedRowBasedOnBottom >= wordGridManager.gridSize)
        {
            // Drag started outside the actual grid cell area (in spacing or beyond)
            // Debug.Log("CalculateTargetRowCol: Drag started outside grid cell bounds.");
            return; // Keep targetRow/Col as -1
        }

        // If within bounds, assign the calculated column
        targetCol = calculatedCol;
        // Invert Row index because UI Y typically increases upwards, but grid arrays often start row 0 at the top
        targetRow = wordGridManager.gridSize - 1 - calculatedRowBasedOnBottom;

        // Final clamp for safety, although the check above should handle bounds
        targetCol = Mathf.Clamp(targetCol, 0, wordGridManager.gridSize - 1);
        targetRow = Mathf.Clamp(targetRow, 0, wordGridManager.gridSize - 1);
    }

    // Resets all flags related to pending actions
    private void ResetPendingFlags()
    {
        pendingValidationCheck = false;
        pendingMoveReduction = false;
        moveReductionRow = -1;
        moveReductionCol = -1;
    }

    // Resets the core state variables related to an active drag
    private void ResetDragState()
    {
        isDragging = false;
        axisLocked = false;
        accumulatedDrag = 0f;
        // Keep targetRow/Col as they are reset in OnEndDrag or OnBeginDrag
    }

    // Updates the cached originalScale value
    private void UpdateOriginalScale()
    {
        if (wordGridManager != null && wordGridManager.gridSize > 0)
        {
            CellController sampleCell = wordGridManager.GetCellController(new Vector2Int(0, 0));
            if (sampleCell != null && sampleCell.RectTransform != null)
            {
                originalScale = sampleCell.RectTransform.localScale;
            }
            else { originalScale = Vector3.one; /* Default */ }
        }
        else { originalScale = Vector3.one; /* Default */ }
    }


    // --- Highlight Methods (Using CellController, Instant Apply/Reset) ---

    // Applies highlight instantly to a row
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
            Image cellImage = cellController.GetComponent<Image>() ?? cellController.GetComponentInChildren<Image>();
            if (cellImage != null)
            {
                highlightedImages.Add(cellImage);
                originalColors.Add(cellImage.color);
                cellImage.color = highlightColor;
            }
            else { highlightedImages.Add(null); originalColors.Add(Color.clear); } // Keep lists aligned
        }
        isHighlightApplied = true;
    }

    // Applies highlight instantly to a column
    private void ApplyHighlightColumn(int colIndex)
    {
        if (isHighlightApplied || wordGridManager == null || colIndex < 0 || colIndex >= wordGridManager.gridSize) return;
        // Debug.Log($"Applying highlight to column {colIndex}");

        ClearHighlightLists();

        for (int r = 0; r < wordGridManager.gridSize; r++)
        {
            CellController cellController = wordGridManager.GetCellController(new Vector2Int(r, colIndex));
            if (cellController == null || cellController.RectTransform == null) continue;

            RectTransform cellRect = cellController.RectTransform;

            // --- Apply Scale ---
            cellRect.localScale = originalScale * highlightScaleMultiplier;
            currentlyHighlightedCells.Add(cellController);

            // --- Apply Color ---
            Image cellImage = cellController.GetComponent<Image>() ?? cellController.GetComponentInChildren<Image>();
            if (cellImage != null)
            {
                highlightedImages.Add(cellImage);
                originalColors.Add(cellImage.color);
                cellImage.color = highlightColor;
            }
            else { highlightedImages.Add(null); originalColors.Add(Color.clear); } // Keep lists aligned
        }
        isHighlightApplied = true;
    }

    // --- Reset Highlight Methods ---

    // Public method called by OnEndDrag to reset highlight
    private void ResetHighlight()
    {
        ForceResetHighlightVisuals(); // Use the instant reset method
    }

    // Instantly resets visuals (used by ResetHighlight, OnDisable, OnEnable, OnBeginDrag)
    private void ForceResetHighlightVisuals()
    {
        if (!isHighlightApplied) return; // Only reset if applied

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

    // Clears the lists used for tracking highlighted elements
    private void ClearHighlightLists()
    {
        currentlyHighlightedCells.Clear();
        highlightedImages.Clear();
        originalColors.Clear();
    }

} // End of GridInputHandler class