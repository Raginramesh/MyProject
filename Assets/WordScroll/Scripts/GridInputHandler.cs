using UnityEngine;
using UnityEngine.EventSystems; // Required for drag interfaces (IBeginDragHandler, etc.)
using UnityEngine.UI; // Required for Image component (for highlight color)
using System.Collections.Generic; // Required for List (for highlight tracking)
using System.Collections; // Required for Coroutines

public class GridInputHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("References")]
    [SerializeField] private WordGridManager wordGridManager; // Manages the grid data and animations
    [SerializeField] private GameManager gameManager; // Manages game state and animation flags
    [SerializeField] private RectTransform gridPanelRect; // The UI Panel RectTransform that receives drag events
    [SerializeField] private Camera uiCamera; // Camera for ScreenPointToLocalPointInRectangle (usually Main Camera for Screen Space - Camera canvas)

    [Header("Drag Settings")]
    [Tooltip("Minimum screen distance the pointer must move before a drag direction is locked.")]
    [SerializeField] private float dragThreshold = 20f;
    [Tooltip("Factor of (cell size + spacing) the pointer must move along the locked axis to trigger a direct scroll action during drag.")]
    [SerializeField] private float scrollThresholdFactor = 0.4f;

    [Header("Inertia Settings")]
    [Tooltip("Enable inertia scrolling after a flick.")]
    [SerializeField] private bool enableInertia = true;
    [Tooltip("Minimum release velocity (pixels/sec) to trigger inertia.")]
    [SerializeField] private float minFlickVelocity = 300f;
    [Tooltip("How quickly inertia slows down. Value is a factor applied each frame (e.g., 0.95 means 5% speed loss).")]
    [Range(0.8f, 0.99f)]
    [SerializeField] private float inertiaDampingFactor = 0.95f;
    [Tooltip("Minimum speed (pixels/sec) for inertia to continue.")]
    [SerializeField] private float minInertiaSpeed = 30f;
    [Tooltip("How many of the latest drag samples to use for velocity calculation.")]
    [SerializeField] private int velocityCalculationSamples = 5;


    [Header("Highlight Settings")]
    [Tooltip("If true, the dragged row/column will be visually highlighted.")]
    [SerializeField] private bool enableDragHighlight = true;
    [Tooltip("Scale multiplier applied to highlighted cells (e.g., 1.0 means no change, 1.1 is 10% bigger).")]
    [SerializeField] private float highlightScaleMultiplier = 1.08f;
    [Tooltip("Color tint applied to the Image component of highlighted cells.")]
    [SerializeField] private Color highlightColor = new Color(0.9f, 0.9f, 0.9f, 1f); // Light gray default

    // --- Drag State Variables ---
    private Vector2 dragStartPosition; // Where the drag started in local panel coordinates
    private bool isDragging = false; // Is a drag currently active?
    private bool axisLocked = false; // Has the drag direction (horizontal/vertical) been determined for this drag?
    private bool isHorizontalDrag = false; // Is the locked axis horizontal? (Only valid if axisLocked is true)
    private int targetRow = -1; // Grid row index under the drag start point (-1 if not on grid)
    private int targetCol = -1; // Grid column index under the drag start point (-1 if not on grid)
    private float accumulatedDrag = 0f; // Accumulated drag distance along the locked axis since the last scroll trigger
    private float cellSizeWithSpacing; // Cached value: wordGridManager.cellSize + wordGridManager.spacing

    // --- Velocity Tracking for Inertia ---
    private struct PointerSample
    {
        public Vector2 Position;
        public float Time;
        public PointerSample(Vector2 position, float time) { Position = position; Time = time; }
    }
    private List<PointerSample> pointerSamples = new List<PointerSample>();
    private Coroutine inertiaCoroutine = null;
    public bool IsPerformingInertiaScroll { get; private set; } = false; // Public getter for GameManager


    // --- State Flags for Delayed Actions (Processed in Update) ---\
    private bool scrollOccurredThisDrag = false; // Tracks if ANY scroll happened during the current drag session
    private bool pendingValidationCheck = false; // Flag: Trigger word validation when grid settles?
    private bool pendingMoveReduction = false;   // Flag: Apply move reduction when grid settles?
    private int moveReductionRow = -1;           // Target row for pending move reduction
    private int moveReductionCol = -1;           // Target column for pending move reduction

    // --- Highlight Tracking Variables ---\
    private List<CellController> currentlyHighlightedCells = new List<CellController>(); // Stores controllers for reset
    private List<Image> highlightedImages = new List<Image>(); // Stores Image components for color reset
    private List<Color> originalColors = new List<Color>();    // Stores original colors of images
    private Vector3 originalScale = Vector3.one;               // Original scale of cells (assumed uniform)
    private bool isHighlightApplied = false;                   // Is highlight currently active?


    // --- Initialization ---\
    void Awake()
    {
        IsPerformingInertiaScroll = false;
        // Attempt to find references if not assigned in Inspector
        if (wordGridManager == null) wordGridManager = FindFirstObjectByType<WordGridManager>();
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (gridPanelRect == null) gridPanelRect = GetComponent<RectTransform>(); // Assume script is on the panel if not assigned
        if (uiCamera == null) uiCamera = Camera.main; // Default to main camera

        // --- Validate Critical References ---\
        if (wordGridManager == null) { Debug.LogError("GridInputHandler: WordGridManager reference missing!", this); enabled = false; return; }
        if (gameManager == null) { Debug.LogError("GridInputHandler: GameManager reference missing!", this); enabled = false; return; }
        if (gridPanelRect == null) { Debug.LogError("GridInputHandler: Grid Panel Rect reference missing!", this); enabled = false; return; }
        if (uiCamera == null) { Debug.LogError("GridInputHandler: UI Camera reference missing!", this); enabled = false; return; }
    }

    void Start()
    {
        IsPerformingInertiaScroll = false;
        // Pre-calculate cell size + spacing (ensure WordGridManager is available)
        if (wordGridManager != null)
        {
            cellSizeWithSpacing = wordGridManager.cellSize + wordGridManager.spacing;
        }
        else
        {
            Debug.LogError("GridInputHandler: Cannot calculate cellSizeWithSpacing in Start - WordGridManager is null!", this);
            enabled = false;
            return;
        }

        UpdateOriginalScale();
        isHighlightApplied = false;
        ResetPendingFlags();
        scrollOccurredThisDrag = false;
    }

    void OnEnable()
    {
        ResetDragState();
        ResetPendingFlags();
        scrollOccurredThisDrag = false;
        IsPerformingInertiaScroll = false;
        StopInertiaCoroutine();

        if (isHighlightApplied) ForceResetHighlightVisuals();
        isHighlightApplied = false;
        ClearHighlightLists();
    }

    void OnDisable()
    {
        ResetDragState();
        ResetPendingFlags();
        scrollOccurredThisDrag = false;
        IsPerformingInertiaScroll = false;
        StopInertiaCoroutine();

        if (isHighlightApplied) ForceResetHighlightVisuals();
    }

    private void StopInertiaCoroutine()
    {
        if (inertiaCoroutine != null)
        {
            StopCoroutine(inertiaCoroutine);
            inertiaCoroutine = null;
        }
        IsPerformingInertiaScroll = false;
    }


    // --- Update Loop for Delayed Actions ---\
    void Update()
    {
        // Do not process pending actions if game isn't playing or ANY animations are running (grid, effects, OR INERTIA)
        if (gameManager == null || gameManager.CurrentStatePublic != GameManager.GameState.Playing || gameManager.IsAnyAnimationPlaying)
        {
            return;
        }

        // --- Process Pending Move Reduction ---
        if (pendingMoveReduction && wordGridManager != null && !wordGridManager.isAnimating)
        {
            pendingMoveReduction = false;
            wordGridManager.ApplyPendingMoveReduction(moveReductionRow, moveReductionCol);
            moveReductionRow = -1;
            moveReductionCol = -1;
        }

        // --- Process Pending Validation Check ---
        if (pendingValidationCheck && wordGridManager != null && !wordGridManager.isAnimating)
        {
            pendingValidationCheck = false;
            wordGridManager.TriggerValidationCheck();
        }
    }


    // --- Input Handling (Event System Interfaces) ---\

    public void OnBeginDrag(PointerEventData eventData)
    {
        // --- Pre-Drag Checks ---
        if (!enabled || wordGridManager == null || gameManager == null ||
            gameManager.CurrentStatePublic != GameManager.GameState.Playing || gameManager.IsAnyAnimationPlaying)
        {
            isDragging = false;
            return;
        }

        StopInertiaCoroutine(); // Stop any ongoing inertia if a new drag starts
        ResetPendingFlags();    // Also reset pending flags as inertia might have set one

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, eventData.position, uiCamera, out dragStartPosition))
        {
            isDragging = false; return;
        }

        isDragging = true;
        axisLocked = false;
        accumulatedDrag = 0f;
        CalculateTargetRowCol(dragStartPosition);

        ResetPendingFlags(); // Redundant? Already called above. Keeping for safety.
        scrollOccurredThisDrag = false;

        pointerSamples.Clear();
        AddPointerSample(eventData.position);

        UpdateOriginalScale();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || wordGridManager == null || gameManager == null)
        {
            if (!isDragging) ResetDragState();
            return;
        }

        if (gameManager.CurrentStatePublic != GameManager.GameState.Playing ||
            (wordGridManager.isAnimating && !IsPerformingInertiaScroll)) // Allow drag if inertia is causing wordGridManager animation
        {
            // If WordGridManager is animating due to a direct scroll (not inertia), wait for it.
            if (wordGridManager.isAnimating && !IsPerformingInertiaScroll) return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, eventData.position, uiCamera, out Vector2 currentLocalPos))
        {
            OnEndDrag(eventData); // Dragged outside panel
            return;
        }

        AddPointerSample(eventData.position); // Record pointer sample for velocity

        Vector2 dragVector = currentLocalPos - dragStartPosition;

        // --- Axis Locking Logic ---
        if (!axisLocked)
        {
            if (dragVector.magnitude > dragThreshold)
            {
                axisLocked = true;
                isHorizontalDrag = Mathf.Abs(dragVector.x) > Mathf.Abs(dragVector.y);
                if (enableDragHighlight)
                {
                    if (targetRow != -1 && targetCol != -1) // Ensure valid start target
                    {
                        if (isHorizontalDrag) ApplyHighlightRow(targetRow);
                        else ApplyHighlightColumn(targetCol);
                    }
                }
            }
            else { return; } // Not enough movement to lock axis yet
        }

        // --- Scroll Triggering Logic (Only if axis is locked and NOT performing inertia) ---
        if (axisLocked && !IsPerformingInertiaScroll) // Inertia handles its own scrolling
        {
            float dragAmountOnAxis = isHorizontalDrag ? dragVector.x : dragVector.y;
            accumulatedDrag += dragAmountOnAxis; // Accumulate signed drag specific to this frame's movement along the axis

            float scrollTriggerDistance = cellSizeWithSpacing * scrollThresholdFactor;
            int scrollDirection = 0;

            // Check if accumulated drag has passed the threshold for a scroll
            if (Mathf.Abs(accumulatedDrag) >= scrollTriggerDistance)
            {
                scrollDirection = (int)Mathf.Sign(accumulatedDrag);

                // Reduce accumulated drag by what was "used" for this scroll
                accumulatedDrag -= scrollDirection * scrollTriggerDistance;
            }

            if (scrollDirection != 0)
            {
                if (targetRow == -1 || targetCol == -1) { return; } // Ignore if start target invalid

                // Wait if grid is already animating from a previous quick scroll in the same drag
                if (wordGridManager.isAnimating)
                {
                    // Postpone or queue. For now, we might have slight animation overlap
                    // if user drags extremely fast over multiple scrollThresholds within WordGridManager's animation time.
                    // We can add accumulatedDrag back if we decide to wait until next frame.
                    // For now, allow the request.
                }
                else
                {
                    scrollOccurredThisDrag = true;
                    // ResetPendingFlags(); // This might be too aggressive, validation should occur after drag sequence or inertia

                    if (isHorizontalDrag)
                    {
                        wordGridManager.RequestRowScroll(targetRow, scrollDirection, 0f);
                    }
                    else
                    {
                        wordGridManager.RequestColumnScroll(targetCol, -scrollDirection, 0f);
                    }
                }
            }
            // Update dragStartPosition for the next OnDrag calculation to be relative to current pos
            dragStartPosition = currentLocalPos;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (enableDragHighlight && isHighlightApplied)
        {
            ResetHighlight();
        }

        if (!isDragging || wordGridManager == null)
        {
            ResetDragState();
            scrollOccurredThisDrag = false;
            pointerSamples.Clear();
            return;
        }

        bool inertiaWillStart = false;
        if (enableInertia && axisLocked) // Inertia needs a locked axis
        {
            AddPointerSample(eventData.position); // Add final sample
            Vector2 releaseVelocity = CalculateVelocity();
            float speedOnAxis = isHorizontalDrag ? Mathf.Abs(releaseVelocity.x) : Mathf.Abs(releaseVelocity.y);

            if (speedOnAxis > minFlickVelocity)
            {
                float relevantVelocityComponent = isHorizontalDrag ? releaseVelocity.x : releaseVelocity.y;
                inertiaCoroutine = StartCoroutine(InertiaScrollCoroutine(relevantVelocityComponent, isHorizontalDrag));
                inertiaWillStart = true;
            }
        }

        if (!inertiaWillStart)
        {
            // Original Pending Flags Logic (if no inertia starts)
            if (scrollOccurredThisDrag && axisLocked) // Ensure scroll happened and axis was determined
            {
                pendingMoveReduction = true;
                if (isHorizontalDrag) { moveReductionRow = targetRow; moveReductionCol = -1; }
                else { moveReductionRow = -1; moveReductionCol = targetCol; }
            }
            // Always set validation check if no inertia, or after inertia finishes (handled in coroutine)
            pendingValidationCheck = true;
        }

        ResetDragState();
        scrollOccurredThisDrag = false;
        pointerSamples.Clear();
    }

    private void AddPointerSample(Vector2 screenPosition)
    {
        // Convert screen position to local panel position for consistent velocity calculation
        RectTransformUtility.ScreenPointToLocalPointInRectangle(gridPanelRect, screenPosition, uiCamera, out Vector2 localPos);
        pointerSamples.Add(new PointerSample(localPos, Time.unscaledTime));
        while (pointerSamples.Count > velocityCalculationSamples && velocityCalculationSamples > 0)
        {
            pointerSamples.RemoveAt(0);
        }
    }

    private Vector2 CalculateVelocity()
    {
        if (pointerSamples.Count < 2) return Vector2.zero;

        PointerSample firstSample = pointerSamples[0];
        PointerSample lastSample = pointerSamples[pointerSamples.Count - 1];

        float timeDelta = lastSample.Time - firstSample.Time;
        if (timeDelta <= 0.001f) return Vector2.zero;

        Vector2 positionDelta = lastSample.Position - firstSample.Position; // Using local positions
        return positionDelta / timeDelta; // local units per second
    }


    private IEnumerator InertiaScrollCoroutine(float initialAxisVelocity, bool forHorizontal)
    {
        IsPerformingInertiaScroll = true;
        pendingValidationCheck = false; // Validation will be set at the end of inertia

        float currentAxisVelocity = initialAxisVelocity;
        float inertiaAccumulatedDistance = 0f;

        // Target row/col for inertia is the one active when drag ended
        int inertiaTargetRow = targetRow;
        int inertiaTargetCol = targetCol;


        while (Mathf.Abs(currentAxisVelocity) > minInertiaSpeed)
        {
            // Damp the velocity
            currentAxisVelocity *= inertiaDampingFactor;

            float moveAmountThisFrame = currentAxisVelocity * Time.deltaTime;
            inertiaAccumulatedDistance += moveAmountThisFrame;

            float distancePerCellScroll = cellSizeWithSpacing;

            if (Mathf.Abs(inertiaAccumulatedDistance) >= distancePerCellScroll)
            {
                int scrollDirection = (int)Mathf.Sign(inertiaAccumulatedDistance);

                // Wait for WordGridManager to be free AND game to be playing
                yield return new WaitUntil(() => wordGridManager != null && !wordGridManager.isAnimating &&
                                               gameManager != null && gameManager.CurrentStatePublic == GameManager.GameState.Playing);

                if (wordGridManager == null || gameManager == null || gameManager.CurrentStatePublic != GameManager.GameState.Playing)
                {
                    break; // Game state changed or refs lost, abort inertia
                }

                if (forHorizontal)
                {
                    if (inertiaTargetRow != -1)
                        wordGridManager.RequestRowScroll(inertiaTargetRow, scrollDirection, 0f);
                }
                else
                {
                    if (inertiaTargetCol != -1)
                        wordGridManager.RequestColumnScroll(inertiaTargetCol, -scrollDirection, 0f);
                }

                inertiaAccumulatedDistance -= scrollDirection * distancePerCellScroll;

                if (gameManager.CurrentGameDisplayMode == GameManager.DisplayMode.Moves)
                {
                    pendingMoveReduction = true;
                    if (forHorizontal) { moveReductionRow = inertiaTargetRow; moveReductionCol = -1; }
                    else { moveReductionRow = -1; moveReductionCol = inertiaTargetCol; }
                }
            }

            if (Mathf.Abs(currentAxisVelocity) <= minInertiaSpeed) break;

            yield return null;
        }

        IsPerformingInertiaScroll = false;
        inertiaCoroutine = null;
        pendingValidationCheck = true; // Now that all inertia movement is done, flag for validation.
    }

    void CalculateTargetRowCol(Vector2 localPosition)
    {
        targetRow = -1;
        targetCol = -1;

        if (wordGridManager == null || cellSizeWithSpacing <= 0) return;

        float totalGridSizeUI = wordGridManager.gridSize * wordGridManager.cellSize + (wordGridManager.gridSize - 1) * wordGridManager.spacing;
        float gridOriginOffsetX = -totalGridSizeUI / 2f;
        float gridOriginOffsetY = -totalGridSizeUI / 2f;

        float relativeX = localPosition.x - gridOriginOffsetX;
        float relativeY = localPosition.y - gridOriginOffsetY;

        int calculatedCol = Mathf.FloorToInt(relativeX / cellSizeWithSpacing);
        int calculatedRowBasedOnBottom = Mathf.FloorToInt(relativeY / cellSizeWithSpacing);

        if (calculatedCol < 0 || calculatedCol >= wordGridManager.gridSize || calculatedRowBasedOnBottom < 0 || calculatedRowBasedOnBottom >= wordGridManager.gridSize)
        {
            return;
        }

        targetCol = calculatedCol;
        targetRow = wordGridManager.gridSize - 1 - calculatedRowBasedOnBottom;

        targetCol = Mathf.Clamp(targetCol, 0, wordGridManager.gridSize - 1);
        targetRow = Mathf.Clamp(targetRow, 0, wordGridManager.gridSize - 1);
    }

    private void ResetPendingFlags()
    {
        pendingValidationCheck = false;
        pendingMoveReduction = false;
        moveReductionRow = -1;
        moveReductionCol = -1;
    }

    private void ResetDragState()
    {
        isDragging = false;
        axisLocked = false;
        accumulatedDrag = 0f;
        // targetRow and targetCol are determined at the start of a drag
    }

    private void UpdateOriginalScale()
    {
        if (wordGridManager != null && wordGridManager.gridSize > 0)
        {
            CellController sampleCell = wordGridManager.GetCellController(new Vector2Int(0, 0));
            if (sampleCell != null && sampleCell.RectTransform != null)
            {
                originalScale = sampleCell.RectTransform.localScale;
            }
            else { originalScale = Vector3.one; }
        }
        else { originalScale = Vector3.one; }
    }

    // --- Highlight Methods ---
    private void ApplyHighlightRow(int rowIndex)
    {
        if (!enableDragHighlight || isHighlightApplied || wordGridManager == null || rowIndex < 0 || rowIndex >= wordGridManager.gridSize) return;

        ClearHighlightLists();

        for (int c = 0; c < wordGridManager.gridSize; c++)
        {
            CellController cellController = wordGridManager.GetCellController(new Vector2Int(rowIndex, c));
            if (cellController == null || cellController.RectTransform == null) continue;

            RectTransform cellRect = cellController.RectTransform;
            cellRect.localScale = originalScale * highlightScaleMultiplier;
            currentlyHighlightedCells.Add(cellController);

            Image cellImage = cellController.GetComponent<Image>() ?? cellController.GetComponentInChildren<Image>();
            if (cellImage != null)
            {
                highlightedImages.Add(cellImage);
                originalColors.Add(cellImage.color);
                cellImage.color = highlightColor;
            }
            else { highlightedImages.Add(null); originalColors.Add(Color.clear); }
        }
        isHighlightApplied = true;
    }

    private void ApplyHighlightColumn(int colIndex)
    {
        if (!enableDragHighlight || isHighlightApplied || wordGridManager == null || colIndex < 0 || colIndex >= wordGridManager.gridSize) return;

        ClearHighlightLists();

        for (int r = 0; r < wordGridManager.gridSize; r++)
        {
            CellController cellController = wordGridManager.GetCellController(new Vector2Int(r, colIndex));
            if (cellController == null || cellController.RectTransform == null) continue;

            RectTransform cellRect = cellController.RectTransform;
            cellRect.localScale = originalScale * highlightScaleMultiplier;
            currentlyHighlightedCells.Add(cellController);

            Image cellImage = cellController.GetComponent<Image>() ?? cellController.GetComponentInChildren<Image>();
            if (cellImage != null)
            {
                highlightedImages.Add(cellImage);
                originalColors.Add(cellImage.color);
                cellImage.color = highlightColor;
            }
            else { highlightedImages.Add(null); originalColors.Add(Color.clear); }
        }
        isHighlightApplied = true;
    }

    private void ResetHighlight()
    {
        ForceResetHighlightVisuals();
    }

    private void ForceResetHighlightVisuals()
    {
        if (!isHighlightApplied) return;

        for (int i = 0; i < currentlyHighlightedCells.Count; i++)
        {
            CellController cellController = currentlyHighlightedCells[i];
            if (cellController != null && cellController.RectTransform != null)
            {
                cellController.RectTransform.localScale = originalScale;

                if (i < highlightedImages.Count && highlightedImages[i] != null && i < originalColors.Count)
                {
                    highlightedImages[i].color = originalColors[i];
                }
            }
        }
        isHighlightApplied = false;
        ClearHighlightLists();
    }

    private void ClearHighlightLists()
    {
        currentlyHighlightedCells.Clear();
        highlightedImages.Clear();
        originalColors.Clear();
    }

}