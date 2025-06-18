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
            Vector2Int gridCoords = CalculateGridCoordsFromLocalPos(initialTouchLocalPos);

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
                    // Use GameManager's GetCurrentPotentialWords to get the list
                    if (gameManager != null && gameManager.CurrentStatePublic == GameManager.GameState.Playing && !gameManager.IsAnyAnimationPlaying)
                    {
                        var potentialWords = gameManager.GetCurrentPotentialWords();
                        var selectedWords = new System.Collections.Generic.List<FoundWordData>();
                        foreach (var word in potentialWords)
                        {
                            if (word.Coordinates != null && word.Coordinates.Contains(tappedGridCoords))
                            {
                                selectedWords.Add(word);
                            }
                        }
                        if (selectedWords.Count > 0)
                        {
                            // Optionally filter subwords as in the original AttemptTapValidation
                            // If needed, call a filter method here
                            // Start the scoring/animation coroutine
                            gameManager.StartCoroutine(gameManager.ProcessWordsSequentially(selectedWords));
                        }
                    }
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