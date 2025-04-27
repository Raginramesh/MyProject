using UnityEngine;
using UnityEngine.EventSystems; // Required for drag interfaces

public class GridInputHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("References")]
    [SerializeField] private WordGridManager wordGridManager; // Reference to the grid manager
    [SerializeField] private RectTransform gridPanelRect; // The UI Panel RectTransform that receives drag events

    [Header("Settings")]
    [SerializeField] private float dragThreshold = 20f; // Minimum distance drag before locking axis
    [SerializeField] private float scrollThresholdFactor = 0.4f; // Factor of cell size+spacing to trigger a scroll

    // Drag state variables
    private Vector2 dragStartPosition; // Where the drag started in local panel coordinates
    private bool isDragging = false; // Is a drag currently active?
    private bool axisLocked = false; // Has the drag direction (horizontal/vertical) been determined?
    private bool isHorizontalDrag = false; // Is the locked axis horizontal?
    private int targetRow = -1; // Row index under the drag start point
    private int targetCol = -1; // Column index under the drag start point
    private float accumulatedDrag = 0f; // Accumulated drag distance along the locked axis since last scroll trigger
    private float cellSizeWithSpacing; // Calculated value for scroll threshold

    // --- NEW: Flag to check for validation after drag ends ---
    private bool pendingValidationCheck = false; // Set to true on drag end, checked in Update

    void Start()
    {
        // Validate references
        if (wordGridManager == null)
        {
            Debug.LogError("GridInputHandler: WordGridManager reference missing!", this);
            enabled = false; return;
        }
        if (gridPanelRect == null)
        {
            Debug.LogError("GridInputHandler: Grid Panel Rect reference missing! Assign the UI panel for input.", this);
            enabled = false; return;
        }
        // Pre-calculate cell size + spacing for efficiency
        cellSizeWithSpacing = wordGridManager.cellSize + wordGridManager.spacing;
        Debug.Log($"GridInputHandler Started. CellSizeWithSpacing: {cellSizeWithSpacing}");
        pendingValidationCheck = false; // Ensure flag is false on start
    }

    void OnEnable()
    {
        // Reset state when the component is enabled (e.g., after game restart)
        isDragging = false;
        axisLocked = false;
        accumulatedDrag = 0f;
        pendingValidationCheck = false; // Reset flag when enabled
        Debug.Log("GridInputHandler Enabled");
    }

    void OnDisable()
    {
        // Log when disabled and reset flag
        Debug.Log("GridInputHandler Disabled");
        pendingValidationCheck = false;
    }

    // --- NEW: Update method to check for pending validation ---
    void Update()
    {
        // Check conditions: Validation is pending? Grid manager exists? Grid manager is NOT animating?
        if (pendingValidationCheck && wordGridManager != null && !wordGridManager.isAnimating)
        {
            // --- Conditions met: Trigger Validation ---
            pendingValidationCheck = false; // Reset the flag FIRST to prevent multiple triggers in the same frame
            Debug.Log("GridInputHandler: Grid settled after drag. Requesting validation check.", this);
            // Call the public method in WordGridManager to start the validation process
            wordGridManager.TriggerValidationCheck();
        }
    }

    // Called once when a drag gesture starts on the gridPanelRect
    public void OnBeginDrag(PointerEventData eventData)
    {
        // Ignore if component disabled or grid manager missing
        if (!enabled || wordGridManager == null)
        {
            Debug.LogWarning("OnBeginDrag ignored: GridInputHandler disabled or WordGridManager missing.");
            return;
        }

        // Ignore if grid is currently animating (scrolling or replacing)
        if (wordGridManager.isAnimating)
        {
            Debug.Log("OnBeginDrag ignored: WordGridManager is animating.");
            isDragging = false; // Ensure dragging flag is false if ignored
            return;
        }

        // Convert screen point to local point within the grid panel's RectTransform
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, eventData.position, eventData.pressEventCamera, out dragStartPosition))
        {
            Debug.Log("OnBeginDrag ignored: Click outside Grid Panel Rect bounds.");
            isDragging = false;
            return;
        }

        // --- If checks pass, start the drag ---
        isDragging = true;
        axisLocked = false; // Reset axis lock
        accumulatedDrag = 0f; // Reset accumulated distance
        CalculateTargetRowCol(dragStartPosition); // Determine which row/col the drag started over

        // --- Reset pending validation check on new drag start ---
        // Prevents validation from a previous drag if a new drag starts before Update checks
        pendingValidationCheck = false;

        Debug.Log($"OnBeginDrag: Started at Local Pos {dragStartPosition}. Target (R:{targetRow}, C:{targetCol}). IsAnimating: {wordGridManager.isAnimating}. PendingValidationCheck reset.");
    }

    // Called repeatedly while the pointer is being dragged
    public void OnDrag(PointerEventData eventData)
    {
        // Ignore if not currently in a dragging state
        if (!isDragging)
        {
            // Debug.Log("OnDrag ignored: Not dragging."); // Can be spammy
            return;
        }
        // Ignore if grid manager missing or starts animating mid-drag
        if (wordGridManager == null || wordGridManager.isAnimating)
        {
            Debug.Log($"OnDrag ignored: WordGridManager missing or animating (IsAnimating: {wordGridManager?.isAnimating}).");
            // Consider ending drag if manager goes missing or starts animating mid-drag
            // OnEndDrag(eventData); // Or just ignore input for this frame
            return;
        }

        // Get current pointer position in local coordinates
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, eventData.position, eventData.pressEventCamera, out Vector2 currentLocalPos))
        {
            // Pointer moved outside the panel bounds - treat this as ending the drag
            Debug.Log("OnDrag: Dragged outside Grid Panel Rect, ending drag.");
            OnEndDrag(eventData);
            return;
        }

        // Calculate the vector difference from the start position
        Vector2 dragVector = currentLocalPos - dragStartPosition;
        // Debug.Log($"OnDrag: Current Local Pos {currentLocalPos}, Drag Vector {dragVector}"); // Spammy

        // --- Axis Locking Logic ---
        // If the axis isn't locked yet, check if drag distance exceeds threshold
        if (!axisLocked)
        {
            if (dragVector.magnitude > dragThreshold)
            {
                axisLocked = true;
                // Determine dominant axis (horizontal or vertical)
                isHorizontalDrag = Mathf.Abs(dragVector.x) > Mathf.Abs(dragVector.y);
                // Re-calculate target row/col based on the locked axis start point? Usually not needed.
                // CalculateTargetRowCol(dragStartPosition);
                Debug.Log($"OnDrag: Axis Locked - {(isHorizontalDrag ? "Horizontal" : "Vertical")}. Target (R:{targetRow}, C:{targetCol})");
            }
            else
            {
                // Debug.Log($"OnDrag: Axis not locked yet. Magnitude {dragVector.magnitude} < Threshold {dragThreshold}"); // Spammy
                return; // Don't process scroll until axis is locked
            }
        }

        // --- Scroll Triggering Logic (Only if axis is locked) ---
        if (axisLocked)
        {
            // Get drag amount along the locked axis
            float dragAmount = isHorizontalDrag ? dragVector.x : dragVector.y;
            float previousAccumulated = accumulatedDrag; // Store value from last frame
            accumulatedDrag = dragAmount; // Update current accumulated value

            // Calculate the threshold distance needed to trigger a scroll
            float scrollThreshold = cellSizeWithSpacing * scrollThresholdFactor;
            int scrollDirection = 0; // 1 or -1 if scroll triggered, 0 otherwise
            float scrollAmountValue = 0; // Actual drag distance at trigger point

            // Check if positive threshold was crossed since last frame
            if (previousAccumulated < scrollThreshold && accumulatedDrag >= scrollThreshold)
            {
                scrollDirection = isHorizontalDrag ? 1 : -1; // Right or Up (inverted Y)
                scrollAmountValue = accumulatedDrag;
                Debug.Log($"OnDrag: Positive Scroll Triggered! Dir: {scrollDirection}, Accum: {scrollAmountValue}, Thresh: {scrollThreshold}");
                accumulatedDrag = 0; // Reset accumulation after trigger
                dragStartPosition = currentLocalPos; // Reset start pos for next drag segment relative to current pos
            }
            // Check if negative threshold was crossed
            else if (previousAccumulated > -scrollThreshold && accumulatedDrag <= -scrollThreshold)
            {
                scrollDirection = isHorizontalDrag ? -1 : 1; // Left or Down (inverted Y)
                scrollAmountValue = accumulatedDrag;
                Debug.Log($"OnDrag: Negative Scroll Triggered! Dir: {scrollDirection}, Accum: {scrollAmountValue}, Thresh: {scrollThreshold}");
                accumulatedDrag = 0; // Reset accumulation
                dragStartPosition = currentLocalPos; // Reset start pos
            }
            // else { Debug.Log($"OnDrag: No scroll triggered. Accum: {accumulatedDrag}, Thresh: {scrollThreshold}"); } // Spammy

            // --- Request Scroll from WordGridManager if Triggered ---
            if (scrollDirection != 0)
            {
                // --- IMPORTANT: Reset pending check if a scroll happens ---
                // If the user scrolls again, we cancel any pending validation from the previous drag end.
                // A new pending check will be set when the *final* drag ends.
                if (pendingValidationCheck)
                {
                    Debug.Log("OnDrag: Scroll triggered, resetting pendingValidationCheck.", this);
                    pendingValidationCheck = false;
                }


                // Call the appropriate scroll request method on the grid manager
                if (isHorizontalDrag && targetRow != -1)
                {
                    Debug.Log($"--> Requesting Row Scroll: Row {targetRow}, Dir {scrollDirection}");
                    wordGridManager.RequestRowScroll(targetRow, scrollDirection, scrollAmountValue);
                }
                else if (!isHorizontalDrag && targetCol != -1)
                {
                    Debug.Log($"--> Requesting Col Scroll: Col {targetCol}, Dir {scrollDirection}");
                    wordGridManager.RequestColumnScroll(targetCol, scrollDirection, scrollAmountValue);
                }
                else
                {
                    // Should not happen if targetRow/Col calculated correctly, but log if it does
                    Debug.LogWarning($"Scroll triggered (Dir: {scrollDirection}) but target row/col was invalid (R:{targetRow}, C:{targetCol})");
                }
                // Reset accumulation and start position are handled above after trigger
            }
        }
    }

    // Called once when the drag gesture ends (pointer released)
    public void OnEndDrag(PointerEventData eventData)
    {
        // Ignore if not currently dragging (e.g., if drag was ended prematurely)
        if (!isDragging)
        {
            // Debug.Log("OnEndDrag ignored: Not dragging.");
            return;
        }
        Debug.Log("OnEndDrag: Drag finished. Resetting drag state.", this);
        // Reset all drag state variables
        isDragging = false;
        axisLocked = false;
        accumulatedDrag = 0f;
        targetRow = -1; // Reset target
        targetCol = -1; // Reset target

        // --- SET Pending Validation Check ---
        // Instead of validating immediately, set the flag.
        // The Update() method will monitor this flag and the grid manager's animation state.
        pendingValidationCheck = true;
        Debug.Log("OnEndDrag: Set pendingValidationCheck = true. Update() will check when grid is not animating.", this);
    }

    // Calculates the target row and column based on the local position within the grid panel
    void CalculateTargetRowCol(Vector2 localPosition)
    {
        if (wordGridManager == null) return; // Should not happen if Start checks pass

        // Calculate grid dimensions and starting offsets (similar to WordGridManager.InitializeGrid)
        float totalGridSizeUI = wordGridManager.gridSize * wordGridManager.cellSize + (wordGridManager.gridSize - 1) * wordGridManager.spacing;
        float gridStartX = -totalGridSizeUI / 2f;
        float gridStartY = -totalGridSizeUI / 2f;

        // Calculate position relative to the top-left of the grid area
        float relativeX = localPosition.x - gridStartX;
        float relativeY = localPosition.y - gridStartY;

        // Calculate column and row indices based on cell size and spacing
        // FloorToInt handles positions within the cell bounds
        targetCol = Mathf.FloorToInt(relativeX / cellSizeWithSpacing);
        // Row calculation needs inversion due to UI coordinates (Y increases downwards) vs. array index (Y/row increases upwards)
        targetRow = wordGridManager.gridSize - 1 - Mathf.FloorToInt(relativeY / cellSizeWithSpacing);

        // Clamp values to ensure they are within valid grid bounds (0 to gridSize-1)
        // This acts as a safety net, though dragging outside should ideally end the drag via OnDrag logic.
        targetCol = Mathf.Clamp(targetCol, 0, wordGridManager.gridSize - 1);
        targetRow = Mathf.Clamp(targetRow, 0, wordGridManager.gridSize - 1);
        // Debug.Log($"CalculateTargetRowCol: LocalPos {localPosition} -> Relative ({relativeX}, {relativeY}) -> Target (R:{targetRow}, C:{targetCol})"); // Spammy
    }
}