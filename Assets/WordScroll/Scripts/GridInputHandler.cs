using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Collections;
using DG.Tweening;
using System;
using System.Linq;

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
    private const int VELOCITY_TRACKING_SAMPLES = 5;
    private Coroutine activeSnapAnimationCoroutine = null;

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

            Vector2 initialTouchLocalPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                gridPanelRect, pointerDownScreenPosition, uiCamera, out initialTouchLocalPos);
            
            // Always use finger position to determine which row/column to start with
            Vector2Int gridCoords = CalculateGridCoordsFromLocalPos(initialTouchLocalPos);
            
            if (enableCellTracking && isTrackingCell)
            {
                Debug.Log($"🎯 Starting drag at finger position ({gridCoords.x}, {gridCoords.y}), tracking cell {trackedCellID} at ({trackedCellPosition.x}, {trackedCellPosition.y})");
            }

            Vector2 dragVector = currentScreenPosition - pointerDownScreenPosition;

            if (Mathf.Abs(dragVector.x) > Mathf.Abs(dragVector.y))
            {
                isHorizontalDragLocked = true;
                activeDragRow = gridCoords.x;
                activeDragCol = -1;
                if (activeDragRow != -1)
                {
                    cellDimensionWithSpacing = wordGridManager.cellSize + wordGridManager.spacing;
                    initialDragLineData = wordGridManager.GetRowData(activeDragRow);
                    dragBeganOnValidLine = initialDragLineData != null;
                }
                else { isDragging = false; isHorizontalDragLocked = false; return; }
            }
            else
            {
                isVerticalDragLocked = true;
                activeDragCol = gridCoords.y;
                activeDragRow = -1;
                if (activeDragCol != -1)
                {
                    cellDimensionWithSpacing = wordGridManager.cellSize + wordGridManager.spacing;
                    initialDragLineData = wordGridManager.GetColumnData(activeDragCol);
                    dragBeganOnValidLine = initialDragLineData != null;
                }
                else { isDragging = false; isVerticalDragLocked = false; return; }
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, eventData.position, uiCamera, out pointerInitialPanelPosition);
            currentFrameVisualRemainderOffsetX = 0;
            currentFrameVisualRemainderOffsetY = 0;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || (!isHorizontalDragLocked && !isVerticalDragLocked) || gameManager.IsAnyAnimationPlaying || gameManager.CurrentStatePublic != GameManager.GameState.Playing || activeSnapAnimationCoroutine != null)
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

                if (netDataChangedFromDragStart)
                {
                    wordGridManager.ApplyPendingMoveReduction(lineIndex, horizontal ? -1 : lineIndex, 1);
                }

                if (dataActuallyShiftedDuringDrag || actualDataShiftOccurredThisSnap)
                {
                    wordGridManager.TriggerValidationCheckAndHighlightUpdate();
                }
                ResetDragStateAfterAnimation();
            });

        yield return snapTween.WaitForCompletion();
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

        float gridContentWidth = wordGridManager.gridSize * wordGridManager.cellSize + (wordGridManager.gridSize - 1) * wordGridManager.spacing;

        float gridStartX = -gridContentWidth / 2f;
        float gridStartY = gridContentWidth / 2f;

        float xInGrid = localPosition.x - gridStartX;
        float yInGrid = gridStartY - localPosition.y;

        int col = Mathf.FloorToInt(xInGrid / cDimWithSpacing);
        int row = Mathf.FloorToInt(yInGrid / cDimWithSpacing);

        if (row >= 0 && row < wordGridManager.gridSize && col >= 0 && col < wordGridManager.gridSize)
        {
            return new Vector2Int(row, col);
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
}