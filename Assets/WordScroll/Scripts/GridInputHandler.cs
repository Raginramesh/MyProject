using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using DG.Tweening;

public class GridInputHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("References")]
    [SerializeField] private WordGridManager wordGridManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private RectTransform gridPanelRect;
    [SerializeField] private Camera uiCamera;

    [Header("Drag Settings")]
    [SerializeField] private float dragThreshold = 20f;
    [SerializeField] private float scrollThresholdFactor = 0.4f;

    [Header("Inertia Settings")]
    [SerializeField] private bool enableInertia = true;
    [SerializeField] private float minFlickVelocity = 300f;
    [Range(0.8f, 0.99f)]
    [SerializeField] private float inertiaDampimgFactor = 0.95f;
    [SerializeField] private float minInertiaSpeed = 30f;
    [SerializeField] private int velocityCalculationSamples = 5;

    [Header("Highlight Settings (Drag)")]
    [SerializeField] private bool enableDragHighlight = true;
    [SerializeField] private float highlightScaleMultiplier = 1.08f;
    [SerializeField] private Color dragHighlightColor = new Color(0.9f, 0.9f, 0.9f, 1f);

    [Header("Tap Settings")]
    [SerializeField] private float maxTapDuration = 0.3f;
    [SerializeField] private float maxTapMoveDistance = 15f;

    private Vector2 pointerDownScreenPosition;
    private Vector2 dragStartLocalPosition;
    private float pointerDownTime;

    private bool isPointerCurrentlyDown = false;
    private bool isDragging = false;
    private bool axisLocked = false;
    private bool isHorizontalDrag;
    private int targetRow = -1;
    private int targetCol = -1;
    private float accumulatedDragDistanceOnAxis = 0f;
    private float cellSizeWithSpacing;

    private struct PointerSample { public Vector2 LocalPosition; public float Time; public PointerSample(Vector2 p, float t) { LocalPosition = p; Time = t; } }
    private List<PointerSample> pointerSamples = new List<PointerSample>();
    private Coroutine inertiaCoroutine = null;
    public bool IsPerformingInertiaScroll { get; private set; } = false;

    private bool pendingValidationHighlightUpdate = false;
    private bool pendingMoveReduction = false;
    private int moveReductionRow = -1;
    private int moveReductionCol = -1;
    private bool dragActuallyScrolledThisInteraction = false;

    // MODIFICATION: Tracks net cell shifts for the primary interacted row/column
    private int netScrollOperations = 0;

    private List<CellController> currentlyDragHighlightedCells = new List<CellController>();
    private List<Image> dragHighlightedImages = new List<Image>();
    private List<Color> dragOriginalColors = new List<Color>();
    private Vector3 originalCellScale = Vector3.one;
    private bool isDragHighlightApplied = false;

    void Awake()
    {
        if (wordGridManager == null) wordGridManager = FindFirstObjectByType<WordGridManager>();
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (gridPanelRect == null) gridPanelRect = GetComponent<RectTransform>();
        if (uiCamera == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas && (canvas.renderMode == RenderMode.ScreenSpaceOverlay || canvas.worldCamera == null))
                uiCamera = Camera.main;
            else if (canvas)
                uiCamera = canvas.worldCamera;
            if (uiCamera == null) uiCamera = Camera.main;
        }

        if (wordGridManager == null || gameManager == null || gridPanelRect == null || uiCamera == null)
        { Debug.LogError("GridInputHandler: Critical reference missing! Disabling.", this); enabled = false; return; }
    }

    void Start()
    {
        if (wordGridManager.gridSize > 0)
        {
            cellSizeWithSpacing = wordGridManager.cellSize + wordGridManager.spacing;
            if (cellSizeWithSpacing <= 0.01f)
            { Debug.LogError("GIH: cellSizeWithSpacing is too small or zero. Check WGM config.", this); enabled = false; return; }
        }
        else { Debug.LogError("GIH: WordGridManager not ready in Start (gridSize=0).", this); enabled = false; return; }

        UpdateOriginalCellScale();
        ResetAllInternalStates();
    }

    void OnEnable() { ResetAllInternalStates(); }
    void OnDisable() { ResetAllInternalStates(); if (isDragHighlightApplied) ForceResetDragHighlightVisuals(); }

    private void ResetAllInternalStates(bool fromPointerUpOrInertiaEnd = false)
    {
        if (!fromPointerUpOrInertiaEnd)
        {
            IsPerformingInertiaScroll = false;
            StopInertiaCoroutine();
            pendingValidationHighlightUpdate = false;
            pendingMoveReduction = false;
            moveReductionRow = -1;
            moveReductionCol = -1;
            netScrollOperations = 0; // MODIFICATION: Reset net scroll operations for new interaction
            targetRow = -1;          // Reset target row/col for a completely new interaction
            targetCol = -1;
        }

        isPointerCurrentlyDown = false;
        isDragging = false;
        axisLocked = false;
        accumulatedDragDistanceOnAxis = 0f;
        dragActuallyScrolledThisInteraction = false;

        if (isDragHighlightApplied) ForceResetDragHighlightVisuals();
        pointerSamples.Clear();
    }

    void Update()
    {
        if (gameManager == null || wordGridManager == null) return;
        if (gameManager.CurrentStatePublic != GameManager.GameState.Playing) return;

        bool gmAnimating = gameManager.IsAnyAnimationPlaying;
        bool wgmAnimating = wordGridManager.isAnimating;

        if (gmAnimating || wgmAnimating || IsPerformingInertiaScroll)
        {
            return;
        }

        if (pendingMoveReduction)
        {
            pendingMoveReduction = false;
            wordGridManager.ApplyPendingMoveReduction(moveReductionRow, moveReductionCol);
            moveReductionRow = -1;
            moveReductionCol = -1;
        }

        if (pendingValidationHighlightUpdate)
        {
            pendingValidationHighlightUpdate = false;
            wordGridManager.TriggerValidationCheckAndHighlightUpdate();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (gameManager.IsAnyAnimationPlaying && !IsPerformingInertiaScroll)
        { return; }

        if (IsPerformingInertiaScroll)
        {
            StopInertiaCoroutine();
        }
        ResetAllInternalStates(false); // This now also resets netScrollOperations

        isPointerCurrentlyDown = true;
        pointerDownScreenPosition = eventData.position;
        pointerDownTime = Time.unscaledTime;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, pointerDownScreenPosition, uiCamera, out Vector2 initialLocalPos);
        pointerSamples.Clear();
        AddPointerSample(initialLocalPos);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!isPointerCurrentlyDown) { return; }
        if (gameManager.IsAnyAnimationPlaying && !IsPerformingInertiaScroll) { return; }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isPointerCurrentlyDown) return;
        if (gameManager.IsAnyAnimationPlaying && !IsPerformingInertiaScroll) return;

        Vector2 currentScreenPos = eventData.position;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, currentScreenPos, uiCamera, out Vector2 currentLocalPos);
        AddPointerSample(currentLocalPos);

        if (!isDragging)
        {
            float screenDistanceMoved = Vector2.Distance(pointerDownScreenPosition, currentScreenPos);
            if (screenDistanceMoved > dragThreshold)
            {
                isDragging = true;
                axisLocked = false;
                dragStartLocalPosition = currentLocalPos;
                accumulatedDragDistanceOnAxis = 0f;

                Vector2 screenDragVector = currentScreenPos - pointerDownScreenPosition;
                if (!axisLocked)
                {
                    axisLocked = true;
                    isHorizontalDrag = Mathf.Abs(screenDragVector.x) > Mathf.Abs(screenDragVector.y);
                    CalculateTargetRowColForDrag(dragStartLocalPosition);
                    if (enableDragHighlight && targetRow != -1 && targetCol != -1)
                    {
                        if (isHorizontalDrag) ApplyDragHighlightRow(targetRow);
                        else ApplyDragHighlightColumn(targetCol);
                    }
                }
            }
            else { return; }
        }

        if (wordGridManager.isAnimating && !IsPerformingInertiaScroll)
        {
            dragStartLocalPosition = currentLocalPos;
            accumulatedDragDistanceOnAxis = 0f;
            return;
        }

        float frameDragDeltaOnAxis = isHorizontalDrag ? (currentLocalPos.x - dragStartLocalPosition.x) : (currentLocalPos.y - dragStartLocalPosition.y);
        accumulatedDragDistanceOnAxis += frameDragDeltaOnAxis;

        float scrollTriggerDistance = cellSizeWithSpacing * scrollThresholdFactor;
        if (Mathf.Abs(accumulatedDragDistanceOnAxis) >= scrollTriggerDistance)
        {
            if (wordGridManager.isAnimating)
            {
                dragStartLocalPosition = currentLocalPos;
                accumulatedDragDistanceOnAxis = 0;
                return;
            }

            int scrollDirection = (int)Mathf.Sign(accumulatedDragDistanceOnAxis);
            float scrollAmountAbs = Mathf.Abs(accumulatedDragDistanceOnAxis);
            bool scrollRequestedThisFrame = false;
            Sequence scrollSequence = null;
            int wgmEffectiveDirection = 0; // To store the direction WGM's data actually shifts

            if (isHorizontalDrag && targetRow != -1)
            {
                wgmEffectiveDirection = scrollDirection;
                scrollSequence = wordGridManager.RequestRowScroll(targetRow, wgmEffectiveDirection, scrollAmountAbs);
                scrollRequestedThisFrame = true;
            }
            else if (!isHorizontalDrag && targetCol != -1)
            {
                wgmEffectiveDirection = -scrollDirection; // WGM's RequestColumnScroll inverts UI Y-drag for data shift
                scrollSequence = wordGridManager.RequestColumnScroll(targetCol, wgmEffectiveDirection, scrollAmountAbs);
                scrollRequestedThisFrame = true;
            }

            if (scrollRequestedThisFrame && scrollSequence != null) // Ensure scroll was actually initiated by WGM
            {
                if (!dragActuallyScrolledThisInteraction)
                    dragActuallyScrolledThisInteraction = true;

                netScrollOperations += wgmEffectiveDirection; // MODIFICATION: Update net operations
                // Debug.Log($"GIH OnDrag: Scrolled. NetOps: {netScrollOperations}");

                accumulatedDragDistanceOnAxis -= scrollDirection * scrollTriggerDistance;
            }
        }
        dragStartLocalPosition = currentLocalPos;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isPointerCurrentlyDown) { return; }

        bool wasDraggingBeforeUp = isDragging;
        bool didActuallyScrollInThisDragCycle = dragActuallyScrolledThisInteraction;

        if (isDragHighlightApplied) { ResetDragHighlight(); }

        isPointerCurrentlyDown = false;
        isDragging = false;

        if (wasDraggingBeforeUp)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, eventData.position, uiCamera, out Vector2 finalLocalPos);
            AddPointerSample(finalLocalPos);

            if (enableInertia && axisLocked)
            {
                Vector2 releaseVelocity = CalculateVelocity();
                float speedOnAxis = isHorizontalDrag ? Mathf.Abs(releaseVelocity.x) : Mathf.Abs(releaseVelocity.y);

                if (speedOnAxis > minFlickVelocity)
                {
                    float relevantVelocityComponent = isHorizontalDrag ? releaseVelocity.x : releaseVelocity.y;
                    if (inertiaCoroutine != null) StopCoroutine(inertiaCoroutine);
                    inertiaCoroutine = StartCoroutine(InertiaScrollCoroutine(relevantVelocityComponent, isHorizontalDrag, targetRow, targetCol, didActuallyScrollInThisDragCycle));
                    return;
                }
            }

            if (didActuallyScrollInThisDragCycle)
            {
                pendingValidationHighlightUpdate = true;
                if (gameManager.CurrentGameDisplayMode == GameManager.DisplayMode.Moves)
                {
                    // MODIFICATION: Check netScrollOperations
                    if (netScrollOperations != 0)
                    {
                        // Debug.Log($"GIH OnPointerUp: Drag ended, scroll occurred, no inertia. NetOps: {netScrollOperations}. Flagging Move Reduction. Row: {targetRow}, Col: {targetCol}");
                        pendingMoveReduction = true;
                        moveReductionRow = isHorizontalDrag ? targetRow : -1;
                        moveReductionCol = isHorizontalDrag ? -1 : targetCol;
                    }
                    else
                    {
                        // Debug.Log($"GIH OnPointerUp: Drag ended, scroll occurred, no inertia. NetOps: {netScrollOperations}. NO Move Reduction.");
                    }
                }
            }
            else
            {
                pendingValidationHighlightUpdate = true; // Still validate if it was a drag, even if no scroll
            }
        }
        else
        {
            float duration = Time.unscaledTime - pointerDownTime;
            float distance = Vector2.Distance(pointerDownScreenPosition, eventData.position);
            if (duration <= maxTapDuration && distance <= maxTapMoveDistance)
            {
                if (!gameManager.IsAnyAnimationPlaying)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, eventData.position, uiCamera, out Vector2 localTapPos);
                    Vector2Int tappedGridCoord = CalculateGridCoordsFromLocalPos(localTapPos);
                    if (tappedGridCoord.x != -1 && tappedGridCoord.y != -1)
                    {
                        bool wordProcessed = gameManager.AttemptTapValidation(tappedGridCoord);
                        if (!wordProcessed)
                        {
                            pendingValidationHighlightUpdate = true;
                        }
                    }
                }
            }
            else
            {
                pendingValidationHighlightUpdate = true;
            }
        }
        ResetAllInternalStates(true);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!IsPerformingInertiaScroll && isPointerCurrentlyDown)
        {
            ResetAllInternalStates(true);
        }
    }

    private void AddPointerSample(Vector2 localPositionOnPanel)
    {
        pointerSamples.Add(new PointerSample(localPositionOnPanel, Time.unscaledTime));
        while (pointerSamples.Count > velocityCalculationSamples && velocityCalculationSamples > 0)
        { pointerSamples.RemoveAt(0); }
    }

    private Vector2 CalculateVelocity()
    {
        if (pointerSamples.Count < 2) return Vector2.zero;
        PointerSample first = pointerSamples[0];
        PointerSample last = pointerSamples[pointerSamples.Count - 1];
        float timeDelta = last.Time - first.Time;
        if (timeDelta <= 0.001f) return Vector2.zero;
        return (last.LocalPosition - first.LocalPosition) / timeDelta;
    }

    private IEnumerator InertiaScrollCoroutine(float initialAxisVelocityLocal, bool forHorizontal, int rowForInertia, int colForInertia, bool scrollAlreadyHappenedDuringDrag)
    {
        IsPerformingInertiaScroll = true;
        bool inertiaItselfCausedScroll = false;

        float currentAxisVelocityLocal = initialAxisVelocityLocal;
        float inertiaAccumulatedScrollDistance = 0f;

        if (forHorizontal && (rowForInertia < 0 || rowForInertia >= wordGridManager.gridSize))
        { IsPerformingInertiaScroll = false; inertiaCoroutine = null; ResetAllInternalStates(true); yield break; }
        if (!forHorizontal && (colForInertia < 0 || colForInertia >= wordGridManager.gridSize))
        { IsPerformingInertiaScroll = false; inertiaCoroutine = null; ResetAllInternalStates(true); yield break; }

        while (Mathf.Abs(currentAxisVelocityLocal) > minInertiaSpeed)
        {
            if (gameManager == null || gameManager.CurrentStatePublic != GameManager.GameState.Playing || wordGridManager == null)
            { IsPerformingInertiaScroll = false; inertiaCoroutine = null; ResetAllInternalStates(true); yield break; }

            currentAxisVelocityLocal *= inertiaDampimgFactor;
            float moveAmountThisFrameLocal = currentAxisVelocityLocal * Time.deltaTime;
            inertiaAccumulatedScrollDistance += moveAmountThisFrameLocal;

            if (Mathf.Abs(inertiaAccumulatedScrollDistance) >= cellSizeWithSpacing * scrollThresholdFactor)
            {
                yield return new WaitUntil(() => !wordGridManager.isAnimating);
                if (gameManager == null || gameManager.CurrentStatePublic != GameManager.GameState.Playing)
                { IsPerformingInertiaScroll = false; inertiaCoroutine = null; ResetAllInternalStates(true); yield break; }

                int scrollDirection = (int)Mathf.Sign(inertiaAccumulatedScrollDistance); // UI perspective
                float scrollAmountAbs = Mathf.Abs(inertiaAccumulatedScrollDistance);
                Sequence inertiaScrollSequence = null;
                int wgmEffectiveDirection = 0;

                if (forHorizontal)
                {
                    wgmEffectiveDirection = scrollDirection;
                    inertiaScrollSequence = wordGridManager.RequestRowScroll(rowForInertia, wgmEffectiveDirection, scrollAmountAbs);
                }
                else
                { // Vertical
                    wgmEffectiveDirection = -scrollDirection; // WGM's RequestColumnScroll inverts UI Y-drag for data shift
                    inertiaScrollSequence = wordGridManager.RequestColumnScroll(colForInertia, wgmEffectiveDirection, scrollAmountAbs);
                }

                if (inertiaScrollSequence != null && inertiaScrollSequence.IsActive())
                {
                    yield return inertiaScrollSequence.WaitForCompletion();
                }
                else
                {
                    break;
                }

                if (!inertiaItselfCausedScroll) inertiaItselfCausedScroll = true;

                netScrollOperations += wgmEffectiveDirection; // MODIFICATION: Update net operations
                                                              // Debug.Log($"GIH Inertia: Scrolled. NetOps: {netScrollOperations}");

                inertiaAccumulatedScrollDistance -= scrollDirection * (cellSizeWithSpacing * scrollThresholdFactor);
            }
            if (Mathf.Abs(currentAxisVelocityLocal) <= minInertiaSpeed) break;
            yield return null;
        }

        bool anyScrollInTotalInteraction = scrollAlreadyHappenedDuringDrag || inertiaItselfCausedScroll;

        IsPerformingInertiaScroll = false;
        inertiaCoroutine = null;
        ResetAllInternalStates(true);

        if (anyScrollInTotalInteraction)
        {
            pendingValidationHighlightUpdate = true;
            if (gameManager.CurrentGameDisplayMode == GameManager.DisplayMode.Moves)
            {
                // MODIFICATION: Check netScrollOperations
                if (netScrollOperations != 0)
                {
                    // Debug.Log($"GIH Inertia: END. NetOps: {netScrollOperations}. Flagging Move Reduction. Row: {rowForInertia}, Col: {colForInertia}");
                    pendingMoveReduction = true;
                    moveReductionRow = forHorizontal ? rowForInertia : -1;
                    moveReductionCol = forHorizontal ? -1 : colForInertia;
                }
                else
                {
                    // Debug.Log($"GIH Inertia: END. NetOps: {netScrollOperations}. NO Move Reduction.");
                }
            }
        }
    }

    private void StopInertiaCoroutine()
    {
        if (inertiaCoroutine != null)
        { StopCoroutine(inertiaCoroutine); inertiaCoroutine = null; }
        IsPerformingInertiaScroll = false;
    }

    private void CalculateTargetRowColForDrag(Vector2 localPositionOnPanel)
    {
        Vector2Int gridCoords = CalculateGridCoordsFromLocalPos(localPositionOnPanel);
        targetRow = gridCoords.x;
        targetCol = gridCoords.y;
    }

    private Vector2Int CalculateGridCoordsFromLocalPos(Vector2 localPosition)
    {
        if (wordGridManager == null || cellSizeWithSpacing <= 0.001f) return new Vector2Int(-1, -1);
        float totalGridVisualWidth = wordGridManager.gridSize * wordGridManager.cellSize + Mathf.Max(0, wordGridManager.gridSize - 1) * wordGridManager.spacing;
        float gridContentStartX = -totalGridVisualWidth / 2f;
        float gridContentTopY = totalGridVisualWidth / 2f;
        float xInGridContent = localPosition.x - gridContentStartX;
        float yFromTopInGridContent = gridContentTopY - localPosition.y;
        int c = Mathf.FloorToInt(xInGridContent / cellSizeWithSpacing);
        int r = Mathf.FloorToInt(yFromTopInGridContent / cellSizeWithSpacing);
        if (c < 0 || c >= wordGridManager.gridSize || r < 0 || r >= wordGridManager.gridSize) { return new Vector2Int(-1, -1); }
        return new Vector2Int(r, c);
    }

    private void UpdateOriginalCellScale()
    {
        if (wordGridManager != null && wordGridManager.gridSize > 0)
        {
            CellController sample = wordGridManager.GetCellController(new Vector2Int(0, 0));
            if (sample != null && sample.RectTransform != null) originalCellScale = sample.RectTransform.localScale;
            else originalCellScale = Vector3.one;
        }
        else originalCellScale = Vector3.one;
    }

    private void ApplyDragHighlightRow(int rowIndex)
    {
        if (!enableDragHighlight || isDragHighlightApplied || wordGridManager == null || rowIndex < 0 || rowIndex >= wordGridManager.gridSize) return;
        ForceResetDragHighlightVisuals();
        for (int c = 0; c < wordGridManager.gridSize; c++)
        { AddCellToDragHighlight(wordGridManager.GetCellController(new Vector2Int(rowIndex, c))); }
        isDragHighlightApplied = currentlyDragHighlightedCells.Count > 0;
    }

    private void ApplyDragHighlightColumn(int colIndex)
    {
        if (!enableDragHighlight || isDragHighlightApplied || wordGridManager == null || colIndex < 0 || colIndex >= wordGridManager.gridSize) return;
        ForceResetDragHighlightVisuals();
        for (int r = 0; r < wordGridManager.gridSize; r++)
        { AddCellToDragHighlight(wordGridManager.GetCellController(new Vector2Int(r, colIndex))); }
        isDragHighlightApplied = currentlyDragHighlightedCells.Count > 0;
    }

    private void AddCellToDragHighlight(CellController cc)
    {
        if (cc == null || cc.RectTransform == null) return;
        cc.RectTransform.DOKill();
        cc.RectTransform.localScale = originalCellScale * highlightScaleMultiplier;
        currentlyDragHighlightedCells.Add(cc);
        Image img = cc.GetComponent<Image>() ?? cc.GetComponentInChildren<Image>();
        if (img != null)
        {
            img.DOKill();
            dragHighlightedImages.Add(img);
            dragOriginalColors.Add(img.color);
            img.color = dragHighlightColor;
        }
        else
        {
            dragHighlightedImages.Add(null);
            dragOriginalColors.Add(Color.clear);
        }
    }

    private void ResetDragHighlight() { ForceResetDragHighlightVisuals(); }

    private void ForceResetDragHighlightVisuals()
    {
        for (int i = 0; i < currentlyDragHighlightedCells.Count; i++)
        {
            CellController cc = currentlyDragHighlightedCells[i];
            if (cc != null && cc.RectTransform != null)
            {
                cc.RectTransform.DOKill();
                cc.RectTransform.localScale = originalCellScale;
                if (i < dragHighlightedImages.Count && dragHighlightedImages[i] != null && i < dragOriginalColors.Count)
                {
                    dragHighlightedImages[i].DOKill();
                    dragHighlightedImages[i].color = dragOriginalColors[i];
                }
            }
        }
        currentlyDragHighlightedCells.Clear();
        dragHighlightedImages.Clear();
        dragOriginalColors.Clear();
        isDragHighlightApplied = false;
    }
}