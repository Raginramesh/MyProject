using UnityEngine;
using UnityEngine.EventSystems;

public class GridInputHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("References")]
    [SerializeField] private WordGridManager wordGridManager;
    [SerializeField] private RectTransform gridPanelRect; // Panel that receives drag events

    [Header("Settings")]
    [SerializeField] private float dragThreshold = 20f;
    [SerializeField] private float scrollThresholdFactor = 0.4f;

    private Vector2 dragStartPosition;
    private bool isDragging = false;
    private bool axisLocked = false;
    private bool isHorizontalDrag = false;
    private int targetRow = -1;
    private int targetCol = -1;
    private float accumulatedDrag = 0f;
    private float cellSizeWithSpacing;

    void Start()
    {
        if (wordGridManager == null)
        {
            Debug.LogError("GridInputHandler: WordGridManager reference missing!", this);
            enabled = false; return;
        }
        if (gridPanelRect == null)
        {
            Debug.LogError("GridInputHandler: Grid Panel Rect reference missing!", this);
            enabled = false; return;
        }
        // Calculate cell size including spacing using public properties
        cellSizeWithSpacing = wordGridManager.cellSize + wordGridManager.spacing;
        Debug.Log($"GridInputHandler Started. CellSizeWithSpacing: {cellSizeWithSpacing}");
    }

    void OnEnable()
    {
        // Reset state when enabled (e.g., after game over and restart)
        isDragging = false;
        axisLocked = false;
        accumulatedDrag = 0f;
        Debug.Log("GridInputHandler Enabled");
    }

    void OnDisable()
    {
        // Optional: Log when disabled
        Debug.Log("GridInputHandler Disabled");
    }


    public void OnBeginDrag(PointerEventData eventData)
    {
        // Check if script is enabled and WordGridManager exists
        if (!enabled || wordGridManager == null)
        {
            Debug.LogWarning("OnBeginDrag ignored: GridInputHandler disabled or WordGridManager missing.");
            return;
        }

        // Check if WordGridManager is animating
        if (wordGridManager.isAnimating)
        {
            Debug.Log("OnBeginDrag ignored: WordGridManager is animating.");
            isDragging = false; // Ensure dragging flag is false if ignored
            return;
        }

        // Try to get local point, check if click is within the panel
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, eventData.position, eventData.pressEventCamera, out dragStartPosition))
        {
            Debug.Log("OnBeginDrag ignored: Click outside Grid Panel Rect.");
            isDragging = false;
            return;
        }

        // --- If we got here, drag is starting ---
        isDragging = true;
        axisLocked = false;
        accumulatedDrag = 0f;
        CalculateTargetRowCol(dragStartPosition); // Calculate initial target
        Debug.Log($"OnBeginDrag: Started at Local Pos {dragStartPosition}. Initial Target (R:{targetRow}, C:{targetCol}). IsAnimating: {wordGridManager.isAnimating}");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            // Debug.Log("OnDrag ignored: Not dragging."); // Can be spammy, enable if needed
            return;
        }
        if (wordGridManager == null || wordGridManager.isAnimating)
        {
            Debug.Log($"OnDrag ignored: WordGridManager missing or animating (IsAnimating: {wordGridManager?.isAnimating}).");
            // Consider ending drag if manager goes missing or starts animating mid-drag
            // OnEndDrag(eventData);
            return;
        }

        // Get current position, check if still within panel
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, eventData.position, eventData.pressEventCamera, out Vector2 currentLocalPos))
        {
            Debug.Log("OnDrag: Dragged outside Grid Panel Rect, ending drag.");
            OnEndDrag(eventData); // Treat dragging outside as ending the drag
            return;
        }

        Vector2 dragVector = currentLocalPos - dragStartPosition;
        // Debug.Log($"OnDrag: Current Local Pos {currentLocalPos}, Drag Vector {dragVector}"); // Spammy

        // --- Axis Locking ---
        if (!axisLocked)
        {
            if (dragVector.magnitude > dragThreshold)
            {
                axisLocked = true;
                isHorizontalDrag = Mathf.Abs(dragVector.x) > Mathf.Abs(dragVector.y);
                // Optional: Recalculate target based on locked axis start point? Usually not needed.
                // CalculateTargetRowCol(dragStartPosition);
                Debug.Log($"OnDrag: Axis Locked - {(isHorizontalDrag ? "Horizontal" : "Vertical")}. Target (R:{targetRow}, C:{targetCol})");
            }
            else
            {
                // Debug.Log($"OnDrag: Axis not locked yet. Magnitude {dragVector.magnitude} < Threshold {dragThreshold}"); // Spammy
                return; // Don't process scroll until axis is locked
            }
        }

        // --- Scroll Triggering (Only if axis is locked) ---
        if (axisLocked)
        {
            float dragAmount = isHorizontalDrag ? dragVector.x : dragVector.y;
            float previousAccumulated = accumulatedDrag;
            accumulatedDrag = dragAmount;

            float scrollThreshold = cellSizeWithSpacing * scrollThresholdFactor;
            int scrollDirection = 0;
            float scrollAmountValue = 0;

            // Check positive threshold crossing
            if (previousAccumulated < scrollThreshold && accumulatedDrag >= scrollThreshold)
            {
                scrollDirection = isHorizontalDrag ? 1 : -1; // Right or Up
                scrollAmountValue = accumulatedDrag;
                Debug.Log($"OnDrag: Positive Scroll Triggered! Dir: {scrollDirection}, Accum: {scrollAmountValue}, Thresh: {scrollThreshold}");
                accumulatedDrag = 0; // Reset after trigger
                dragStartPosition = currentLocalPos; // Reset start pos for next drag segment
            }
            // Check negative threshold crossing
            else if (previousAccumulated > -scrollThreshold && accumulatedDrag <= -scrollThreshold)
            {
                scrollDirection = isHorizontalDrag ? -1 : 1; // Left or Down
                scrollAmountValue = accumulatedDrag;
                Debug.Log($"OnDrag: Negative Scroll Triggered! Dir: {scrollDirection}, Accum: {scrollAmountValue}, Thresh: {scrollThreshold}");
                accumulatedDrag = 0; // Reset after trigger
                dragStartPosition = currentLocalPos; // Reset start pos for next drag segment
            }
            // else { Debug.Log($"OnDrag: No scroll triggered. Accum: {accumulatedDrag}, Thresh: {scrollThreshold}"); } // Spammy

            // --- Request Scroll if Triggered ---
            if (scrollDirection != 0)
            {
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
                    Debug.LogWarning($"Scroll triggered (Dir: {scrollDirection}) but target row/col was invalid (R:{targetRow}, C:{targetCol})");
                }
                // Reset accumulation and start position are handled above after trigger
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            // Debug.Log("OnEndDrag ignored: Not dragging.");
            return;
        }
        Debug.Log("OnEndDrag: Resetting state.");
        isDragging = false;
        axisLocked = false;
        accumulatedDrag = 0f;
        targetRow = -1; // Reset target
        targetCol = -1; // Reset target
    }

    void CalculateTargetRowCol(Vector2 localPosition)
    {
        if (wordGridManager == null) return; // Should not happen if Start checks pass

        float totalGridSizeUI = wordGridManager.gridSize * wordGridManager.cellSize + (wordGridManager.gridSize - 1) * wordGridManager.spacing;
        float gridStartX = -totalGridSizeUI / 2f;
        float gridStartY = -totalGridSizeUI / 2f;

        float relativeX = localPosition.x - gridStartX;
        float relativeY = localPosition.y - gridStartY;

        targetCol = Mathf.FloorToInt(relativeX / cellSizeWithSpacing);
        targetRow = wordGridManager.gridSize - 1 - Mathf.FloorToInt(relativeY / cellSizeWithSpacing);

        // Clamp values just in case, although dragging outside should end the drag
        targetCol = Mathf.Clamp(targetCol, 0, wordGridManager.gridSize - 1);
        targetRow = Mathf.Clamp(targetRow, 0, wordGridManager.gridSize - 1);
        // Debug.Log($"CalculateTargetRowCol: LocalPos {localPosition} -> Relative ({relativeX}, {relativeY}) -> Target (R:{targetRow}, C:{targetCol})"); // Spammy
    }
}