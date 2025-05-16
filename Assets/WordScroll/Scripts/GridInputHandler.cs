using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using DG.Tweening;

public class GridInputHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private WordGridManager wordGridManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private RectTransform gridPanelRect;
    [SerializeField] private Camera uiCamera;

    [Header("Drag Settings")]
    [SerializeField] private float dragThreshold = 20f; // Screen pixels
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
    [SerializeField] private float maxTapDuration = 0.3f; // Max time for a tap
    [SerializeField] private float maxTapMoveDistance = 15f; // Max screen pixels pointer can move from pressPosition for a tap

    // Drag State
    private Vector2 dragStartPosition; // Local panel coordinates where drag gesture initiated significant movement
    private Vector2 pointerDownInitialPosition; // Screen coordinates where pointer first went down
    private float pointerDownTime;          // Time of initial pointer down

    private bool isDragging = false; // True if current gesture is confirmed as a drag
    private bool axisLocked = false;
    private bool isHorizontalDrag = false;
    private int targetRow = -1; // Grid row for drag
    private int targetCol = -1; // Grid col for drag
    private float accumulatedDragDistanceOnAxis = 0f; // How much has been dragged along the locked axis for current scroll trigger
    private float cellSizeWithSpacing;

    // Velocity & Inertia
    private struct PointerSample { public Vector2 Position; public float Time; public PointerSample(Vector2 p, float t) { Position = p; Time = t; } }
    private List<PointerSample> pointerSamples = new List<PointerSample>();
    private Coroutine inertiaCoroutine = null;
    public bool IsPerformingInertiaScroll { get; private set; } = false;

    // Pending Actions
    private bool scrollOccurredThisConfirmedDrag = false; // True if a scroll was triggered by a confirmed drag
    private bool pendingValidationHighlightUpdate = false;
    private bool pendingMoveReduction = false;
    private int moveReductionRow = -1;
    private int moveReductionCol = -1;

    // Drag Highlight Visuals
    private List<CellController> currentlyDragHighlightedCells = new List<CellController>();
    private List<Image> dragHighlightedImages = new List<Image>();
    private List<Color> dragOriginalColors = new List<Color>();
    private Vector3 originalCellScale = Vector3.one;
    private bool isDragHighlightApplied = false;

    void Awake()
    {
        IsPerformingInertiaScroll = false;
        if (wordGridManager == null) wordGridManager = FindFirstObjectByType<WordGridManager>();
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (gridPanelRect == null) gridPanelRect = GetComponent<RectTransform>();
        if (uiCamera == null) uiCamera = Camera.main;

        if (wordGridManager == null || gameManager == null || gridPanelRect == null || uiCamera == null)
        { Debug.LogError("GridInputHandler: Critical reference missing!", this); enabled = false; return; }
    }

    void Start()
    {
        if (wordGridManager != null) cellSizeWithSpacing = wordGridManager.cellSize + wordGridManager.spacing;
        else { Debug.LogError("GIH: WordGridManager null in Start!", this); enabled = false; return; }
        UpdateOriginalCellScale();
        ResetAllInternalStates(); // Initialize all states
    }

    void OnEnable() { ResetAllInternalStates(); }
    void OnDisable() { ResetAllInternalStates(); if (isDragHighlightApplied) ForceResetDragHighlightVisuals(); }

    private void ResetAllInternalStates()
    {
        isDragging = false; // Critical: ensure isDragging is false initially
        axisLocked = false;
        accumulatedDragDistanceOnAxis = 0f;
        scrollOccurredThisConfirmedDrag = false;

        pendingValidationHighlightUpdate = false;
        pendingMoveReduction = false;
        moveReductionRow = -1;
        moveReductionCol = -1;

        IsPerformingInertiaScroll = false;
        StopInertiaCoroutine();

        if (isDragHighlightApplied) ForceResetDragHighlightVisuals(); // Also clears lists
        isDragHighlightApplied = false; // Ensure this is also reset
        // ClearDragHighlightLists(); // Called by ForceResetDragHighlightVisuals

        pointerSamples.Clear();
        // targetRow/Col will be set on new interaction.
    }

    void Update()
    {
        if (gameManager == null || gameManager.CurrentStatePublic != GameManager.GameState.Playing || gameManager.IsAnyAnimationPlaying)
        {
            return;
        }

        if (pendingMoveReduction && wordGridManager != null && !wordGridManager.isAnimating)
        {
            pendingMoveReduction = false;
            wordGridManager.ApplyPendingMoveReduction(moveReductionRow, moveReductionCol);
            moveReductionRow = -1; moveReductionCol = -1;
        }

        if (pendingValidationHighlightUpdate && wordGridManager != null && !wordGridManager.isAnimating)
        {
            pendingValidationHighlightUpdate = false;
            wordGridManager.TriggerValidationCheckAndHighlightUpdate();
        }
    }

    public void OnPointerDown(PointerEventData eventData) // Using OnPointerDown to record initial tap info
    {
        // This is not an official interface method for GridInputHandler, but called by EventTrigger if set up
        // For IPointerClickHandler to work, this isn't strictly needed by GridInputHandler itself
        // but useful if we wanted to manage tap states more manually.
        // For now, OnBeginDrag will capture pointerDownInitialPosition and pointerDownTime.
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Debug.Log($"GIH: OnPointerClick triggered. Time: {Time.unscaledTime}");

        // Check 1: Was this click part of a drag operation that was confirmed?
        // If isDragging is true here, it means OnEndDrag hasn't reset it yet, which is unlikely
        // but as a safeguard. More importantly, OnEndDrag now has logic to NOT set pending flags
        // if it wasn't a confirmed drag.
        if (isDragging)
        {
             Debug.Log("GIH: OnPointerClick ignored because isDragging is true (unexpected state).");
            return; // Should have been reset by OnEndDrag if drag completed.
        }

        // Check 2: Time and distance from original pointer down position.
        // eventData.pressPosition is the screen position where the press began.
        float duration = Time.unscaledTime - pointerDownTime;
        float distance = Vector2.Distance(pointerDownInitialPosition, eventData.position); // eventData.position is current up position

        // Debug.Log($"GIH: Click check - Duration: {duration} (Max: {maxTapDuration}), Distance: {distance} (Max: {maxTapMoveDistance})");

        if (duration > maxTapDuration)
        {
             Debug.Log("GIH: OnPointerClick ignored, tap duration too long.");
            return;
        }
        if (distance > maxTapMoveDistance)
        {
             Debug.Log("GIH: OnPointerClick ignored, pointer moved too far for a tap.");
            return;
        }

        if (!enabled || wordGridManager == null || gameManager == null ||
            gameManager.CurrentStatePublic != GameManager.GameState.Playing || gameManager.IsAnyAnimationPlaying)
        {
            // Debug.Log("GIH: OnPointerClick ignored due to game state or animation.");
            return;
        }

        Vector2 localClickPos;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, eventData.position, uiCamera, out localClickPos))
        {
            Vector2Int tappedGridCoord = CalculateGridCoordsFromLocalPos(localClickPos);
            if (tappedGridCoord.x != -1 && tappedGridCoord.y != -1)
            {
                // Debug.Log($"GIH: Tap validated at grid coordinate: {tappedGridCoord}");
                gameManager.AttemptTapValidation(tappedGridCoord);
            }
            // else { Debug.Log("GIH: Tap was outside valid grid cell area during OnPointerClick."); }
        }
        // else { Debug.Log("GIH: Tap (Click) could not be converted to local panel point."); }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Debug.Log($"GIH: OnBeginDrag. Time: {Time.unscaledTime}");
        // Record initial pointer down info for potential tap validation by OnPointerClick
        pointerDownInitialPosition = eventData.pressPosition; // eventData.pressPosition is the initial down
        pointerDownTime = Time.unscaledTime; // Record time of actual pointer down

        // Reset drag-specific states, but isDragging is NOT set true yet.
        isDragging = false;
        axisLocked = false;
        accumulatedDragDistanceOnAxis = 0f;
        scrollOccurredThisConfirmedDrag = false;

        if (!enabled || wordGridManager == null || gameManager == null ||
            gameManager.CurrentStatePublic != GameManager.GameState.Playing ||
            (gameManager.IsAnyAnimationPlaying && !IsPerformingInertiaScroll)) // Allow starting a drag if only inertia is playing
        {
            return; // Do not proceed with drag initialization
        }

        StopInertiaCoroutine(); // Stop any ongoing inertia
        // ResetPendingFlags(); // Reset flags for move reduction/validation, as a new interaction starts

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, eventData.position, uiCamera, out dragStartPosition))
        {
            return; // Cannot convert to local point, bail.
        }

        CalculateTargetRowColForDrag(dragStartPosition); // Determine which row/col drag *might* affect

        pointerSamples.Clear();
        AddPointerSample(eventData.position); // Add first sample for velocity
        UpdateOriginalCellScale();
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Debug.Log("GIH: OnDrag");
        if (gameManager == null || gameManager.CurrentStatePublic != GameManager.GameState.Playing)
        {
            if (isDragging) OnEndDrag(eventData); // If it was dragging and game state changed, treat as end.
            isDragging = false; // Ensure it's reset
            return;
        }
        if (wordGridManager == null) { isDragging = false; return; }


        Vector2 currentLocalPos;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, eventData.position, uiCamera, out currentLocalPos))
        {
            if (isDragging) OnEndDrag(eventData); // Dragged outside panel
            return;
        }

        AddPointerSample(eventData.position);
        Vector2 dragVectorFromInitialStart = currentLocalPos - dragStartPosition; // Vector from where drag *gesture* started processing in OnBeginDrag

        if (!isDragging && !axisLocked) // If not yet confirmed as a drag
        {
            // Check against pointerDownInitialPosition (screen space) for drag threshold
            if (Vector2.Distance(pointerDownInitialPosition, eventData.position) > dragThreshold)
            {
                isDragging = true; // Now it's officially a drag
                axisLocked = true;
                isHorizontalDrag = Mathf.Abs(dragVectorFromInitialStart.x) > Mathf.Abs(dragVectorFromInitialStart.y);

                // Update dragStartPosition to currentLocalPos because dragVectorFromInitialStart was just for threshold.
                // From now on, deltas are relative to the last frame's position.
                dragStartPosition = currentLocalPos;

                if (enableDragHighlight && targetRow != -1 && targetCol != -1)
                {
                    if (isHorizontalDrag) ApplyDragHighlightRow(targetRow);
                    else ApplyDragHighlightColumn(targetCol);
                }
            }
            else
            {
                // Not enough movement to confirm drag. It might still be a tap.
                return;
            }
        }

        // If it's not a confirmed drag, don't process scroll logic
        if (!isDragging) return;

        // If grid is animating from a direct scroll (not inertia), hold off on new scrolls
        if (wordGridManager.isAnimating && !IsPerformingInertiaScroll)
        {
            dragStartPosition = currentLocalPos; // Keep dragStartPosition updated
            return;
        }

        // At this point, isDragging is true and axisLocked is true.
        // Calculate delta for this frame for scrolling purposes.
        float frameDragDeltaOnAxis = isHorizontalDrag ? (currentLocalPos.x - dragStartPosition.x) : (currentLocalPos.y - dragStartPosition.y);
        accumulatedDragDistanceOnAxis += frameDragDeltaOnAxis;

        float scrollTriggerDistance = cellSizeWithSpacing * scrollThresholdFactor;

        if (Mathf.Abs(accumulatedDragDistanceOnAxis) >= scrollTriggerDistance)
        {
            int scrollDirection = (int)Mathf.Sign(accumulatedDragDistanceOnAxis);

            if (targetRow != -1 || targetCol != -1) // Ensure valid target line
            {
                if (!wordGridManager.isAnimating) // Only trigger if grid isn't already scrolling
                {
                    scrollOccurredThisConfirmedDrag = true;
                    if (isHorizontalDrag && targetRow != -1)
                        wordGridManager.RequestRowScroll(targetRow, scrollDirection, 0f);
                    else if (!isHorizontalDrag && targetCol != -1)
                        wordGridManager.RequestColumnScroll(targetCol, -scrollDirection, 0f); // Y-axis often inverted for scroll direction

                    accumulatedDragDistanceOnAxis -= scrollDirection * scrollTriggerDistance; // Consume the amount used for scroll
                }
            }
        }
        dragStartPosition = currentLocalPos; // Update for next frame's delta
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Debug.Log("GIH: OnEndDrag");
        if (enableDragHighlight && isDragHighlightApplied)
        {
            ResetDragHighlight(); // Resets isDragHighlightApplied and clears lists
        }

        if (!isDragging) // If it was never confirmed as a drag (movement didn't exceed threshold in OnDrag)
        {
            // This path means OnDrag never set isDragging = true.
            // OnPointerClick should handle if it was a tap.
            // Minimal cleanup needed, primarily for pointerSamples if any were added.
            pointerSamples.Clear();
            // Reset other states just in case, though they shouldn't have been set if !isDragging
            axisLocked = false;
            accumulatedDragDistanceOnAxis = 0f;
            scrollOccurredThisConfirmedDrag = false;
            return;
        }

        // --- From here, isDragging IS true, meaning it was a confirmed drag ---
        bool inertiaWillStart = false;
        if (enableInertia && axisLocked) // axisLocked should be true if isDragging is true
        {
            AddPointerSample(eventData.position); // Add final sample for velocity
            Vector2 releaseVelocity = CalculateVelocity();
            float speedOnAxis = isHorizontalDrag ? Mathf.Abs(releaseVelocity.x) : Mathf.Abs(releaseVelocity.y);

            if (speedOnAxis > minFlickVelocity)
            {
                float relevantVelocityComponent = isHorizontalDrag ? releaseVelocity.x : releaseVelocity.y;
                // Use targetRow/Col determined at the start of the confirmed drag
                inertiaCoroutine = StartCoroutine(InertiaScrollCoroutine(relevantVelocityComponent, isHorizontalDrag, targetRow, targetCol));
                inertiaWillStart = true;
            }
        }

        if (!inertiaWillStart) // If no inertia, or inertia disabled/failed
        {
            if (scrollOccurredThisConfirmedDrag) // Only if a scroll actually happened during this drag
            {
                pendingMoveReduction = true;
                if (isHorizontalDrag) { moveReductionRow = targetRow; moveReductionCol = -1; }
                else { moveReductionRow = -1; moveReductionCol = targetCol; }
            }
            // Regardless of scroll, if a drag ended (and no inertia), trigger validation update.
            pendingValidationHighlightUpdate = true;
        }

        // Reset states for the next interaction
        isDragging = false; // CRITICAL: Reset isDragging now that drag operation is over
        axisLocked = false;
        accumulatedDragDistanceOnAxis = 0f;
        scrollOccurredThisConfirmedDrag = false;
        pointerSamples.Clear();
    }


    private void AddPointerSample(Vector2 screenPosition)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, screenPosition, uiCamera, out Vector2 localPos);
        pointerSamples.Add(new PointerSample(localPos, Time.unscaledTime));
        while (pointerSamples.Count > velocityCalculationSamples && velocityCalculationSamples > 0) pointerSamples.RemoveAt(0);
    }

    private Vector2 CalculateVelocity()
    {
        if (pointerSamples.Count < 2) return Vector2.zero;
        PointerSample first = pointerSamples[0];
        PointerSample last = pointerSamples[pointerSamples.Count - 1];
        float timeDelta = last.Time - first.Time;
        if (timeDelta <= 0.001f) return Vector2.zero; // Avoid division by zero or tiny numbers
        return (last.Position - first.Position) / timeDelta;
    }

    private IEnumerator InertiaScrollCoroutine(float initialAxisVelocity, bool forHorizontal, int inertiaRow, int inertiaCol)
    {
        IsPerformingInertiaScroll = true;
        // pendingValidationHighlightUpdate = false; // Validation update will be set at the very end of inertia

        float currentAxisVelocity = initialAxisVelocity;
        float inertiaAccumulatedScrollDistance = 0f; // Renamed for clarity vs. drag's accumulatedDistance

        while (Mathf.Abs(currentAxisVelocity) > minInertiaSpeed)
        {
            // Ensure game is still in a state to allow scrolling
            if (gameManager == null || gameManager.CurrentStatePublic != GameManager.GameState.Playing || wordGridManager == null)
            {
                IsPerformingInertiaScroll = false; inertiaCoroutine = null; pendingValidationHighlightUpdate = true; yield break;
            }

            currentAxisVelocity *= inertiaDampingFactor;
            float moveAmountThisFrame = currentAxisVelocity * Time.deltaTime; // Delta distance for this frame
            inertiaAccumulatedScrollDistance += moveAmountThisFrame;

            float distancePerCellScroll = cellSizeWithSpacing;

            if (Mathf.Abs(inertiaAccumulatedScrollDistance) >= distancePerCellScroll)
            {
                int scrollDirection = (int)Mathf.Sign(inertiaAccumulatedScrollDistance);

                // Wait for WordGridManager to be free (not animating from a previous inertia step)
                yield return new WaitUntil(() => !wordGridManager.isAnimating);

                // Double check state after wait
                if (gameManager == null || gameManager.CurrentStatePublic != GameManager.GameState.Playing || wordGridManager == null)
                {
                    IsPerformingInertiaScroll = false; inertiaCoroutine = null; pendingValidationHighlightUpdate = true; yield break;
                }

                if (forHorizontal && inertiaRow != -1)
                    wordGridManager.RequestRowScroll(inertiaRow, scrollDirection, 0f);
                else if (!forHorizontal && inertiaCol != -1)
                    wordGridManager.RequestColumnScroll(inertiaCol, -scrollDirection, 0f); // Y-axis often inverted

                inertiaAccumulatedScrollDistance -= scrollDirection * distancePerCellScroll; // Consume scrolled amount

                if (gameManager.CurrentGameDisplayMode == GameManager.DisplayMode.Moves)
                {
                    pendingMoveReduction = true; // This will be processed by Update() when animations settle
                    if (forHorizontal) { moveReductionRow = inertiaRow; moveReductionCol = -1; }
                    else { moveReductionRow = -1; moveReductionCol = inertiaCol; }
                }
            }

            if (Mathf.Abs(currentAxisVelocity) <= minInertiaSpeed) break; // End if speed drops too low
            yield return null; // Wait for next frame
        }

        IsPerformingInertiaScroll = false;
        inertiaCoroutine = null;
        pendingValidationHighlightUpdate = true; // All inertia movement done, flag for validation/highlight update.
    }

    private void CalculateTargetRowColForDrag(Vector2 localPositionOnPanel)
    {
        Vector2Int gridCoords = CalculateGridCoordsFromLocalPos(localPositionOnPanel);
        targetRow = gridCoords.x; // Will be -1 if outside
        targetCol = gridCoords.y; // Will be -1 if outside
    }

    private Vector2Int CalculateGridCoordsFromLocalPos(Vector2 localPosition)
    {
        if (wordGridManager == null || cellSizeWithSpacing <= 0.001f) return new Vector2Int(-1, -1);

        float totalGridWidthUI = wordGridManager.gridSize * wordGridManager.cellSize + (wordGridManager.gridSize - 1) * wordGridManager.spacing;
        // Assuming gridPanelRect's pivot is center (0.5, 0.5), local (0,0) is its center.
        // So, top-left of the grid content area within the panel is at:
        // (-totalGridWidthUI / 2, totalGridWidthUI / 2) in local panel coordinates if Y is upwards.
        float gridContentStartX = -totalGridWidthUI / 2f;
        float gridContentStartY = totalGridWidthUI / 2f; // Y positive is up in local RectTransform space

        // Adjust localPosition to be relative to the grid's content top-left corner
        float xInGridContent = localPosition.x - gridContentStartX;
        float yInGridContent = gridContentStartY - localPosition.y; // Y is downwards for grid row indexing

        int c = Mathf.FloorToInt(xInGridContent / cellSizeWithSpacing);
        int r = Mathf.FloorToInt(yInGridContent / cellSizeWithSpacing);

        if (c < 0 || c >= wordGridManager.gridSize || r < 0 || r >= wordGridManager.gridSize)
        {
            return new Vector2Int(-1, -1); // Outside grid
        }
        return new Vector2Int(r, c);
    }

    private void ResetPendingFlags() // Helper to reset pending action flags
    {
        pendingValidationHighlightUpdate = false;
        pendingMoveReduction = false;
        moveReductionRow = -1;
        moveReductionCol = -1;
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

    private void StopInertiaCoroutine()
    {
        if (inertiaCoroutine != null) { StopCoroutine(inertiaCoroutine); inertiaCoroutine = null; }
        // IsPerformingInertiaScroll is set to false within the coroutine's end or if aborted.
    }

    // --- Drag Highlight Methods (Scaling and Tinting) ---
    private void ApplyDragHighlightRow(int rowIndex)
    {
        if (!enableDragHighlight || isDragHighlightApplied || wordGridManager == null || rowIndex < 0 || rowIndex >= wordGridManager.gridSize) return;
        ClearDragHighlightLists(); // Prepare for new highlight
        for (int c = 0; c < wordGridManager.gridSize; c++)
        {
            CellController cc = wordGridManager.GetCellController(new Vector2Int(rowIndex, c));
            if (cc == null || cc.RectTransform == null) continue;

            cc.RectTransform.DOKill(); // Kill previous scale tweens
            cc.RectTransform.localScale = originalCellScale * highlightScaleMultiplier;
            currentlyDragHighlightedCells.Add(cc);

            Image img = cc.GetComponent<Image>() ?? cc.GetComponentInChildren<Image>();
            if (img != null)
            {
                img.DOKill(); // Kill previous color tweens
                dragHighlightedImages.Add(img);
                dragOriginalColors.Add(img.color);
                img.color = dragHighlightColor;
            }
            else { dragHighlightedImages.Add(null); dragOriginalColors.Add(Color.clear); } // Placeholder if no image
        }
        isDragHighlightApplied = true;
    }
    private void ApplyDragHighlightColumn(int colIndex)
    {
        if (!enableDragHighlight || isDragHighlightApplied || wordGridManager == null || colIndex < 0 || colIndex >= wordGridManager.gridSize) return;
        ClearDragHighlightLists();
        for (int r = 0; r < wordGridManager.gridSize; r++)
        {
            CellController cc = wordGridManager.GetCellController(new Vector2Int(r, colIndex));
            if (cc == null || cc.RectTransform == null) continue;

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
        isDragHighlightApplied = true;
    }
    private void ResetDragHighlight() { ForceResetDragHighlightVisuals(); }
    private void ForceResetDragHighlightVisuals()
    {
        if (!isDragHighlightApplied && currentlyDragHighlightedCells.Count == 0) return; // Nothing to reset or already reset

        for (int i = 0; i < currentlyDragHighlightedCells.Count; i++)
        {
            CellController cc = currentlyDragHighlightedCells[i];
            if (cc != null && cc.RectTransform != null)
            {
                cc.RectTransform.DOKill();
                cc.RectTransform.localScale = originalCellScale; // Reset scale

                if (i < dragHighlightedImages.Count && dragHighlightedImages[i] != null && i < dragOriginalColors.Count)
                {
                    dragHighlightedImages[i].DOKill();
                    dragHighlightedImages[i].color = dragOriginalColors[i]; // Reset color
                }
            }
        }
        isDragHighlightApplied = false; // Mark as no longer applied
        ClearDragHighlightLists(); // Clear lists for next use
    }
    private void ClearDragHighlightLists()
    {
        currentlyDragHighlightedCells.Clear();
        dragHighlightedImages.Clear();
        dragOriginalColors.Clear();
    }
}
