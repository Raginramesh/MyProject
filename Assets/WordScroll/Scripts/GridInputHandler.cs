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

    // --- State flags for delayed actions ---
    private bool scrollOccurredThisDrag = false; // Tracks if ANY scroll happened during the current drag
    private bool pendingValidationCheck = false; // Flag for word validation check
    private bool pendingMoveReduction = false;   // Flag for move reduction check
    private int moveReductionRow = -1;           // Row index for pending reduction
    private int moveReductionCol = -1;           // Column index for pending reduction


    void Start()
    {
        // Validate references
        if (wordGridManager == null) { Debug.LogError("GridInputHandler: WordGridManager reference missing!", this); enabled = false; return; }
        if (gridPanelRect == null) { Debug.LogError("GridInputHandler: Grid Panel Rect reference missing!", this); enabled = false; return; }

        // Pre-calculate cell size + spacing for efficiency
        cellSizeWithSpacing = wordGridManager.cellSize + wordGridManager.spacing;
        Debug.Log($"GridInputHandler Started. CellSizeWithSpacing: {cellSizeWithSpacing}");

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
        Debug.Log("GridInputHandler Enabled");
    }

    void OnDisable()
    {
        // Reset flags on disable
        pendingValidationCheck = false;
        pendingMoveReduction = false;
        scrollOccurredThisDrag = false;
        Debug.Log("GridInputHandler Disabled");
    }

    void Update()
    {
        // --- Check for Pending Move Reduction ---
        if (pendingMoveReduction && wordGridManager != null && !wordGridManager.isAnimating)
        {
            pendingMoveReduction = false; // Reset the flag FIRST
            Debug.Log($"GridInputHandler: Grid settled. Requesting pending move reduction for Row:{moveReductionRow} Col:{moveReductionCol}.", this);
            wordGridManager.ApplyPendingMoveReduction(moveReductionRow, moveReductionCol);
            moveReductionRow = -1; // Reset indices after applying
            moveReductionCol = -1;
        }

        // --- Check for Pending Validation ---
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
            Debug.Log($"OnBeginDrag ignored: Enabled={enabled}, Manager={wordGridManager != null}, Animating={wordGridManager?.isAnimating}");
            isDragging = false; return;
        }

        // Convert screen point to local point
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, eventData.position, eventData.pressEventCamera, out dragStartPosition))
        {
            Debug.Log("OnBeginDrag ignored: Click outside Grid Panel Rect.");
            isDragging = false; return;
        }

        // --- Start the drag ---
        isDragging = true;
        axisLocked = false;
        accumulatedDrag = 0f;
        CalculateTargetRowCol(dragStartPosition);

        // --- Reset ALL pending flags and scroll tracking ---
        pendingValidationCheck = false;
        pendingMoveReduction = false;
        scrollOccurredThisDrag = false;
        moveReductionRow = -1;
        moveReductionCol = -1;

        Debug.Log($"OnBeginDrag: Started at Local Pos {dragStartPosition}. Target (R:{targetRow}, C:{targetCol}). Pending flags reset.");
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Ignore if not dragging or if grid starts animating mid-drag
        if (!isDragging || wordGridManager == null || wordGridManager.isAnimating) { return; }

        // Get current pointer position
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, eventData.position, eventData.pressEventCamera, out Vector2 currentLocalPos))
        {
            Debug.Log("OnDrag: Dragged outside Grid Panel Rect, ending drag.");
            OnEndDrag(eventData); // Treat dragging outside as ending the drag
            return;
        }

        Vector2 dragVector = currentLocalPos - dragStartPosition;

        // --- Axis Locking Logic ---
        if (!axisLocked)
        {
            if (dragVector.magnitude > dragThreshold)
            {
                axisLocked = true;
                isHorizontalDrag = Mathf.Abs(dragVector.x) > Mathf.Abs(dragVector.y);
                Debug.Log($"OnDrag: Axis Locked - {(isHorizontalDrag ? "Horizontal" : "Vertical")}. Target (R:{targetRow}, C:{targetCol})");
            }
            else { return; } // Don't process scroll until axis is locked
        }

        // --- Scroll Triggering Logic ---
        if (axisLocked)
        {
            float dragAmount = isHorizontalDrag ? dragVector.x : dragVector.y;
            float previousAccumulated = accumulatedDrag;
            accumulatedDrag = dragAmount;
            float scrollThreshold = cellSizeWithSpacing * scrollThresholdFactor;
            int scrollDirection = 0;
            float scrollAmountValue = 0;

            // Check threshold crossing
            if (previousAccumulated < scrollThreshold && accumulatedDrag >= scrollThreshold)
            {
                scrollDirection = isHorizontalDrag ? 1 : -1; // Right or Up (inverted Y)
                scrollAmountValue = accumulatedDrag;
                accumulatedDrag = 0; dragStartPosition = currentLocalPos;
            }
            else if (previousAccumulated > -scrollThreshold && accumulatedDrag <= -scrollThreshold)
            {
                scrollDirection = isHorizontalDrag ? -1 : 1; // Left or Down (inverted Y)
                scrollAmountValue = accumulatedDrag;
                accumulatedDrag = 0; dragStartPosition = currentLocalPos;
            }

            // --- Request Scroll if Triggered ---
            if (scrollDirection != 0)
            {
                scrollOccurredThisDrag = true; // Set flag
                Debug.Log($"OnDrag: Scroll triggered. scrollOccurredThisDrag = true.");

                // Cancel any pending actions (safety)
                if (pendingValidationCheck) { Debug.Log("OnDrag: Scroll triggered, resetting pendingValidationCheck."); pendingValidationCheck = false; }
                if (pendingMoveReduction) { Debug.Log("OnDrag: Scroll triggered, resetting pendingMoveReduction."); pendingMoveReduction = false; moveReductionRow = -1; moveReductionCol = -1; }

                // Call the scroll request method
                if (isHorizontalDrag && targetRow != -1)
                {
                    // Debug.Log($"--> Requesting Row Scroll: Row {targetRow}, Dir {scrollDirection}");
                    wordGridManager.RequestRowScroll(targetRow, scrollDirection, scrollAmountValue);
                }
                else if (!isHorizontalDrag && targetCol != -1)
                {
                    // Debug.Log($"--> Requesting Col Scroll: Col {targetCol}, Dir {scrollDirection}");
                    wordGridManager.RequestColumnScroll(targetCol, scrollDirection, scrollAmountValue);
                }
                else { Debug.LogWarning($"Scroll triggered but target row/col invalid (R:{targetRow}, C:{targetCol})"); }
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        Debug.Log($"OnEndDrag: Drag finished. Target Start (R:{targetRow}, C:{targetCol}). scrollOccurredThisDrag={scrollOccurredThisDrag}", this);

        // --- Set Pending Flags ---

        // 1. Pending Move Reduction (IF scroll occurred)
        if (scrollOccurredThisDrag)
        {
            pendingMoveReduction = true;
            if (isHorizontalDrag) { moveReductionRow = targetRow; moveReductionCol = -1; }
            else { moveReductionRow = -1; moveReductionCol = targetCol; }
            Debug.Log($"OnEndDrag: Scroll occurred. Set pendingMoveReduction=true for Row:{moveReductionRow} Col:{moveReductionCol}. Update() will handle.", this);
        }
        else { Debug.Log($"OnEndDrag: No scroll occurred. No move reduction pending.", this); }

        // 2. Pending Validation (Always)
        pendingValidationCheck = true;
        // Debug.Log("OnEndDrag: Set pendingValidationCheck=true. Update() will handle.", this);


        // --- Reset Drag State ---
        isDragging = false;
        axisLocked = false;
        accumulatedDrag = 0f;
        scrollOccurredThisDrag = false; // Reset scroll tracking for next drag
        targetRow = -1;
        targetCol = -1;
    }

    // Calculates the target row and column based on the local position within the grid panel
    void CalculateTargetRowCol(Vector2 localPosition)
    {
        if (wordGridManager == null) return;

        float totalGridSizeUI = wordGridManager.gridSize * wordGridManager.cellSize + (wordGridManager.gridSize - 1) * wordGridManager.spacing;
        float gridStartX = -totalGridSizeUI / 2f;
        float gridStartY = -totalGridSizeUI / 2f;
        float relativeX = localPosition.x - gridStartX;
        float relativeY = localPosition.y - gridStartY;

        targetCol = Mathf.FloorToInt(relativeX / cellSizeWithSpacing);
        targetRow = wordGridManager.gridSize - 1 - Mathf.FloorToInt(relativeY / cellSizeWithSpacing);

        targetCol = Mathf.Clamp(targetCol, 0, wordGridManager.gridSize - 1);
        targetRow = Mathf.Clamp(targetRow, 0, wordGridManager.gridSize - 1);
    }
}