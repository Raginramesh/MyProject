using UnityEngine;
using UnityEngine.EventSystems; // Required for event system interfaces

public class GridInputHandler_Backup : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("References")]
    [Tooltip("Assign the WordGridManager script instance here")]
    [SerializeField] private WordGridManager wordGridManager;

    [Header("Settings")]
    [Tooltip("How much of a cell's width/height needs to be dragged to trigger one scroll step.")]
    [SerializeField][Range(0.1f, 1.0f)] private float scrollThresholdFactor = 0.5f; // e.g., 0.5 means drag half a cell width/height

    // --- Private Drag State ---
    private bool isDragging = false;
    private Vector2 dragStartPosition; // Screen position where drag started
    private Vector2 lastDragPosition;  // Previous frame's screen position
    private Vector2 accumulatedDragVector; // Total drag vector this drag gesture
    private float accumulatedHorizontalDrag = 0f; // Accumulated drag distance specifically for triggering horizontal scrolls
    private float accumulatedVerticalDrag = 0f;   // Accumulated drag distance specifically for triggering vertical scrolls

    private enum DragAxis { None, Horizontal, Vertical }
    private DragAxis lockedAxis = DragAxis.None;
    private int lockedRowOrColumnIndex = -1;

    private RectTransform panelRectTransform;

    // --- Public Flag (Optional - can be controlled by WordGridManager) ---
    // Set this to true while scrolling animation is playing to prevent new input
    //public bool IsInputBlocked { get; set; } = false;

    void Awake()
    {
        panelRectTransform = GetComponent<RectTransform>();
        if (panelRectTransform == null)
        {
            Debug.LogError("GridInputHandler requires a RectTransform component on the same GameObject.", this);
        }
        if (wordGridManager == null)
        {
            Debug.LogError("WordGridManager reference is not assigned in the GridInputHandler inspector!", this);
            // As a fallback, you could try to find it, but assigning is better:
            // wordGridManager = FindObjectOfType<WordGridManager>();
        }
    }

    // Called when the user first presses down and starts moving on this UI element
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (wordGridManager == null || wordGridManager.IsAnimating) // Check if manager is busy
        {
            isDragging = false; // Prevent drag logic if blocked
            return;
        }

        isDragging = true;
        dragStartPosition = eventData.position;
        lastDragPosition = dragStartPosition;
        accumulatedDragVector = Vector2.zero;
        accumulatedHorizontalDrag = 0f;
        accumulatedVerticalDrag = 0f;
        lockedAxis = DragAxis.None;
        lockedRowOrColumnIndex = -1;
        // Debug.Log($"Begin Drag at: {dragStartPosition}");
    }

    // Called while the user is dragging (we don't need complex logic here for simple swipes)
    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || wordGridManager == null || wordGridManager.IsAnimating) // Check if should process drag
        {
            // If we were dragging but got blocked mid-drag, reset state
            if (isDragging) OnEndDrag(eventData); // Treat as drag end if blocked
            return;
        }

        Vector2 currentDragPosition = eventData.position;
        Vector2 delta = currentDragPosition - lastDragPosition; // Movement since last frame
        accumulatedDragVector += delta; // Add to total drag vector for direction locking
        lastDragPosition = currentDragPosition;

        // --- Lock Axis if not already locked ---
        if (lockedAxis == DragAxis.None)
        {
            float totalHorizontal = Mathf.Abs(accumulatedDragVector.x);
            float totalVertical = Mathf.Abs(accumulatedDragVector.y);
            float lockThreshold = 10f; // Min pixels to move before locking axis

            if (totalHorizontal > lockThreshold || totalVertical > lockThreshold)
            {
                if (totalHorizontal > totalVertical)
                {
                    lockedAxis = DragAxis.Horizontal;
                    lockedRowOrColumnIndex = GetRowIndexFromScreenPosition(dragStartPosition);
                    // Debug.Log($"Locked Axis: Horizontal, Row: {lockedRowOrColumnIndex}");
                }
                else
                {
                    lockedAxis = DragAxis.Vertical;
                    lockedRowOrColumnIndex = GetColIndexFromScreenPosition(dragStartPosition);
                    // Debug.Log($"Locked Axis: Vertical, Column: {lockedRowOrColumnIndex}");
                }

                // If locking failed (invalid index), stop the drag
                if (lockedRowOrColumnIndex == -1)
                {
                    Debug.LogWarning("Could not determine valid row/column index on drag start.");
                    OnEndDrag(eventData); // Treat as drag end
                    return;
                }
            }
        }

        // --- Process Drag based on Locked Axis ---
        if (lockedAxis == DragAxis.Horizontal && lockedRowOrColumnIndex != -1)
        {
            accumulatedHorizontalDrag += delta.x;
            float scrollThreshold = (wordGridManager.CellSize.x + wordGridManager.Spacing.x) * scrollThresholdFactor;

            // Check if enough distance dragged for a scroll LEFT
            if (accumulatedHorizontalDrag < -scrollThreshold)
            {
                Debug.Log($"Threshold met: Requesting Row {lockedRowOrColumnIndex} scroll LEFT");
                wordGridManager.RequestRowScroll(lockedRowOrColumnIndex, -1);
                accumulatedHorizontalDrag += scrollThreshold; // Consume threshold distance
                                                              // Re-check if animating *immediately* after request in case it was instant? Or rely on next frame's check.
                if (wordGridManager.IsAnimating) { OnEndDrag(eventData); return; } // Stop processing drag if animation started
            }
            // Check if enough distance dragged for a scroll RIGHT
            else if (accumulatedHorizontalDrag > scrollThreshold)
            {
                Debug.Log($"Threshold met: Requesting Row {lockedRowOrColumnIndex} scroll RIGHT");
                wordGridManager.RequestRowScroll(lockedRowOrColumnIndex, 1);
                accumulatedHorizontalDrag -= scrollThreshold; // Consume threshold distance
                if (wordGridManager.IsAnimating) { OnEndDrag(eventData); return; } // Stop processing drag
            }
        }
        else if (lockedAxis == DragAxis.Vertical && lockedRowOrColumnIndex != -1)
        {
            accumulatedVerticalDrag += delta.y;
            float scrollThreshold = (wordGridManager.CellSize.y + wordGridManager.Spacing.y) * scrollThresholdFactor;

            // Check for scroll UP (negative direction, positive delta Y)
            if (accumulatedVerticalDrag > scrollThreshold)
            {
                Debug.Log($"Threshold met: Requesting Column {lockedRowOrColumnIndex} scroll UP");
                wordGridManager.RequestColumnScroll(lockedRowOrColumnIndex, -1); // UP is -1
                accumulatedVerticalDrag -= scrollThreshold; // Consume threshold
                if (wordGridManager.IsAnimating) { OnEndDrag(eventData); return; } // Stop processing drag
            }
            // Check for scroll DOWN (positive direction, negative delta Y)
            else if (accumulatedVerticalDrag < -scrollThreshold)
            {
                Debug.Log($"Threshold met: Requesting Column {lockedRowOrColumnIndex} scroll DOWN");
                wordGridManager.RequestColumnScroll(lockedRowOrColumnIndex, 1); // DOWN is +1
                accumulatedVerticalDrag += scrollThreshold; // Consume threshold
                if (wordGridManager.IsAnimating) { OnEndDrag(eventData); return; } // Stop processing drag
            }
        }
    }


    // Called when the user releases the pointer after dragging
    public void OnEndDrag(PointerEventData eventData)
    {
        // Reset state regardless of previous state
        isDragging = false;
        lockedAxis = DragAxis.None;
        lockedRowOrColumnIndex = -1;
        accumulatedHorizontalDrag = 0f;
        accumulatedVerticalDrag = 0f;
        // Debug.Log("End Drag");
    }


    // --- Helper Functions to get Row/Column from Screen Position ---

    private int GetRowIndexFromScreenPosition(Vector2 screenPos)
    {
        if (panelRectTransform == null || wordGridManager == null) return -1;

        // Convert screen position to local position within the panel's RectTransform
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            panelRectTransform, screenPos, (GetComponentInParent<Canvas>().renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main), out Vector2 localPoint))
        {
            // Assuming panel pivot is center (0.5, 0.5)
            // Normalize local position to range [0, 1] (0,0 being bottom-left based on localPoint)
            float panelHeight = panelRectTransform.rect.height;
            // Adjust Y based on pivot to get distance from bottom edge
            float yPosFromBottom = localPoint.y + panelHeight * panelRectTransform.pivot.y;
            // Normalize (0 at bottom, 1 at top)
            float normalizedY = Mathf.Clamp01(yPosFromBottom / panelHeight);

            // Convert normalized Y (0=bottom, 1=top) to row index (0=top, 3=bottom)
            int rowIndex = Mathf.FloorToInt((1f - normalizedY) * wordGridManager.GridSize); // Invert Y
                                                                                            // Clamp to ensure it's within valid grid bounds
            rowIndex = Mathf.Clamp(rowIndex, 0, wordGridManager.GridSize - 1);
            // Debug.Log($"Screen Pos: {screenPos} -> Local Pos: {localPoint} -> Norm Y: {normalizedY} -> Row Index: {rowIndex}");
            return rowIndex;

        }
        return -1; // Return -1 if conversion fails
    }

    private int GetColIndexFromScreenPosition(Vector2 screenPos)
    {
        if (panelRectTransform == null || wordGridManager == null) return -1;

        // Convert screen position to local position within the panel's RectTransform
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            panelRectTransform, screenPos, (GetComponentInParent<Canvas>().renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main), out Vector2 localPoint))
        {
            // Assuming panel pivot is center (0.5, 0.5)
            // Normalize local position to range [0, 1] (0,0 being bottom-left based on localPoint)
            float panelWidth = panelRectTransform.rect.width;
            // Adjust X based on pivot to get distance from left edge
            float xPosFromLeft = localPoint.x + panelWidth * panelRectTransform.pivot.x;
            // Normalize (0 at left, 1 at right)
            float normalizedX = Mathf.Clamp01(xPosFromLeft / panelWidth);

            // Convert normalized X (0=left, 1=right) to col index (0=left, 3=right)
            int colIndex = Mathf.FloorToInt(normalizedX * wordGridManager.GridSize);
            // Clamp to ensure it's within valid grid bounds
            colIndex = Mathf.Clamp(colIndex, 0, wordGridManager.GridSize - 1);
            // Debug.Log($"Screen Pos: {screenPos} -> Local Pos: {localPoint} -> Norm X: {normalizedX} -> Col Index: {colIndex}");
            return colIndex;
        }
        return -1; // Return -1 if conversion fails
    }

    // --- Need access to GridSize from WordGridManager ---
    // Add a public property to WordGridManager:
    // public int GridSize => gridSize; // Assuming private int gridSize;
}