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
    [SerializeField] private float inertiaDampingFactor = 0.95f;
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
    private bool isHorizontalDrag = false;
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
        if (!fromPointerUpOrInertiaEnd) // If it's a brand new interaction (e.g. OnPointerDown)
        {
            IsPerformingInertiaScroll = false; // Stop any old inertia
            StopInertiaCoroutine();
            pendingValidationHighlightUpdate = false; // Crucially, clear pending flags for a new interaction
            pendingMoveReduction = false;
        }
        // else: if fromPointerUpOrInertiaEnd is true, it means an interaction phase just ended.
        // We *don't* clear pendingValidationHighlightUpdate here because it might have *just been set*
        // by the logic in OnPointerUp (no inertia) or at the end of InertiaScrollCoroutine.
        // It will be cleared by the Update() loop once processed.

        isPointerCurrentlyDown = false;
        isDragging = false;
        axisLocked = false;
        accumulatedDragDistanceOnAxis = 0f;
        targetRow = -1;
        targetCol = -1;
        dragActuallyScrolledThisInteraction = false;

        // These are generally safe to clear if not in active inertia.
        // If fromPointerUpOrInertiaEnd is true, and IsPerformingInertiaScroll is *still* true (shouldn't happen with current logic),
        // then these might be cleared too early for a move reduction during inertia.
        // However, pendingMoveReduction is set *during* inertia/drag, and processed by Update loop.
        if (!IsPerformingInertiaScroll)
        {
            // Only clear these if no inertia is active. If inertia *just finished*, these might have been set by it.
            // The Update loop should consume them.
            // This part of ResetAllInternalStates needs careful thought if flags are being missed.
            // For now, if inertia just finished and set pendingMoveReduction, this will clear it IF called immediately.
            // The current flow: Inertia sets pendingMoveReduction -> Inertia sets IsPerformingInertiaScroll=false -> Inertia calls ResetAllInternalStates(true)
            // -> ResetAllInternalStates sees IsPerformingInertiaScroll=false -> Clears pendingMoveReduction.
            // This is an issue. pendingMoveReduction should persist until Update.

            // Let's adjust: pending flags are only reset for a truly new interaction (OnPointerDown)
            // or after they have been consumed by Update.
            // For now, the OnPointerDown reset is the most critical for these.
        }


        if (isDragHighlightApplied) ForceResetDragHighlightVisuals();
        pointerSamples.Clear();
        // Debug.Log($"GIH ResetAllInternalStates: fromPointerUpOrInertiaEnd={fromPointerUpOrInertiaEnd}, IsPerformingInertiaScroll={IsPerformingInertiaScroll}, pendingValidation={pendingValidationHighlightUpdate}");
    }


    void Update()
    {
        if (gameManager == null || wordGridManager == null) return;

        if (gameManager.CurrentStatePublic != GameManager.GameState.Playing) return;

        bool gmAnimating = gameManager.IsAnyAnimationPlaying;
        bool wgmAnimating = wordGridManager.isAnimating;

        if (gmAnimating || wgmAnimating)
        {
            // Debug.Log($"GIH Update: DEFERRED. GM Animating: {gmAnimating}, WGM Animating: {wgmAnimating}. pendingValidation: {pendingValidationHighlightUpdate}. Time: {Time.time:F2}");
            return;
        }

        if (pendingMoveReduction)
        {
            // Debug.Log($"GIH Update: Processing PENDING MOVE REDUCTION. Row: {moveReductionRow}, Col: {moveReductionCol}. Time: {Time.time:F2}");
            pendingMoveReduction = false; // Consume the flag
            wordGridManager.ApplyPendingMoveReduction(moveReductionRow, moveReductionCol);
            moveReductionRow = -1; moveReductionCol = -1;
        }

        if (pendingValidationHighlightUpdate)
        {
            Debug.Log($"GIH Update: PROCESSING PENDING VALIDATION/HIGHLIGHT UPDATE. Time: {Time.time:F2}");
            pendingValidationHighlightUpdate = false; // Consume the flag
            wordGridManager.TriggerValidationCheckAndHighlightUpdate();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Debug.Log($"GIH OnPointerDown. Time: {Time.time:F2}");
        if (gameManager.IsAnyAnimationPlaying && !IsPerformingInertiaScroll)
        { return; }

        if (IsPerformingInertiaScroll)
        {
            // Debug.Log("GIH OnPointerDown: Stopping inertia.");
            StopInertiaCoroutine(); // This will also set IsPerformingInertiaScroll = false
        }
        // THIS IS A NEW INTERACTION START. Clear all flags from any previous interaction.
        ResetAllInternalStates(false); // 'false' signifies a new interaction, not the end of one.

        isPointerCurrentlyDown = true;
        pointerDownScreenPosition = eventData.position;
        pointerDownTime = Time.unscaledTime;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, pointerDownScreenPosition, uiCamera, out Vector2 initialLocalPos);
        pointerSamples.Clear(); // Cleared in ResetAllInternalStates, but good to be explicit for new sample set
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
                // dragActuallyScrolledThisInteraction is reset in OnPointerDown via ResetAllInternalStates(false)

                Vector2 screenDragVector = currentScreenPos - pointerDownScreenPosition;
                if (!axisLocked)
                {
                    axisLocked = true;
                    isHorizontalDrag = Mathf.Abs(screenDragVector.x) > Mathf.Abs(screenDragVector.y);
                    // Debug.Log($"GIH OnDrag: DRAG CONFIRMED & AXIS LOCKED. isHorizontalDrag={isHorizontalDrag}");
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
        { dragStartLocalPosition = currentLocalPos; return; }

        float frameDragDeltaOnAxis = isHorizontalDrag ? (currentLocalPos.x - dragStartLocalPosition.x) : (currentLocalPos.y - dragStartLocalPosition.y);
        accumulatedDragDistanceOnAxis += frameDragDeltaOnAxis;

        float scrollTriggerDistance = cellSizeWithSpacing * scrollThresholdFactor;
        if (Mathf.Abs(accumulatedDragDistanceOnAxis) >= scrollTriggerDistance)
        {
            if (wordGridManager.isAnimating)
            { dragStartLocalPosition = currentLocalPos; accumulatedDragDistanceOnAxis = 0; return; }

            int scrollDirection = (int)Mathf.Sign(accumulatedDragDistanceOnAxis);
            float scrollAmountAbs = Mathf.Abs(accumulatedDragDistanceOnAxis);
            bool scrollRequestedThisFrame = false;

            if (isHorizontalDrag && targetRow != -1)
            {
                wordGridManager.RequestRowScroll(targetRow, scrollDirection, scrollAmountAbs);
                scrollRequestedThisFrame = true;
            }
            else if (!isHorizontalDrag && targetCol != -1)
            {
                wordGridManager.RequestColumnScroll(targetCol, -scrollDirection, scrollAmountAbs);
                scrollRequestedThisFrame = true;
            }

            if (scrollRequestedThisFrame)
            {
                if (!dragActuallyScrolledThisInteraction) Debug.Log($"GIH OnDrag: First scroll of this interaction occurred. Time: {Time.time:F2}");
                dragActuallyScrolledThisInteraction = true;
                accumulatedDragDistanceOnAxis -= scrollDirection * scrollTriggerDistance;
                if (gameManager.CurrentGameDisplayMode == GameManager.DisplayMode.Moves)
                {
                    pendingMoveReduction = true;
                    moveReductionRow = isHorizontalDrag ? targetRow : -1;
                    moveReductionCol = isHorizontalDrag ? -1 : targetCol;
                }
            }
        }
        dragStartLocalPosition = currentLocalPos;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // Debug.Log($"GIH OnPointerUp. Time: {Time.time:F2}");
        if (!isPointerCurrentlyDown) { return; }

        bool wasDraggingBeforeUp = isDragging;
        bool didActuallyScrollInThisDragCycle = dragActuallyScrolledThisInteraction;

        if (isDragHighlightApplied) { ResetDragHighlight(); }

        // Temporarily set isPointerCurrentlyDown to false. If inertia starts, it's still part of this "interaction"
        // but the pointer itself is up. ResetAllInternalStates at the very end will handle full state.
        isPointerCurrentlyDown = false;
        isDragging = false; // Drag gesture itself is over, even if inertia follows.


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
                    // Debug.Log($"GIH OnPointerUp: Starting INERTIA. didActuallyScrollInThisDragCycle before inertia: {didActuallyScrollInThisDragCycle}");
                    if (inertiaCoroutine != null) StopCoroutine(inertiaCoroutine); // Should be stopped by OnPointerDown if new touch

                    inertiaCoroutine = StartCoroutine(InertiaScrollCoroutine(relevantVelocityComponent, isHorizontalDrag, targetRow, targetCol, didActuallyScrollInThisDragCycle));
                    // isPointerCurrentlyDown and isDragging are already false.
                    // IsPerformingInertiaScroll is set by coroutine.
                    // DO NOT call ResetAllInternalStates here if inertia starts. Coroutine will handle it.
                    return;
                }
            }

            // If drag ended, NO inertia:
            if (didActuallyScrollInThisDragCycle)
            {
                pendingValidationHighlightUpdate = true;
                Debug.Log($"GIH OnPointerUp: DRAG ENDED (NO INERTIA), SCROLL OCCURRED. Flagged for validation. didActuallyScrollInThisDragCycle={didActuallyScrollInThisDragCycle}. Time: {Time.time:F2}");
            }
            // else Debug.Log($"GIH OnPointerUp: Drag ended, no scroll, no inertia. didActuallyScrollInThisDragCycle={didActuallyScrollInThisDragCycle}. Time: {Time.time:F2}");
        }
        else // Was NOT a drag, consider it a TAP
        {
            float duration = Time.unscaledTime - pointerDownTime;
            float distance = Vector2.Distance(pointerDownScreenPosition, eventData.position);
            if (duration <= maxTapDuration && distance <= maxTapMoveDistance)
            {
                if (!gameManager.IsAnyAnimationPlaying) // Check GM animation for taps too
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, eventData.position, uiCamera, out Vector2 localTapPos);
                    Vector2Int tappedGridCoord = CalculateGridCoordsFromLocalPos(localTapPos);
                    if (tappedGridCoord.x != -1 && tappedGridCoord.y != -1)
                    {
                        gameManager.AttemptTapValidation(tappedGridCoord);
                        // If tap validation directly changes grid and needs immediate re-check by WGM:
                        // pendingValidationHighlightUpdate = true; 
                        // Debug.Log($"GIH OnPointerUp: TAP. Flagging validation. Time: {Time.time:F2}");
                    }
                }
            }
        }
        // If no inertia started, or if it wasn't a drag, then this interaction is fully over.
        // Reset all states. Pass 'true' because it's the end of a pointer up sequence.
        ResetAllInternalStates(true);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // This is called AFTER OnPointerUp if a drag gesture was significant enough
        // to be registered by the EventSystem as a drag (i.e., OnBeginDrag & OnDrag were called).
        // Debug.Log($"GIH OnEndDrag. IsPerformingInertiaScroll={IsPerformingInertiaScroll}, isPointerCurrentlyDown={isPointerCurrentlyDown}");

        // If OnPointerUp started inertia, IsPerformingInertiaScroll will be true.
        // If OnPointerUp did NOT start inertia, it would have called ResetAllInternalStates(true).
        // isPointerCurrentlyDown should be false if OnPointerUp ran.
        // This function mostly serves as a final catch-all if OnPointerUp's logic path somehow
        // didn't fully reset and no inertia is active.
        if (!IsPerformingInertiaScroll && isPointerCurrentlyDown)
        {
            // This state (pointer still considered down by this script, but OnEndDrag called and no inertia) is unusual.
            // It implies OnPointerUp might not have fully executed its cleanup path.
            // Debug.LogWarning($"GIH OnEndDrag: Forcing state reset as no inertia is active and pointer was unexpectedly still considered down. Time: {Time.time:F2}");
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

    private IEnumerator InertiaScrollCoroutine(float initialAxisVelocityLocal, bool forHorizontal, int inertiaRow, int inertiaCol, bool scrollAlreadyHappenedThisInteraction)
    {
        IsPerformingInertiaScroll = true;
        bool inertiaItselfCausedScroll = false;
        // Debug.Log($"GIH Inertia: START. scrollAlreadyHappenedThisInteraction={scrollAlreadyHappenedThisInteraction}. Time: {Time.time:F2}");

        float currentAxisVelocityLocal = initialAxisVelocityLocal;
        float inertiaAccumulatedScrollDistance = 0f;

        if (forHorizontal && (inertiaRow < 0 || inertiaRow >= wordGridManager.gridSize))
        { Debug.LogError("GIH Inertia: Invalid targetRow. Aborting."); IsPerformingInertiaScroll = false; inertiaCoroutine = null; ResetAllInternalStates(true); yield break; }
        if (!forHorizontal && (inertiaCol < 0 || inertiaCol >= wordGridManager.gridSize))
        { Debug.LogError("GIH Inertia: Invalid targetCol. Aborting."); IsPerformingInertiaScroll = false; inertiaCoroutine = null; ResetAllInternalStates(true); yield break; }

        while (Mathf.Abs(currentAxisVelocityLocal) > minInertiaSpeed)
        {
            if (gameManager == null || gameManager.CurrentStatePublic != GameManager.GameState.Playing || wordGridManager == null)
            { IsPerformingInertiaScroll = false; inertiaCoroutine = null; ResetAllInternalStates(true); yield break; }

            currentAxisVelocityLocal *= inertiaDampingFactor;
            float moveAmountThisFrameLocal = currentAxisVelocityLocal * Time.deltaTime;
            inertiaAccumulatedScrollDistance += moveAmountThisFrameLocal;

            if (Mathf.Abs(inertiaAccumulatedScrollDistance) >= cellSizeWithSpacing * scrollThresholdFactor)
            {
                // Wait for WGM direct scroll animation to finish BEFORE requesting another scroll
                yield return new WaitUntil(() => !wordGridManager.isAnimating);
                if (gameManager == null || gameManager.CurrentStatePublic != GameManager.GameState.Playing) // Re-check after wait
                { IsPerformingInertiaScroll = false; inertiaCoroutine = null; ResetAllInternalStates(true); yield break; }

                int scrollDirection = (int)Mathf.Sign(inertiaAccumulatedScrollDistance);
                float scrollAmountAbs = Mathf.Abs(inertiaAccumulatedScrollDistance);

                if (forHorizontal) { wordGridManager.RequestRowScroll(inertiaRow, scrollDirection, scrollAmountAbs); }
                else { wordGridManager.RequestColumnScroll(inertiaCol, -scrollDirection, scrollAmountAbs); }

                if (!inertiaItselfCausedScroll) Debug.Log($"GIH Inertia: First scroll of this inertia sequence occurred. Time: {Time.time:F2}");
                inertiaItselfCausedScroll = true;

                inertiaAccumulatedScrollDistance -= scrollDirection * (cellSizeWithSpacing * scrollThresholdFactor);
                if (gameManager.CurrentGameDisplayMode == GameManager.DisplayMode.Moves)
                {
                    pendingMoveReduction = true;
                    moveReductionRow = forHorizontal ? inertiaRow : -1;
                    moveReductionCol = forHorizontal ? -1 : inertiaCol;
                }
            }
            if (Mathf.Abs(currentAxisVelocityLocal) <= minInertiaSpeed) break;
            yield return null;
        }

        bool needsValidationAfterInertia = scrollAlreadyHappenedThisInteraction || inertiaItselfCausedScroll;
        // Debug.Log($"GIH Inertia: FINISHED. inertiaItselfCausedScroll={inertiaItselfCausedScroll}, scrollAlreadyHappenedThisInteraction={scrollAlreadyHappenedThisInteraction}. Time: {Time.time:F2}");

        IsPerformingInertiaScroll = false;
        inertiaCoroutine = null;

        // Call ResetAllInternalStates first. It now uses 'fromPointerUpOrInertiaEnd=true'
        // and should NOT clear pendingValidationHighlightUpdate if IsPerformingInertiaScroll is false (which it is now).
        // However, the critical part is that pendingValidationHighlightUpdate is set *after* this reset.
        ResetAllInternalStates(true); // true because inertia (an interaction part) just ended

        if (needsValidationAfterInertia)
        {
            pendingValidationHighlightUpdate = true; // Set it AFTER reset, ensuring it persists for the next Update cycle
            Debug.Log($"GIH Inertia: END. Flagging validation. Total scroll this interaction: {needsValidationAfterInertia}. Time: {Time.time:F2}");
        }
        // If !needsValidationAfterInertia, pendingValidationHighlightUpdate remains false (as cleared by ResetAllInternalStates earlier in OnPointerDown).
    }

    private void StopInertiaCoroutine()
    {
        if (inertiaCoroutine != null)
        { StopCoroutine(inertiaCoroutine); inertiaCoroutine = null; }
        // This is called when a new touch interrupts or inertia ends naturally.
        // If interrupted, IsPerformingInertiaScroll should be set to false by the calling context
        // (e.g., OnPointerDown sets it false before calling ResetAllInternalStates(false)).
        // If inertia ends naturally, the coroutine itself sets IsPerformingInertiaScroll = false.
        IsPerformingInertiaScroll = false;
    }

    private void CalculateTargetRowColForDrag(Vector2 localPositionOnPanel)
    {
        Vector2Int gridCoords = CalculateGridCoordsFromLocalPos(localPositionOnPanel);
        targetRow = gridCoords.x; targetCol = gridCoords.y;
    }

    private Vector2Int CalculateGridCoordsFromLocalPos(Vector2 localPosition)
    {
        if (wordGridManager == null || cellSizeWithSpacing <= 0.001f) return new Vector2Int(-1, -1);
        float totalGridVisualWidth = wordGridManager.gridSize * wordGridManager.cellSize + Mathf.Max(0, wordGridManager.gridSize - 1) * wordGridManager.spacing;
        float gridContentStartX = -totalGridVisualWidth / 2f;
        float gridContentStartY = totalGridVisualWidth / 2f;
        float xInGridContent = localPosition.x - gridContentStartX;
        float yInGridContent = gridContentStartY - localPosition.y;
        int c = Mathf.FloorToInt(xInGridContent / cellSizeWithSpacing);
        int r = Mathf.FloorToInt(yInGridContent / cellSizeWithSpacing);
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
        else { dragHighlightedImages.Add(null); dragOriginalColors.Add(Color.clear); }
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