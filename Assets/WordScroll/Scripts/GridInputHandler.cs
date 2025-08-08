using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Collections;
using DG.Tweening;
using System;
using System.Linq;

public enum DragDirection
{
    None,
    Horizontal,
    Vertical
}

public class GridInputHandler : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
{
    [Header("References")]
    [SerializeField] private WordGridManager wordGridManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private RectTransform gridPanelRect;
    [SerializeField] private Camera uiCamera;

    [Header("Drag Settings")]
    [SerializeField] private float dragInitiationThreshold = 10f;
    [Tooltip("Percentage of cell dimension to cross for snap to next cell (0.0 to 1.0)")]
    [SerializeField] private float snapToNextThresholdPercentage = 0.6f; // 60%

    [Header("Tap Settings")]
    [SerializeField] private float maxTapDuration = 0.3f;
    [SerializeField] private float maxTapMoveDistance = 20f;

    [Header("Flick Settings")]
    [SerializeField] private float minFlickVelocity = 200f;
    [SerializeField] private float flickSnapDuration = 0.25f;
    [SerializeField] private Ease flickEaseType = Ease.OutQuad;

    [Header("Release Snap Settings")]
    [SerializeField] private float releaseSnapDuration = 0.2f;
    [SerializeField] private Ease releaseSnapEaseType = Ease.OutQuad;

    [Header("Deadzone Settings")]
    [Tooltip("Radius around initial touch point where no direction is committed (in pixels)")]
    [SerializeField] private float centerDeadzoneRadius = 20f;
    [Tooltip("Minimum distance to travel outside deadzone before direction locks")]
    [SerializeField] private float directionLockThreshold = 15f;
    [Tooltip("Enable debug visualization of deadzone (editor only)")]
    [SerializeField] private bool showDeadzoneDebug = false;

    private Vector2 pointerDownScreenPosition;
    private Vector2 pointerInitialPanelPosition;
    private float pointerDownTime;
    private bool isPointerCurrentlyDown = false;
    private bool isDragging = false;
    private bool isHorizontalDragLocked = false;
    private bool isVerticalDragLocked = false;
    private int activeDragRow = -1;
    private int activeDragCol = -1;
    private float currentFrameVisualRemainderOffsetX = 0f;
    private float currentFrameVisualRemainderOffsetY = 0f;
    private float cellDimensionWithSpacing;
    private bool dataActuallyShiftedDuringDrag = false;
    private bool tapCandidate = false;
    private List<Vector2> pointerPositionsHistory = new List<Vector2>();
    private List<float> pointerTimesHistory = new List<float>();

    // Deadzone state tracking
    private bool isInDeadzone = true;
    private Vector2 initialTouchScreenPosition;
    private DragDirection currentDragDirection = DragDirection.None;
    private const int VELOCITY_TRACKING_SAMPLES = 5;
    private Coroutine activeSnapAnimationCoroutine = null;

    // Move counting for multi-directional drags
    private bool hasDataChangedThisDragGesture = false;  // Track if any data changed during entire drag gesture
    private bool moveAlreadyCountedThisGesture = false;  // Prevent counting move multiple times per gesture

    // Cell tracking system - tracks specific cell by unique ID
    private int trackedCellID = -1;                // Unique ID of the tracked cell
    private Vector2Int trackedCellPosition;        // Current grid position of tracked cell
    private bool isTrackingCell = false;          // Whether we're actively tracking a cell
    
    [Header("Cell Tracking Settings")]
    [Tooltip("Enable cell-centric tracking that follows a specific cell by unique ID")]
    [SerializeField] private bool enableCellTracking = true;

    private char[] initialDragLineData;
    private bool dragBeganOnValidLine = false;

    void Awake()
    {
        if (wordGridManager == null) { Debug.LogError("GIH: WordGridManager not assigned!", this); enabled = false; return; }
        if (gameManager == null) { Debug.LogError("GIH: GameManager not assigned!", this); enabled = false; return; }
        if (gridPanelRect == null) { Debug.LogError("GIH: Grid Panel RectTransform not assigned!", this); enabled = false; return; }
    }

    private void ResetDragState()
    {
        bool wasPreviouslyDraggingRow = activeDragRow != -1;
        int prevDragRow = activeDragRow;
        bool wasPreviouslyDraggingCol = activeDragCol != -1;
        int prevDragCol = activeDragCol;

        isDragging = false;
        isHorizontalDragLocked = false;
        isVerticalDragLocked = false;
        activeDragRow = -1;
        activeDragCol = -1;
        currentFrameVisualRemainderOffsetX = 0f;
        currentFrameVisualRemainderOffsetY = 0f;
        dataActuallyShiftedDuringDrag = false;
        pointerPositionsHistory.Clear();
        pointerTimesHistory.Clear();

        // Reset deadzone state
        isInDeadzone = true;
        currentDragDirection = DragDirection.None;
        initialTouchScreenPosition = Vector2.zero;

        // Reset move counting for multi-directional drags
        hasDataChangedThisDragGesture = false;
        moveAlreadyCountedThisGesture = false;

        // Reset cell tracking
        trackedCellID = -1;
        trackedCellPosition = Vector2Int.zero;
        isTrackingCell = false;

        initialDragLineData = null;
        dragBeganOnValidLine = false;

        if (activeSnapAnimationCoroutine != null)
        {
            StopCoroutine(activeSnapAnimationCoroutine);
            activeSnapAnimationCoroutine = null;
            if (wordGridManager != null)
            {
                if (wasPreviouslyDraggingRow && prevDragRow != -1) wordGridManager.SnapRowToGrid(prevDragRow);
                if (wasPreviouslyDraggingCol && prevDragCol != -1) wordGridManager.SnapColumnToGrid(prevDragCol);
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (gameManager.IsAnyAnimationPlaying || gameManager.CurrentStatePublic != GameManager.GameState.Playing || activeSnapAnimationCoroutine != null) return;

        isPointerCurrentlyDown = true;
        tapCandidate = true;
        pointerDownTime = Time.time;
        pointerDownScreenPosition = eventData.position;
        
        // Initialize deadzone tracking
        initialTouchScreenPosition = eventData.position;
        isInDeadzone = true;
        currentDragDirection = DragDirection.None;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gridPanelRect,
            pointerDownScreenPosition,
            uiCamera,
            out pointerInitialPanelPosition
        );

        isDragging = false;
        isHorizontalDragLocked = false;
        isVerticalDragLocked = false;
        currentFrameVisualRemainderOffsetX = 0f;
        currentFrameVisualRemainderOffsetY = 0f;
        dataActuallyShiftedDuringDrag = false;
        initialDragLineData = null;
        dragBeganOnValidLine = false;
        pointerPositionsHistory.Clear();
        pointerTimesHistory.Clear();

        // Initialize cell tracking if enabled
        if (enableCellTracking)
        {
            Vector2Int gridCoords = CalculateGridCoordsFromLocalPos(pointerInitialPanelPosition);
            if (gridCoords.x >= 0 && gridCoords.y >= 0 && 
                gridCoords.x < wordGridManager.gridSize && gridCoords.y < wordGridManager.gridSize)
            {
                CellController cellController = wordGridManager.GetCellController(gridCoords);
                if (cellController != null)
                {
                    trackedCellID = cellController.uniqueID;
                    trackedCellPosition = gridCoords;
                    isTrackingCell = true;
                    
                    Debug.Log($"🎯 Started tracking cell ID {trackedCellID} at position ({gridCoords.x}, {gridCoords.y})");
                }
            }
        }

        AddPointerSample(eventData.position);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!isPointerCurrentlyDown || gameManager.IsAnyAnimationPlaying || gameManager.CurrentStatePublic != GameManager.GameState.Playing || activeSnapAnimationCoroutine != null) return;

        AddPointerSample(eventData.position);
        Vector2 currentScreenPosition = eventData.position;
        float dragDistance = Vector2.Distance(currentScreenPosition, pointerDownScreenPosition);

        if (!isDragging && dragDistance < dragInitiationThreshold)
        {
            return;
        }

        if (!isDragging)
        {
            isDragging = true;
            tapCandidate = false;

            // Clear all highlights when a drag gesture officially starts
            if (wordGridManager != null)
            {
                wordGridManager.ClearAllCellHighlights(false); // false to reset to stored default colors
            }

            // Initialize cell tracking if enabled
            if (enableCellTracking)
            {
                Vector2 initialTouchLocalPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    gridPanelRect, pointerDownScreenPosition, uiCamera, out initialTouchLocalPos);
                    
                Vector2Int gridCoords = CalculateGridCoordsFromLocalPos(initialTouchLocalPos);
                if (gridCoords.x >= 0 && gridCoords.y >= 0 && 
                    gridCoords.x < wordGridManager.gridSize && gridCoords.y < wordGridManager.gridSize)
                {
                    CellController cellController = wordGridManager.GetCellController(gridCoords);
                    if (cellController != null)
                    {
                        trackedCellID = cellController.uniqueID;
                        trackedCellPosition = gridCoords;
                        isTrackingCell = true;
                        
                        Debug.Log($"🎯 Started tracking cell ID {trackedCellID} at position ({gridCoords.x}, {gridCoords.y})");
                    }
                }
            }

            // Use deadzone logic instead of immediate direction locking
            Vector2 currentPanelPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                gridPanelRect, currentScreenPosition, uiCamera, out currentPanelPosition);
                
            // Handle deadzone and direction determination
            HandleDeadzoneLogic(currentScreenPosition, currentPanelPosition);
            
            RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, eventData.position, uiCamera, out pointerInitialPanelPosition);
            currentFrameVisualRemainderOffsetX = 0;
            currentFrameVisualRemainderOffsetY = 0;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || gameManager.IsAnyAnimationPlaying || gameManager.CurrentStatePublic != GameManager.GameState.Playing || activeSnapAnimationCoroutine != null)
        {
            return;
        }
        AddPointerSample(eventData.position);

        Vector2 currentPanelPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gridPanelRect,
            eventData.position,
            uiCamera,
            out currentPanelPosition
        );

        // Handle deadzone logic first - this may change direction or keep us unlocked
        bool directionChanged = HandleDeadzoneLogic(eventData.position, currentPanelPosition);
        
        // Only proceed with scrolling if we have a locked direction
        if (currentDragDirection == DragDirection.None || (!isHorizontalDragLocked && !isVerticalDragLocked))
        {
            return; // Still in deadzone or no direction established
        }

        // Handle cell tracking and direction switching if enabled
        if (enableCellTracking && isTrackingCell)
        {
            HandleCellTrackingDrag(currentPanelPosition, eventData);
        }
        else
        {
            HandleOriginalDrag(currentPanelPosition);
        }
    }

    /// <summary>
    /// Handle cell tracking drag with direction switching - allows moving to any row/column
    /// </summary>
    private void HandleCellTrackingDrag(Vector2 currentPanelPosition, PointerEventData eventData)
    {
        // Update tracked cell position (in case data shifted from other operations)
        UpdateTrackedCellPosition();
        
        // Calculate finger position in grid coordinates
        Vector2Int fingerGridPos = CalculateGridCoordsFromLocalPos(currentPanelPosition);
        
        // Check if we need to switch direction based on finger movement
        Vector2 movementVector = currentPanelPosition - pointerInitialPanelPosition;
        bool wantsHorizontal = Mathf.Abs(movementVector.x) > Mathf.Abs(movementVector.y);
        
        // Handle direction switching based on movement and finger position
        if (wantsHorizontal && isVerticalDragLocked)
        {
            SwitchToHorizontalDrag(fingerGridPos);
        }
        else if (!wantsHorizontal && isHorizontalDragLocked)
        {
            SwitchToVerticalDrag(fingerGridPos);
        }
        
        // Perform the actual drag movement (this handles visual offsets and data shifts when thresholds are met)
        HandleOriginalDrag(currentPanelPosition);
    }

    /// <summary>
    /// Handle original drag behavior (horizontal OR vertical)
    /// </summary>
    private void HandleOriginalDrag(Vector2 currentPanelPosition)
    {
        if (isHorizontalDragLocked && activeDragRow != -1)
        {
            currentFrameVisualRemainderOffsetX = currentPanelPosition.x - pointerInitialPanelPosition.x;

            if (Mathf.Abs(currentFrameVisualRemainderOffsetX) >= cellDimensionWithSpacing)
            {
                int cellsToShift = Mathf.FloorToInt(currentFrameVisualRemainderOffsetX / cellDimensionWithSpacing);
                if (cellsToShift != 0)
                {
                    wordGridManager.ShiftRowDataAndRefresh(activeDragRow, cellsToShift);
                    currentFrameVisualRemainderOffsetX -= cellsToShift * cellDimensionWithSpacing;
                    dataActuallyShiftedDuringDrag = true;
                    hasDataChangedThisDragGesture = true; // Track data change for entire gesture
                    pointerInitialPanelPosition.x += cellsToShift * cellDimensionWithSpacing;
                    
                    // Update tracked cell position after shift
                    if (enableCellTracking && isTrackingCell)
                    {
                        UpdateTrackedCellPosition();
                    }
                }
            }
            wordGridManager.SetRowVisualOffset(activeDragRow, currentFrameVisualRemainderOffsetX);
        }
        else if (isVerticalDragLocked && activeDragCol != -1)
        {
            currentFrameVisualRemainderOffsetY = currentPanelPosition.y - pointerInitialPanelPosition.y;

            if (Mathf.Abs(currentFrameVisualRemainderOffsetY) >= cellDimensionWithSpacing)
            {
                int cellsToShift = Mathf.FloorToInt(currentFrameVisualRemainderOffsetY / cellDimensionWithSpacing);
                if (cellsToShift != 0)
                {
                    wordGridManager.ShiftColumnDataAndRefresh(activeDragCol, -cellsToShift);
                    currentFrameVisualRemainderOffsetY -= cellsToShift * cellDimensionWithSpacing;
                    dataActuallyShiftedDuringDrag = true;
                    hasDataChangedThisDragGesture = true; // Track data change for entire gesture
                    pointerInitialPanelPosition.y += cellsToShift * cellDimensionWithSpacing;
                    
                    // Update tracked cell position after shift
                    if (enableCellTracking && isTrackingCell)
                    {
                        UpdateTrackedCellPosition();
                    }
                }
            }
            wordGridManager.SetColumnVisualOffset(activeDragCol, currentFrameVisualRemainderOffsetY);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            if (isPointerCurrentlyDown) OnPointerUp(eventData);
            return;
        }
        AddPointerSample(eventData.position);

        float visualRemainderAtRelease = 0f;
        int lineIndex = -1;
        bool horizontalDrag = false;

        if (isHorizontalDragLocked && activeDragRow != -1)
        {
            visualRemainderAtRelease = currentFrameVisualRemainderOffsetX;
            lineIndex = activeDragRow;
            horizontalDrag = true;
        }
        else if (isVerticalDragLocked && activeDragCol != -1)
        {
            visualRemainderAtRelease = currentFrameVisualRemainderOffsetY;
            lineIndex = activeDragCol;
            horizontalDrag = false;
        }
        else
        {
            ResetDragState();
            if (isPointerCurrentlyDown) isPointerCurrentlyDown = false;
            return;
        }

        if (activeSnapAnimationCoroutine != null) StopCoroutine(activeSnapAnimationCoroutine);

        Vector2 flickVelocity = CalculateVelocity();
        bool isFlick = (horizontalDrag && Mathf.Abs(flickVelocity.x) > minFlickVelocity) ||
                       (!horizontalDrag && Mathf.Abs(flickVelocity.y) > minFlickVelocity);

        int dataShiftOnRelease = 0;

        if (isFlick)
        {
            int flickDirection = 0;
            if (horizontalDrag) flickDirection = Math.Sign(flickVelocity.x);
            else flickDirection = Math.Sign(flickVelocity.y);

            if (flickDirection != 0)
            {
                dataShiftOnRelease = horizontalDrag ? flickDirection : -flickDirection;
            }
            else
            {
                if (Mathf.Abs(visualRemainderAtRelease) > cellDimensionWithSpacing * snapToNextThresholdPercentage)
                {
                    dataShiftOnRelease = Math.Sign(visualRemainderAtRelease);
                    if (!horizontalDrag) dataShiftOnRelease *= -1;
                }
            }
        }
        else
        {
            if (Mathf.Abs(visualRemainderAtRelease) > cellDimensionWithSpacing * snapToNextThresholdPercentage)
            {
                dataShiftOnRelease = Math.Sign(visualRemainderAtRelease);
                if (!horizontalDrag) dataShiftOnRelease *= -1;
            }
        }

        activeSnapAnimationCoroutine = StartCoroutine(AnimateSnapAndShift(lineIndex, horizontalDrag, visualRemainderAtRelease, dataShiftOnRelease, isFlick ? flickSnapDuration : releaseSnapDuration, isFlick ? flickEaseType : releaseSnapEaseType));
    }

    private IEnumerator AnimateSnapAndShift(int lineIndex, bool horizontal, float visualStartOffset, int dataShiftSteps, float duration, Ease easeType)
    {
        float animationTargetVisualOffset = dataShiftSteps * cellDimensionWithSpacing;
        if (!horizontal && dataShiftSteps != 0)
        {
            animationTargetVisualOffset = -dataShiftSteps * cellDimensionWithSpacing;
        }

        float currentAnimatedOffset = visualStartOffset;

        Tween snapTween = DOTween.To(() => currentAnimatedOffset, x => currentAnimatedOffset = x, animationTargetVisualOffset, duration)
            .SetEase(easeType)
            .OnUpdate(() =>
            {
                if (horizontal) wordGridManager.SetRowVisualOffset(lineIndex, currentAnimatedOffset);
                else wordGridManager.SetColumnVisualOffset(lineIndex, currentAnimatedOffset);
            })
            .OnComplete(() =>
            {
                bool actualDataShiftOccurredThisSnap = false;
                if (dataShiftSteps != 0)
                {
                    if (horizontal) wordGridManager.ShiftRowDataAndRefresh(lineIndex, dataShiftSteps);
                    else wordGridManager.ShiftColumnDataAndRefresh(lineIndex, dataShiftSteps);
                    actualDataShiftOccurredThisSnap = true;
                    hasDataChangedThisDragGesture = true; // Track data change for entire gesture
                }

                if (horizontal) wordGridManager.SetRowVisualOffset(lineIndex, 0f);
                else wordGridManager.SetColumnVisualOffset(lineIndex, 0f);

                if (horizontal) wordGridManager.SnapRowToGrid(lineIndex);
                else wordGridManager.SnapColumnToGrid(lineIndex);

                bool netDataChangedFromDragStart = false;
                if (dragBeganOnValidLine && initialDragLineData != null)
                {
                    char[] finalDragLineData;
                    if (horizontal) finalDragLineData = wordGridManager.GetRowData(lineIndex);
                    else finalDragLineData = wordGridManager.GetColumnData(lineIndex);

                    if (finalDragLineData != null && !initialDragLineData.SequenceEqual(finalDragLineData))
                    {
                        netDataChangedFromDragStart = true;
                    }
                }
                else if (dataActuallyShiftedDuringDrag || actualDataShiftOccurredThisSnap)
                {
                    netDataChangedFromDragStart = true;
                }

                // Track if any data changed for validation purposes
                if (netDataChangedFromDragStart)
                {
                    hasDataChangedThisDragGesture = true;
                }

                // Move counting is now handled when the final animation completes
                // No move counting here to prevent counting multiple times during direction switches

                if (dataActuallyShiftedDuringDrag || actualDataShiftOccurredThisSnap)
                {
                    wordGridManager.TriggerValidationCheckAndHighlightUpdate();
                }
                
                // Count the move for the completed gesture if this is the final animation
                if (hasDataChangedThisDragGesture && !moveAlreadyCountedThisGesture)
                {
                    wordGridManager.ApplyPendingMoveReduction(lineIndex, horizontal ? -1 : lineIndex, 1);
                    moveAlreadyCountedThisGesture = true;
                    Debug.Log($"🎯 Counted move for completed gesture on {(horizontal ? "row" : "column")} {lineIndex}");
                }
                
                ResetDragStateAfterAnimation();
            });

        yield return snapTween.WaitForCompletion();
    }

    /// <summary>
    /// Handle move counting for completed drag gesture
    /// </summary>
    private void HandleMoveCountingForCompletedGesture()
    {
        if (hasDataChangedThisDragGesture && !moveAlreadyCountedThisGesture)
        {
            // Find which line was affected most recently for move counting
            int lineIndex = -1;
            bool horizontal = false;
            
            if (activeDragRow != -1)
            {
                lineIndex = activeDragRow;
                horizontal = true;
            }
            else if (activeDragCol != -1)
            {
                lineIndex = activeDragCol;
                horizontal = false;
            }
            
            if (lineIndex != -1)
            {
                wordGridManager.ApplyPendingMoveReduction(lineIndex, horizontal ? -1 : lineIndex, 1);
                moveAlreadyCountedThisGesture = true;
                Debug.Log($"🎯 Counted move for completed multi-directional gesture on {(horizontal ? "row" : "column")} {lineIndex}");
            }
        }
    }

    private void ResetDragStateAfterAnimation()
    {
        ResetDragState();
        activeSnapAnimationCoroutine = null;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isPointerCurrentlyDown) return;

        bool wasDraggingBeforeSnapAttempt = isDragging;

        if (activeSnapAnimationCoroutine == null)
        {
            float pressDuration = Time.time - pointerDownTime;
            float moveDistance = Vector2.Distance(eventData.position, pointerDownScreenPosition);

            if (!wasDraggingBeforeSnapAttempt && tapCandidate && pressDuration <= maxTapDuration && moveDistance <= maxTapMoveDistance)
            {
                Vector2 originalPointerDownPanelPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    gridPanelRect,
                    pointerDownScreenPosition,
                    uiCamera,
                    out originalPointerDownPanelPos);

                Vector2Int tappedGridCoords = CalculateGridCoordsFromLocalPos(originalPointerDownPanelPos);
                if (tappedGridCoords.x != -1)
                {
                    gameManager.AttemptTapValidation(tappedGridCoords);
                }
            }

            if (!wasDraggingBeforeSnapAttempt)
            {
                ResetDragState();
            }
        }

        isPointerCurrentlyDown = false;
        tapCandidate = false;
    }

    /// <summary>
    /// Update the tracked cell's current position by finding it by unique ID
    /// </summary>
    private void UpdateTrackedCellPosition()
    {
        if (!isTrackingCell || trackedCellID == -1) return;
        
        Vector2Int currentPos = wordGridManager.FindCellByUniqueID(trackedCellID);
        if (currentPos.x != -1 && currentPos.y != -1)
        {
            trackedCellPosition = currentPos;
        }
    }
    
    /// <summary>
    /// Move the tracked cell towards the target position by shifting grid data
    /// NOTE: This method is disabled to prevent unwanted text changes in all cells
    /// The current approach uses visual-only tracking until snap thresholds are met
    /// </summary>
    private void MoveTrackedCellTowards(Vector2Int targetGridPos)
    {
        // DISABLED: This method was causing all cells in a column/row to change text
        // when trying to follow the tracked cell. The new approach uses visual-only
        // tracking and only shifts data when snap thresholds are actually met.
        return;
        
        /*
        if (!isTrackingCell || trackedCellID == -1) return;
        
        // Calculate the movement needed
        Vector2Int movement = targetGridPos - trackedCellPosition;
        
        if (movement == Vector2Int.zero) return; // No movement needed
        
        // Prioritize the larger movement direction
        if (Mathf.Abs(movement.x) >= Mathf.Abs(movement.y))
        {
            // Move horizontally (shift row)
            if (movement.x != 0)
            {
                int shiftDirection = movement.x > 0 ? 1 : -1;
                wordGridManager.ShiftRowDataAndRefresh(trackedCellPosition.x, shiftDirection);
                dataActuallyShiftedDuringDrag = true;
                hasDataChangedThisDragGesture = true; // Track data change for entire gesture
                
                Debug.Log($"🔄 Shifted row {trackedCellPosition.x} by {shiftDirection} to follow tracked cell {trackedCellID}");
            }
        }
        else
        {
            // Move vertically (shift column)
            if (movement.y != 0)
            {
                int shiftDirection = movement.y > 0 ? -1 : 1; // Negative because of coordinate system
                wordGridManager.ShiftColumnDataAndRefresh(trackedCellPosition.y, shiftDirection);
                dataActuallyShiftedDuringDrag = true;
                hasDataChangedThisDragGesture = true; // Track data change for entire gesture
                
                Debug.Log($"🔄 Shifted column {trackedCellPosition.y} by {shiftDirection} to follow tracked cell {trackedCellID}");
            }
        }
        
        // Update tracked cell position after the shift
        UpdateTrackedCellPosition();
        */
    }
    
    /// <summary>
    /// Switch drag mode to horizontal, snapping the grid and updating drag state
    /// </summary>
    private void SwitchToHorizontalDrag(Vector2Int fingerGridPos = default)
    {
        if (isHorizontalDragLocked) return; // Already horizontal
        
        // Determine which row to use - finger position if valid, otherwise tracked cell position
        Vector2Int targetPos = (fingerGridPos.x >= 0 && fingerGridPos.x < wordGridManager.gridSize) ? fingerGridPos : trackedCellPosition;
        
        Debug.Log($"🔄 Switching to horizontal drag, finger at ({fingerGridPos.x}, {fingerGridPos.y}), using row {targetPos.x}");
        
        // Snap the current vertical column to grid
        if (isVerticalDragLocked && activeDragCol != -1)
        {
            wordGridManager.SetColumnVisualOffset(activeDragCol, 0f);
            wordGridManager.SnapColumnToGrid(activeDragCol);
        }
        
        // Switch to horizontal mode using the target row
        isVerticalDragLocked = false;
        isHorizontalDragLocked = true;
        activeDragCol = -1;
        activeDragRow = targetPos.x; // Use the target row (finger or tracked cell)
        
        // Reset visual offsets
        currentFrameVisualRemainderOffsetX = 0f;
        currentFrameVisualRemainderOffsetY = 0f;
        
        // Update initial drag line data for the new row
        if (activeDragRow != -1)
        {
            cellDimensionWithSpacing = wordGridManager.cellSize + wordGridManager.spacing;
            initialDragLineData = wordGridManager.GetRowData(activeDragRow);
            dragBeganOnValidLine = initialDragLineData != null;
        }
        
        Debug.Log($"✅ Switched to horizontal drag on row {activeDragRow}");
    }
    
    /// <summary>
    /// Switch drag mode to vertical, snapping the grid and updating drag state
    /// </summary>
    private void SwitchToVerticalDrag(Vector2Int fingerGridPos = default)
    {
        if (isVerticalDragLocked) return; // Already vertical
        
        // Determine which column to use - finger position if valid, otherwise tracked cell position
        Vector2Int targetPos = (fingerGridPos.y >= 0 && fingerGridPos.y < wordGridManager.gridSize) ? fingerGridPos : trackedCellPosition;
        
        Debug.Log($"🔄 Switching to vertical drag, finger at ({fingerGridPos.x}, {fingerGridPos.y}), using column {targetPos.y}");
        
        // Snap the current horizontal row to grid
        if (isHorizontalDragLocked && activeDragRow != -1)
        {
            wordGridManager.SetRowVisualOffset(activeDragRow, 0f);
            wordGridManager.SnapRowToGrid(activeDragRow);
        }
        
        // Switch to vertical mode using the target column
        isHorizontalDragLocked = false;
        isVerticalDragLocked = true;
        activeDragRow = -1;
        activeDragCol = targetPos.y; // Use the target column (finger or tracked cell)
        
        // Reset visual offsets
        currentFrameVisualRemainderOffsetX = 0f;
        currentFrameVisualRemainderOffsetY = 0f;
        
        // Update initial drag line data for the new column
        if (activeDragCol != -1)
        {
            cellDimensionWithSpacing = wordGridManager.cellSize + wordGridManager.spacing;
            initialDragLineData = wordGridManager.GetColumnData(activeDragCol);
            dragBeganOnValidLine = initialDragLineData != null;
        }
        
        Debug.Log($"✅ Switched to vertical drag on column {activeDragCol}");
    }

    private Vector2Int CalculateGridCoordsFromLocalPos(Vector2 localPosition)
    {
        if (wordGridManager == null) return new Vector2Int(-1, -1);

        float cDimWithSpacing = wordGridManager.cellSize + wordGridManager.spacing;
        if (cDimWithSpacing <= 0) return new Vector2Int(-1, -1);

        // Use the same calculation as WordGridManager for consistency
        float totalGridWidth = wordGridManager.gridSize * wordGridManager.cellSize + (wordGridManager.gridSize - 1) * wordGridManager.spacing;
        float gridCenterOffsetX = totalGridWidth / 2f - wordGridManager.cellSize / 2f;
        float gridCenterOffsetY = totalGridWidth / 2f - wordGridManager.cellSize / 2f;

        // Calculate which cell this position falls into
        // Reverse the GetBaseCellPosition calculation:
        // xPos = c * (cellSize + spacing) - gridCenterOffset.x
        // yPos = -(r * (cellSize + spacing) - gridCenterOffset.y)
        
        float adjustedX = localPosition.x + gridCenterOffsetX;
        float adjustedY = -localPosition.y + gridCenterOffsetY;

        // Use RoundToInt instead of FloorToInt for center-based cell detection
        // This makes cell boundaries at 0.5, 1.5, 2.5, etc., putting the cell center 
        // at the actual visual center of each cell for more accurate touch detection
        int col = Mathf.RoundToInt(adjustedX / cDimWithSpacing);
        int row = Mathf.RoundToInt(adjustedY / cDimWithSpacing);

        // Debug logging for touch accuracy (can be removed in production)
        if (Application.isEditor)
        {
            float cellFractionalX = (adjustedX / cDimWithSpacing) - col;
            float cellFractionalY = (adjustedY / cDimWithSpacing) - row;
            Debug.Log($"🎯 Touch Detection: LocalPos({localPosition.x:F1},{localPosition.y:F1}) → Cell({row},{col}) | CellFraction({cellFractionalY:F2},{cellFractionalX:F2})");
        }

        if (row >= 0 && row < wordGridManager.gridSize && col >= 0 && col < wordGridManager.gridSize)
        {
            return new Vector2Int(row, col);
        }
        
        // If rounded coordinates are out of bounds, try fallback with floor-based detection
        // This handles edge cases near grid boundaries
        int fallbackCol = Mathf.FloorToInt(adjustedX / cDimWithSpacing);
        int fallbackRow = Mathf.FloorToInt(adjustedY / cDimWithSpacing);
        
        if (fallbackRow >= 0 && fallbackRow < wordGridManager.gridSize && 
            fallbackCol >= 0 && fallbackCol < wordGridManager.gridSize)
        {
            if (Application.isEditor)
            {
                Debug.Log($"🎯 Using fallback detection: ({fallbackRow},{fallbackCol}) instead of ({row},{col})");
            }
            return new Vector2Int(fallbackRow, fallbackCol);
        }
        
        return new Vector2Int(-1, -1);
    }

    private void AddPointerSample(Vector2 position)
    {
        pointerPositionsHistory.Add(position);
        pointerTimesHistory.Add(Time.time);
        while (pointerPositionsHistory.Count > VELOCITY_TRACKING_SAMPLES)
        {
            pointerPositionsHistory.RemoveAt(0);
            pointerTimesHistory.RemoveAt(0);
        }
    }

    private Vector2 CalculateVelocity()
    {
        if (pointerPositionsHistory.Count < 2) return Vector2.zero;

        int lastIndex = pointerPositionsHistory.Count - 1;
        int firstIndex = Mathf.Max(0, lastIndex - (VELOCITY_TRACKING_SAMPLES - 1));
        if (lastIndex == firstIndex) return Vector2.zero;

        Vector2 deltaPosition = pointerPositionsHistory[lastIndex] - pointerPositionsHistory[firstIndex];
        float deltaTime = pointerTimesHistory[lastIndex] - pointerTimesHistory[firstIndex];

        if (deltaTime <= 0.001f) return Vector2.zero;

        return deltaPosition / deltaTime;
    }

    /// <summary>
    /// Check if the current pointer position is within the deadzone
    /// </summary>
    private bool IsWithinDeadzone(Vector2 currentScreenPosition)
    {
        float distance = Vector2.Distance(currentScreenPosition, initialTouchScreenPosition);
        return distance <= centerDeadzoneRadius;
    }
    
    /// <summary>
    /// Determine drag direction based on the vector from initial touch to current position
    /// Only called when exiting deadzone
    /// </summary>
    private DragDirection DetermineDragDirection(Vector2 currentScreenPosition)
    {
        Vector2 dragVector = currentScreenPosition - initialTouchScreenPosition;
        float distance = dragVector.magnitude;
        
        // Must be outside deadzone and past direction lock threshold
        if (distance < centerDeadzoneRadius + directionLockThreshold)
            return DragDirection.None;
        
        // Determine primary direction based on the larger component
        if (Mathf.Abs(dragVector.x) > Mathf.Abs(dragVector.y))
            return DragDirection.Horizontal;
        else
            return DragDirection.Vertical;
    }
    
    /// <summary>
    /// Handle deadzone logic and direction switching
    /// Returns true if direction was established/changed
    /// </summary>
    private bool HandleDeadzoneLogic(Vector2 currentScreenPosition, Vector2 currentPanelPosition)
    {
        bool wasInDeadzone = isInDeadzone;
        isInDeadzone = IsWithinDeadzone(currentScreenPosition);
        
        // If we're in the deadzone, unlock direction if previously locked
        if (isInDeadzone)
        {
            if (currentDragDirection != DragDirection.None)
            {
                Debug.Log($"🎯 Returned to deadzone - unlocking direction (was {currentDragDirection})");
                
                // Snap current line to grid before unlocking
                if (currentDragDirection == DragDirection.Horizontal && activeDragRow != -1)
                {
                    wordGridManager.SetRowVisualOffset(activeDragRow, 0f);
                    wordGridManager.SnapRowToGrid(activeDragRow);
                }
                else if (currentDragDirection == DragDirection.Vertical && activeDragCol != -1)
                {
                    wordGridManager.SetColumnVisualOffset(activeDragCol, 0f);
                    wordGridManager.SnapColumnToGrid(activeDragCol);
                }
                
                // Reset direction state
                currentDragDirection = DragDirection.None;
                isHorizontalDragLocked = false;
                isVerticalDragLocked = false;
                activeDragRow = -1;
                activeDragCol = -1;
                currentFrameVisualRemainderOffsetX = 0f;
                currentFrameVisualRemainderOffsetY = 0f;
            }
            return false; // No direction established
        }
        
        // We're outside deadzone - determine/maintain direction
        DragDirection newDirection = DetermineDragDirection(currentScreenPosition);
        
        if (newDirection == DragDirection.None)
            return false; // Still too close to deadzone
        
        // Check if direction changed
        if (currentDragDirection != newDirection)
        {
            Debug.Log($"🔄 Direction changed from {currentDragDirection} to {newDirection}");
            
            // Set new direction
            currentDragDirection = newDirection;
            
            // Establish new drag line
            Vector2Int gridCoords = CalculateGridCoordsFromLocalPos(currentPanelPosition);
            
            if (newDirection == DragDirection.Horizontal)
            {
                isHorizontalDragLocked = true;
                isVerticalDragLocked = false;
                activeDragRow = gridCoords.x;
                activeDragCol = -1;
                
                if (activeDragRow >= 0 && activeDragRow < wordGridManager.gridSize)
                {
                    cellDimensionWithSpacing = wordGridManager.cellSize + wordGridManager.spacing;
                    initialDragLineData = wordGridManager.GetRowData(activeDragRow);
                    dragBeganOnValidLine = initialDragLineData != null;
                }
            }
            else // Vertical
            {
                isVerticalDragLocked = true;
                isHorizontalDragLocked = false;
                activeDragCol = gridCoords.y;
                activeDragRow = -1;
                
                if (activeDragCol >= 0 && activeDragCol < wordGridManager.gridSize)
                {
                    cellDimensionWithSpacing = wordGridManager.cellSize + wordGridManager.spacing;
                    initialDragLineData = wordGridManager.GetColumnData(activeDragCol);
                    dragBeganOnValidLine = initialDragLineData != null;
                }
            }
            
            return true; // Direction established/changed
        }
        
        return false; // Direction unchanged
    }

#if UNITY_EDITOR
    /// <summary>
    /// Debug visualization of the deadzone (Editor only)
    /// </summary>
    void OnDrawGizmos()
    {
        if (!showDeadzoneDebug || !isPointerCurrentlyDown) return;
        
        // Convert screen position to world position for gizmo drawing
        if (Camera.main != null)
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(initialTouchScreenPosition.x, initialTouchScreenPosition.y, 10f));
            
            // Draw deadzone circle (using sphere wireframe)
            Gizmos.color = isInDeadzone ? Color.green : Color.red;
            Gizmos.DrawWireSphere(worldPos, centerDeadzoneRadius * 0.01f); // Scale for world space
            
            // Draw direction lock threshold
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(worldPos, (centerDeadzoneRadius + directionLockThreshold) * 0.01f);
            
            // Draw current position
            if (isPointerCurrentlyDown)
            {
                Vector3 currentWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 10f));
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(currentWorldPos, 2f * 0.01f);
                
                // Draw line from center to current position
                Gizmos.color = currentDragDirection == DragDirection.None ? Color.white : 
                              currentDragDirection == DragDirection.Horizontal ? Color.red : Color.blue;
                Gizmos.DrawLine(worldPos, currentWorldPos);
            }
        }
    }
#endif
    
    // ===========================
    // SINGLE CELL DIRECTIONAL SCROLLING CONTROL API
    // ===========================
    
    /// <summary>
    /// Enable single cell directional scrolling (cell tracking system)
    /// </summary>
    public void EnableSingleCellDirectionalScrolling()
    {
        enableCellTracking = true;
        Debug.Log("🎮 GridInputHandler: Single cell directional scrolling ENABLED");
    }
    
    /// <summary>
    /// Disable single cell directional scrolling (cell tracking system)
    /// </summary>
    public void DisableSingleCellDirectionalScrolling()
    {
        enableCellTracking = false;
        
        // Clear any active cell tracking
        if (isTrackingCell)
        {
            trackedCellID = -1;
            trackedCellPosition = Vector2Int.zero;
            isTrackingCell = false;
            Debug.Log("🎮 GridInputHandler: Cleared active cell tracking due to system being disabled");
        }
        
        Debug.Log("🎮 GridInputHandler: Single cell directional scrolling DISABLED");
    }
    
    /// <summary>
    /// Toggle single cell directional scrolling on/off
    /// </summary>
    public void ToggleSingleCellDirectionalScrolling()
    {
        if (enableCellTracking)
        {
            DisableSingleCellDirectionalScrolling();
        }
        else
        {
            EnableSingleCellDirectionalScrolling();
        }
    }
    
    /// <summary>
    /// Set single cell directional scrolling state directly
    /// </summary>
    /// <param name="enabled">Whether to enable single cell directional scrolling</param>
    public void SetSingleCellDirectionalScrollingEnabled(bool enabled)
    {
        if (enabled)
        {
            EnableSingleCellDirectionalScrolling();
        }
        else
        {
            DisableSingleCellDirectionalScrolling();
        }
    }
    
    /// <summary>
    /// Check if single cell directional scrolling is enabled
    /// </summary>
    public bool IsSingleCellDirectionalScrollingEnabled => enableCellTracking;
    
    /// <summary>
    /// Check if currently tracking a cell
    /// </summary>
    public bool IsTrackingCell => isTrackingCell;
    
    /// <summary>
    /// Get the currently tracked cell ID (if any)
    /// </summary>
    public int GetTrackedCellID() => isTrackingCell ? trackedCellID : -1;
    
    /// <summary>
    /// Get the currently tracked cell position (if any)
    /// </summary>
    public Vector2Int GetTrackedCellPosition() => isTrackingCell ? trackedCellPosition : Vector2Int.one * -1;
    
    // ===========================
    // INSPECTOR HELPER METHODS
    // ===========================
    
    /// <summary>
    /// Enable single cell scrolling (callable from Inspector context menu)
    /// </summary>
    [ContextMenu("Enable Single Cell Scrolling")]
    public void InspectorEnableSingleCellScrolling()
    {
        EnableSingleCellDirectionalScrolling();
    }
    
    /// <summary>
    /// Disable single cell scrolling (callable from Inspector context menu)
    /// </summary>
    [ContextMenu("Disable Single Cell Scrolling")]
    public void InspectorDisableSingleCellScrolling()
    {
        DisableSingleCellDirectionalScrolling();
    }
    
    /// <summary>
    /// Toggle single cell scrolling (callable from Inspector context menu)
    /// </summary>
    [ContextMenu("Toggle Single Cell Scrolling")]
    public void InspectorToggleSingleCellScrolling()
    {
        ToggleSingleCellDirectionalScrolling();
    }
    
    /// <summary>
    /// Check current single cell scrolling state (callable from Inspector context menu)
    /// </summary>
    [ContextMenu("Check Single Cell Scrolling Status")]
    public void InspectorCheckSingleCellScrollingStatus()
    {
        Debug.Log($"📊 GridInputHandler Single Cell Scrolling Status: {(enableCellTracking ? "🔓 ENABLED" : "🔒 DISABLED")}");
        
        if (isTrackingCell)
        {
            Debug.Log($"📊 Currently tracking cell ID: {trackedCellID} at position: {trackedCellPosition}");
        }
        else
        {
            Debug.Log("📊 No cell currently being tracked");
        }
    }
}