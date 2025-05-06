using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections; // Required for Coroutines
using System.Collections.Generic; // Required for List

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
// Ensure HorizontalLayoutGroup and ContentSizeFitter are present (or add dynamically if preferred)
[RequireComponent(typeof(HorizontalLayoutGroup))]
[RequireComponent(typeof(ContentSizeFitter))]
public class WordListItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // --- Remove old wordText reference ---
    // [SerializeField] private TextMeshProUGUI wordText;

    [Header("UI References")]
    [SerializeField] private Image backgroundImage; // Optional: Background for the whole word item
    [SerializeField] private GameObject letterTileMiniPrefab; // *** ASSIGN your LetterTileMiniPrefab ***
    [SerializeField] private Transform letterContainer; // *** ASSIGN the GameObject THIS script is on (it has the HorizontalLayoutGroup) ***

    [Header("Appearance")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color dragColor = Color.yellow;
    [SerializeField] private Color invalidDropColor = Color.red;
    [SerializeField] private float placementAnimationDelay = 0.08f; // Delay between each letter appearing on grid

    // --- Highlight State ---
    private bool isInHighlightZone = false; // Can this item be dragged?
    private Vector3 targetScale = Vector3.one;
    private float currentScaleLerpSpeed = 8f;

    // Data
    private string word;
    private bool isNew; // Keep this if you still want 'new' word indication

    // Runtime References (Set by WordInventory during Setup)
    private WordPlacementValidator wordPlacementValidator;
    private WordInventory wordInventory;
    private GridManager gridManager;
    // private ScoreManager scoreManager; // Add later
    private RectTransform itemRectTransform;

    // Dragging State
    private RectTransform rectTransform;
    private Canvas rootCanvas;
    private CanvasGroup canvasGroup;
    private Vector3 originalPosition;
    private Transform originalParent;
    private int originalSiblingIndex;
    private bool isDragging = false;
    private bool currentPlacementValid = false;
    private Coroutine placementCoroutine = null; // To manage the placement animation

    // Keep track of instantiated letter tiles if needed (e.g., for pooling)
    private List<GameObject> currentLetterTiles = new List<GameObject>();

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        // Ensure letterContainer is assigned, default to this transform if needed
        if (letterContainer == null)
        {
            letterContainer = transform;
        }
        if (letterTileMiniPrefab == null) Debug.LogError("WordListItemUI: letterTileMiniPrefab reference not set!", this);
        itemRectTransform = GetComponent<RectTransform>(); // Cache this component
        targetScale = transform.localScale; // Initialize target scale
    }

    void Update()
    {
        // --- Smoothly Lerp Scale ---
        if (transform.localScale != targetScale)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * currentScaleLerpSpeed);
        }
    }

    // --- NEW Method called by WordInventory ---
    public void UpdateHighlightState(RectTransform viewport, Camera canvasCam, float zoneMinNormY, float zoneMaxNormY, Vector3 normScale, Vector3 highScale, float lerpSpeed)
    {
        if (itemRectTransform == null || viewport == null) return; // Safety check

        bool wasInZone = isInHighlightZone; // Store previous state

        // Calculate item's center Y position relative to the viewport (normalized 0-1)
        float normalizedY = CalculateNormalizedViewportY(viewport, canvasCam);

        // Determine if it's within the defined zone
        isInHighlightZone = (normalizedY >= zoneMinNormY && normalizedY <= zoneMaxNormY);

        // Set the target scale based on whether it's in the zone
        targetScale = isInHighlightZone ? highScale : normScale;
        currentScaleLerpSpeed = lerpSpeed;

        // Optional: Add visual feedback beyond scaling (e.g., change background color slightly?)
        if (isInHighlightZone != wasInZone)
        {
            // Debug.Log($"{word} {(isInHighlightZone ? "entered" : "exited")} highlight zone.");
            // UpdateAppearance(); // If appearance depends on highlight state
        }
    }

    // --- Helper to Calculate Position ---
    private float CalculateNormalizedViewportY(RectTransform viewport, Camera canvasCam)
    {
        // Get the center of this item's RectTransform in world space
        Vector3 worldCenter = itemRectTransform.TransformPoint(itemRectTransform.rect.center);

        // Convert world center to the viewport's local coordinate space
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, RectTransformUtility.WorldToScreenPoint(canvasCam, worldCenter), canvasCam, out Vector2 localPoint))
        {
            // Normalize the Y position within the viewport (0=bottom, 1=top)
            // Adjust for the viewport's pivot
            float normalizedY = (localPoint.y + viewport.rect.height * viewport.pivot.y) / viewport.rect.height;
            return normalizedY;
        }

        // If conversion fails (e.g., item is completely off-screen relative to viewport), return value outside 0-1 range
        return -1f;
    }


    public void Setup(string word, bool isNew, WordPlacementValidator validator, WordInventory inventory, GridManager gridMgr /*, ScoreManager scoreMgr */)
    {
        this.word = word.ToUpper(); // Store word consistently
        this.isNew = isNew;
        this.wordPlacementValidator = validator;
        this.wordInventory = inventory;
        this.gridManager = gridMgr;
        // this.scoreManager = scoreMgr;

        PopulateLetterTiles();
        UpdateAppearance(); // Set initial background color etc.
    }

    private void PopulateLetterTiles()
    {
        // Clear existing tiles first (important if reusing items from a pool)
        foreach (GameObject oldTile in currentLetterTiles)
        {
            Destroy(oldTile);
        }
        currentLetterTiles.Clear();

        if (letterTileMiniPrefab == null) return;

        // Instantiate a mini tile for each letter in the word
        foreach (char letter in word)
        {
            GameObject tileInstance = Instantiate(letterTileMiniPrefab, letterContainer);
            TextMeshProUGUI textComp = tileInstance.GetComponentInChildren<TextMeshProUGUI>();
            if (textComp != null)
            {
                textComp.text = letter.ToString();
            }
            else
            {
                Debug.LogWarning($"LetterTileMiniPrefab is missing TextMeshProUGUI child on instance for letter {letter}.", tileInstance);
            }
            currentLetterTiles.Add(tileInstance);
        }

        // Optional: Force layout rebuild if ContentSizeFitter doesn't update immediately
        // LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
    }


    private void UpdateAppearance()
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = isDragging ? (currentPlacementValid ? dragColor : invalidDropColor) : normalColor;
        }
        // Add other visual state changes here (e.g., based on 'isNew')
    }

    // --- Drag Handlers (Largely the same logic, dragging the root object) ---

    public void OnBeginDrag(PointerEventData eventData)
    {
        // --- DRAG CONTROL ADDED HERE ---
        if (!isInHighlightZone)
        {
            // Prevent drag from starting if not in the zone
            // Setting pointerDrag to null tells the EventSystem this object shouldn't handle the drag.
            eventData.pointerDrag = null;
            // Debug.Log($"Drag prevented for '{word}': Outside highlight zone.");
            return;
        }
        // --- END DRAG CONTROL ---

        if (string.IsNullOrEmpty(word) || wordPlacementValidator == null || gridManager == null || wordInventory == null || placementCoroutine != null) return; // Prevent drag during placement

        isDragging = true;
        originalPosition = rectTransform.position;
        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
        transform.SetParent(rootCanvas.transform, true);
        transform.SetAsLastSibling();
        canvasGroup.blocksRaycasts = false; // Raycasts hit things *behind* this item now
        canvasGroup.alpha = 0.8f;
        currentPlacementValid = false;
        UpdateAppearance();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        // Move the item
        Vector3 globalMousePos;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out globalMousePos))
        { rectTransform.position = globalMousePos; }

        // Check validity (same as before)
        Vector2Int potentialCoords;
        Camera uiCamera = (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : eventData.pressEventCamera;
        if (gridManager.ScreenPointToGridCoords(eventData.position, uiCamera, out potentialCoords))
        {
            bool isHorizontal = true; // TODO: Orientation toggle
            PlacementResult previewResult = wordPlacementValidator.ValidatePlacement(word, potentialCoords, isHorizontal);
            currentPlacementValid = previewResult.IsValid;
            // TODO: Optional - Show visual preview/ghost on grid?
        }
        else { currentPlacementValid = false; }
        UpdateAppearance();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        isDragging = false;
        // Don't immediately block raycasts or reset alpha if placement might be valid,
        // wait for the animation/placement logic to finish.

        Vector2Int finalCoords;
        Camera uiCamera = (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : eventData.pressEventCamera;
        bool droppedOnGrid = gridManager.ScreenPointToGridCoords(eventData.position, uiCamera, out finalCoords);

        if (droppedOnGrid)
        {
            bool isHorizontal = true; // TODO: Orientation toggle
            PlacementResult finalResult = wordPlacementValidator.ValidatePlacement(word, finalCoords, isHorizontal);

            if (finalResult.IsValid)
            {
                Debug.Log($"Valid placement for '{word}' at {finalCoords}. Starting placement animation.");
                // --- Start the Placement Animation ---
                // Disable dragging visuals immediately
                canvasGroup.alpha = 0; // Hide the dragged item
                canvasGroup.blocksRaycasts = true; // Allow interaction with grid again

                // Start the coroutine to place letters one by one
                placementCoroutine = StartCoroutine(AnimatePlacement(finalResult));
                // The coroutine will handle destroying this object upon completion

                return; // Exit, letting the coroutine handle the rest
            }
            else { Debug.LogWarning($"Invalid placement for '{word}' at {finalCoords}: {finalResult.ErrorMessage}"); }
        }
        else { Debug.LogWarning($"Dropped '{word}' outside of grid."); }

        // --- If placement failed or dropped outside grid ---
        // Reset alpha and raycast blocking here since placement didn't start
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1.0f;
        ResetPosition();
        currentPlacementValid = false;
        UpdateAppearance();
    }

    // --- Coroutine for Animated Placement ---
    private IEnumerator AnimatePlacement(PlacementResult result)
    {
        bool firstLetter = true; // Flag for validator confirmation

        // Place letters one by one onto the grid
        for (int i = 0; i < result.WordCoordinates.Count; i++)
        {
            Vector2Int coord = result.WordCoordinates[i];
            char letter = word[i]; // Get the correct letter from the original word

            // Check if the tile is already occupied by the correct letter (overlap)
            TileData targetTile = gridManager.GetTileData(coord);
            bool isOverlap = targetTile != null && targetTile.IsOccupied && targetTile.Letter == letter;

            // Only place/animate if it's not a pre-existing overlap tile
            if (!isOverlap)
            {
                // 1. Place the letter logically on the grid data
                bool placed = gridManager.TrySetLetter(coord, letter);

                if (placed)
                {
                    // TODO: Add actual animation here if desired
                    // e.g., Instantiate a flying letter effect from list item pos to grid pos
                    // For now, just wait
                    yield return new WaitForSeconds(placementAnimationDelay);
                }
                else
                {
                    // Should not happen if validation passed, but handle defensively
                    Debug.LogError($"Placement Error: Failed to set letter {letter} at {coord} even after validation passed!");
                    // Potentially break or revert? For now, continue.
                }
            }
            else
            {
                // If it's just an overlap, we don't need to animate, just skip the delay
                // Debug.Log($"Skipping animation for overlap letter '{letter}' at {coord}");
            }


            // 2. Confirm first word placement *after* the first valid letter is logically placed
            if (firstLetter)
            {
                wordPlacementValidator.ConfirmFirstWordPlaced();
                firstLetter = false; // Only do this once per word placement
            }
        }

        // --- Post-Placement Cleanup ---
        Debug.Log($"Placement animation finished for '{word}'. Score: {result.Score}");

        // 3. Add Score
        // scoreManager?.AddScore(result.Score); // Uncomment when ScoreManager exists

        // 4. Notify Inventory to remove word from available list
        wordInventory?.UseWord(word);

        // 5. Destroy this list item GameObject
        Destroy(gameObject);

        placementCoroutine = null; // Allow dragging again
    }


    private void ResetPosition()
    {
        // Return to original parent and position in the list
        transform.SetParent(originalParent, false);
        transform.SetSiblingIndex(originalSiblingIndex);
        // Ensure layout group updates if needed
        // LayoutRebuilder.ForceRebuildLayoutImmediate(originalParent as RectTransform);
        Debug.Log($"Reset position for {word}");
    }

    public void MarkAsSeen() { /* ... */ }

    // Ensure coroutine stops if object is destroyed prematurely
    void OnDestroy()
    {
        if (placementCoroutine != null)
        {
            StopCoroutine(placementCoroutine);
            placementCoroutine = null;
        }
    }
}