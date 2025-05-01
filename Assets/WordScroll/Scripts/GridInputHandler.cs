using UnityEngine;
using UnityEngine.EventSystems; // Required for drag interfaces
using UnityEngine.UI; // <<< Required for Image component
using System.Collections.Generic; // <<< Required for List

public class GridInputHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("References")]
    [SerializeField] private WordGridManager wordGridManager; // Reference to the grid manager
    [SerializeField] private RectTransform gridPanelRect; // The UI Panel RectTransform that receives drag events (Previously gridRectTransform)

    [Header("Drag Settings")] // <<< RENAMED from "Settings"
    [SerializeField] private float dragThreshold = 20f; // Minimum distance drag before locking axis
    [SerializeField] private float scrollThresholdFactor = 0.4f; // Factor of cell size+spacing to trigger a scroll

    [Header("Highlight Settings")] // <<< NEW Section
    [Tooltip("Whether to apply visual highlight on drag")]
    [SerializeField] private bool enableDragHighlight = true;
    [Tooltip("Percentage to increase cell size (e.g., 1.08 for 8%)")]
    [SerializeField] private float highlightScaleMultiplier = 1.08f;
    [Tooltip("Color to tint the cell background during drag")]
    [SerializeField] private Color highlightColor = new Color(0.9f, 0.9f, 0.9f, 1f); // Light gray default

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

    // <<< NEW: Highlight tracking >>>
    private List<RectTransform> currentlyHighlightedCells = new List<RectTransform>();
    private List<Image> highlightedImages = new List<Image>(); // Store Image components for color reset
    private List<Color> originalColors = new List<Color>();
    private Vector3 originalScale = Vector3.one;
    private bool isHighlightApplied = false;


    void Start()
    {
        // Validate references
        if (wordGridManager == null) { Debug.LogError("GridInputHandler: WordGridManager reference missing!", this); enabled = false; return; }
        // Use gridPanelRect name from the user's provided base script
        if (gridPanelRect == null) { Debug.LogError("GridInputHandler: Grid Panel Rect reference missing!", this); enabled = false; return; }

        // Pre-calculate cell size + spacing for efficiency
        cellSizeWithSpacing = wordGridManager.cellSize + wordGridManager.spacing;
        Debug.Log($"GridInputHandler Started. CellSizeWithSpacing: {cellSizeWithSpacing}");

        // --- Highlight Initialization ---
        // Store original scale once grid is likely initialized
        // Use null-conditional operator and null-coalescing operator for safety
        if (wordGridManager.gridCellRects != null && wordGridManager.gridSize > 0)
        {
            originalScale = wordGridManager.gridCellRects[0, 0]?.localScale ?? Vector3.one;
        }
        else
        {
            Debug.LogWarning("GridInputHandler: Could not get original cell scale in Start. Grid might not be initialized yet.", this);
            originalScale = Vector3.one; // Default
        }
        isHighlightApplied = false; // Ensure reset on start

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

        // Reset highlight state as well
        isHighlightApplied = false;
        ClearHighlightLists(); // Ensure lists are cleared if disabled mid-highlight

        Debug.Log("GridInputHandler Enabled");
    }

    void OnDisable()
    {
        // Reset flags on disable
        pendingValidationCheck = false;
        pendingMoveReduction = false;
        scrollOccurredThisDrag = false;

        // Reset highlight state on disable
        if (isHighlightApplied)
        {
            // Instantly reset visuals if disabled while highlighted
            ForceResetHighlightVisuals();
        }

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

        // --- Highlight State Reset ---
        isHighlightApplied = false; // Reset highlight flag
        // Store/update original scale reliably here
        // Use null-conditional operator and null-coalescing operator for safety
        if (wordGridManager.gridCellRects != null && wordGridManager.gridSize > 0)
        {
            originalScale = wordGridManager.gridCellRects[0, 0]?.localScale ?? Vector3.one;
            // Debug.Log($"OnBeginDrag: Updated originalScale to {originalScale}", this);
        }


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

                // <<< Apply highlight AFTER axis is locked >>>
                if (enableDragHighlight)
                {
                    if (isHorizontalDrag && targetRow != -1)
                    {
                        ApplyHighlightRow(targetRow); // No sequence needed
                    }
                    else if (!isHorizontalDrag && targetCol != -1)
                    {
                        ApplyHighlightColumn(targetCol); // No sequence needed
                    }
                }
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
            // float scrollAmountValue = 0; // Not used by WordGridManager in original script

            // Check threshold crossing
            if (previousAccumulated < scrollThreshold && accumulatedDrag >= scrollThreshold)
            {
                scrollDirection = isHorizontalDrag ? 1 : -1; // Right or Up (inverted Y)
                accumulatedDrag = 0; dragStartPosition = currentLocalPos; // Reset accumulation and start position for next potential scroll
            }
            else if (previousAccumulated > -scrollThreshold && accumulatedDrag <= -scrollThreshold)
            {
                scrollDirection = isHorizontalDrag ? -1 : 1; // Left or Down (inverted Y)
                accumulatedDrag = 0; dragStartPosition = currentLocalPos; // Reset accumulation and start position
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
                // WordGridManager's Request methods don't use the float amount in the provided scripts
                if (isHorizontalDrag && targetRow != -1)
                {
                    // Debug.Log($"--> Requesting Row Scroll: Row {targetRow}, Dir {scrollDirection}");
                    wordGridManager.RequestRowScroll(targetRow, scrollDirection, 0f); // Pass 0 for amount
                }
                else if (!isHorizontalDrag && targetCol != -1)
                {
                    // Debug.Log($"--> Requesting Col Scroll: Col {targetCol}, Dir {scrollDirection}");
                    wordGridManager.RequestColumnScroll(targetCol, scrollDirection, 0f); // Pass 0 for amount
                }
                else { Debug.LogWarning($"Scroll triggered but target row/col invalid (R:{targetRow}, C:{targetCol})"); }
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // <<< Reset highlight FIRST >>>
        if (enableDragHighlight && isHighlightApplied)
        {
            ResetHighlight(); // No animation needed
        }

        if (!isDragging) return; // Ignore if drag wasn't properly started

        Debug.Log($"OnEndDrag: Drag finished. Target Start (R:{targetRow}, C:{targetCol}). scrollOccurredThisDrag={scrollOccurredThisDrag}", this);

        // --- Set Pending Flags ---
        // 1. Pending Move Reduction (IF scroll occurred)
        if (scrollOccurredThisDrag)
        {
            pendingMoveReduction = true;
            if (axisLocked) // Ensure axis was locked to determine direction
            {
                if (isHorizontalDrag) { moveReductionRow = targetRow; moveReductionCol = -1; }
                else { moveReductionRow = -1; moveReductionCol = targetCol; }
            }
            else
            {
                // Fallback if somehow scroll occurred but axis wasn't marked locked? Use initial target.
                Debug.LogWarning("OnEndDrag: Scroll occurred but axis wasn't locked? Using initial target for move reduction.");
                if (targetRow != -1) { moveReductionRow = targetRow; moveReductionCol = -1; }
                else if (targetCol != -1) { moveReductionRow = -1; moveReductionCol = targetCol; }
                else { pendingMoveReduction = false; } // Cannot determine target
            }

            if (pendingMoveReduction)
                Debug.Log($"OnEndDrag: Scroll occurred. Set pendingMoveReduction=true for Row:{moveReductionRow} Col:{moveReductionCol}. Update() will handle.", this);
        }
        else { Debug.Log($"OnEndDrag: No scroll occurred. No move reduction pending.", this); }

        // 2. Pending Validation (Always, unless grid is animating - Update handles that)
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

        if (cellSizeWithSpacing <= 0) { Debug.LogError("CalculateTargetRowCol: cellSizeWithSpacing is zero or negative!", this); return; }

        targetCol = Mathf.FloorToInt(relativeX / cellSizeWithSpacing);
        targetRow = wordGridManager.gridSize - 1 - Mathf.FloorToInt(relativeY / cellSizeWithSpacing);

        targetCol = Mathf.Clamp(targetCol, 0, wordGridManager.gridSize - 1);
        targetRow = Mathf.Clamp(targetRow, 0, wordGridManager.gridSize - 1);
    }


    // --- Highlight Methods (No Animation, No Position Change) ---

    private void ApplyHighlightRow(int rowIndex)
    {
        if (isHighlightApplied || wordGridManager == null || wordGridManager.gridCellRects == null) return;
        Debug.Log($"Applying highlight to row {rowIndex}");

        ClearHighlightLists(); // Ensure lists are empty before adding

        for (int c = 0; c < wordGridManager.gridSize; c++)
        {
            // Bounds check
            if (rowIndex < 0 || rowIndex >= wordGridManager.gridCellRects.GetLength(0) || c < 0 || c >= wordGridManager.gridCellRects.GetLength(1)) continue;

            RectTransform cellRect = wordGridManager.gridCellRects[rowIndex, c];
            if (cellRect == null) continue;

            // --- Apply Scale ---
            cellRect.localScale = originalScale * highlightScaleMultiplier;
            currentlyHighlightedCells.Add(cellRect); // Add to list for resetting

            // --- Apply Color ---
            Image cellImage = cellRect.GetComponent<Image>() ?? cellRect.GetComponentInChildren<Image>();
            if (cellImage != null)
            {
                highlightedImages.Add(cellImage);
                originalColors.Add(cellImage.color); // Store original color
                cellImage.color = highlightColor;    // Apply highlight color
            }
            else
            {
                highlightedImages.Add(null); // Keep lists aligned
                originalColors.Add(Color.clear);
                Debug.LogWarning($"Could not find Image component on cell [{rowIndex},{c}] for highlight.", cellRect.gameObject);
            }
        }
        isHighlightApplied = true;
    }

    private void ApplyHighlightColumn(int colIndex)
    {
        if (isHighlightApplied || wordGridManager == null || wordGridManager.gridCellRects == null) return;
        Debug.Log($"Applying highlight to column {colIndex}");

        ClearHighlightLists(); // Ensure lists are empty

        for (int r = 0; r < wordGridManager.gridSize; r++)
        {
            // Bounds check
            if (r < 0 || r >= wordGridManager.gridCellRects.GetLength(0) || colIndex < 0 || colIndex >= wordGridManager.gridCellRects.GetLength(1)) continue;

            RectTransform cellRect = wordGridManager.gridCellRects[r, colIndex];
            if (cellRect == null) continue;

            // --- Apply Scale ---
            cellRect.localScale = originalScale * highlightScaleMultiplier;
            currentlyHighlightedCells.Add(cellRect);

            // --- Apply Color ---
            Image cellImage = cellRect.GetComponent<Image>() ?? cellRect.GetComponentInChildren<Image>();
            if (cellImage != null)
            {
                highlightedImages.Add(cellImage);
                originalColors.Add(cellImage.color);
                cellImage.color = highlightColor;
            }
            else
            {
                highlightedImages.Add(null); // Keep lists aligned
                originalColors.Add(Color.clear);
                Debug.LogWarning($"Could not find Image component on cell [{r},{colIndex}] for highlight.", cellRect.gameObject);
            }
        }
        isHighlightApplied = true;
    }

    // --- Reset methods (No Animation) ---
    private void ResetHighlight()
    {
        ForceResetHighlightVisuals(); // Use the instant reset method
    }

    // Instantly resets visuals (used by ResetHighlight and OnDisable)
    private void ForceResetHighlightVisuals()
    {
        if (!isHighlightApplied) return; // Only reset if applied
                                         // Debug.Log($"Instantly resetting highlight visuals for {currentlyHighlightedCells.Count} cells.");

        for (int i = 0; i < currentlyHighlightedCells.Count; i++)
        {
            RectTransform cellRect = currentlyHighlightedCells[i];
            if (cellRect != null)
            {
                // Set Scale Instantly
                cellRect.localScale = originalScale;

                // Set Color Instantly
                if (i < highlightedImages.Count && highlightedImages[i] != null && i < originalColors.Count)
                {
                    highlightedImages[i].color = originalColors[i];
                }
            }
        }
        isHighlightApplied = false; // Mark as not applied
        ClearHighlightLists();
    }


    private void ClearHighlightLists()
    {
        currentlyHighlightedCells.Clear();
        highlightedImages.Clear();
        originalColors.Clear();
    }

} // End of GridInputHandler class