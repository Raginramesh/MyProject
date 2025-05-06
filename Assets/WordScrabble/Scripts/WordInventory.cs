using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class WordInventory : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject wordListItemPrefab;
    [SerializeField] private RectTransform scrollContentParent;

    // --- References for Highlight Zone ---
    [Header("Highlight Zone References")]
    [SerializeField] private ScrollRect scrollRect;         // *** ASSIGN the ScrollRect component ***
    [SerializeField] private RectTransform viewportRect;   // *** ASSIGN the Viewport RectTransform ***

    [Header("Highlight Zone Settings")]
    [Range(0f, 1f)] // Restrict values between 0 and 1 (normalized viewport space)
    [SerializeField] private float highlightZoneCenterY = 0.5f; // 0.5 = vertical center of viewport
    [Range(0f, 1f)]
    [SerializeField] private float highlightZoneHeight = 0.3f;  // 0.3 = 30% of viewport height
    [SerializeField] private Vector3 normalScale = Vector3.one;
    [SerializeField] private Vector3 highlightScale = new Vector3(1.15f, 1.15f, 1.0f); // Slightly larger scale
    [SerializeField] private float scaleLerpSpeed = 8f;

    [Header("Dependencies")]
    [SerializeField] private WordPlacementValidator wordPlacementValidator;
    [SerializeField] private GridManager gridManager;
    // [SerializeField] private ScoreManager scoreManager;

    // Runtime Data
    private List<string> availableWords = new List<string>();
    private Dictionary<string, WordListItemUI> wordUIItems = new Dictionary<string, WordListItemUI>();
    private Canvas rootCanvas; // Cache the canvas
    private Camera canvasCamera; // Cache the camera used by the canvas

    // Zone calculation cache
    private float zoneMinY;
    private float zoneMaxY;

    void Start()
    {
        // --- Setup Highlight Zone Listener ---
        if (scrollRect != null)
        {
            // Add listener for scroll changes
            scrollRect.onValueChanged.AddListener(UpdateHighlightStates);
            // Initial check in case list is populated before first scroll
            CalculateZoneBounds();
            // Delay initial update slightly to allow layout to settle? Might not be needed.
            Invoke(nameof(InitialHighlightUpdate), 0.1f);
        }
        else
        {
            Debug.LogError("WordInventory: ScrollRect reference not set! Highlight zone will not work.", this);
        }

        // Cache canvas and camera
        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null)
        {
            canvasCamera = (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : rootCanvas.worldCamera;
        }
    }

    void OnDestroy()
    {
        // --- Cleanup Listener ---
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.RemoveListener(UpdateHighlightStates);
        }
    }

    void CalculateZoneBounds()
    {
        float halfZoneHeight = highlightZoneHeight * 0.5f;
        zoneMinY = highlightZoneCenterY - halfZoneHeight;
        zoneMaxY = highlightZoneCenterY + halfZoneHeight;
    }

    // Called manually and by scroll listener
    void UpdateHighlightStates(Vector2 scrollValue) // Parameter received from onValueChanged
    {
        UpdateAllItemStates();
    }
    void InitialHighlightUpdate() // Called by Invoke
    {
        UpdateAllItemStates();
    }


    void UpdateAllItemStates()
    {
        if (viewportRect == null || wordUIItems.Count == 0) return; // Nothing to do

        // Iterate through all currently active word UI items
        foreach (WordListItemUI itemUI in wordUIItems.Values)
        {
            if (itemUI != null) // Check if item hasn't been destroyed (e.g., by placement)
            {
                // Pass necessary info for the item to check itself
                itemUI.UpdateHighlightState(viewportRect, canvasCamera, zoneMinY, zoneMaxY, normalScale, highlightScale, scaleLerpSpeed);
            }
        }
    }

    public void PopulateInventory(List<string> words)
    {
        // ... (rest of the method remains the same, including sorting) ...
        if (wordListItemPrefab == null || scrollContentParent == null /*... other checks ...*/) return;

        ClearInventory();
        availableWords = new List<string>(words);
        availableWords.Sort();

        foreach (string word in availableWords)
        {
            GameObject newItemGO = Instantiate(wordListItemPrefab, scrollContentParent);
            WordListItemUI itemUI = newItemGO.GetComponent<WordListItemUI>();
            if (itemUI != null)
            {
                itemUI.Setup(word, true, wordPlacementValidator, this, gridManager);
                wordUIItems.Add(word.ToUpper(), itemUI);
            }
            // ... (error handling) ...
        }
        Debug.Log($"Word Inventory populated with {availableWords.Count} words (sorted).");

        // --- Trigger initial highlight check after population ---
        // Use Invoke to delay slightly, allowing layout groups to potentially update first
        CancelInvoke(nameof(InitialHighlightUpdate)); // Cancel previous invokes if any
        Invoke(nameof(InitialHighlightUpdate), 0.1f);
    }

    private void ClearInventory()
    {
        foreach (Transform child in scrollContentParent)
        {
            Destroy(child.gameObject);
        }
        availableWords.Clear();
        wordUIItems.Clear();
    }

    public void UseWord(string word)
    {
        string upperWord = word.ToUpper(); // Use ToUpper for consistent dictionary keys
        if (availableWords.Contains(upperWord) || availableWords.Contains(word)) // Check both just in case
        {
            // Remove based on original case if needed, or just use upper
            availableWords.RemoveAll(w => w.Equals(word, System.StringComparison.OrdinalIgnoreCase));

            if (wordUIItems.ContainsKey(upperWord))
            {
                // The WordListItemUI destroys itself upon successful placement.
                // We just need to remove it from our tracking dictionary.
                wordUIItems.Remove(upperWord);
            }

            Debug.Log($"Word '{word}' used. Remaining: {availableWords.Count}");
            // TODO: Check win condition?
        }
        else
        {
            Debug.LogWarning($"Attempted to use word '{word}' which is not in the available list or dictionary.");
        }
    }
}