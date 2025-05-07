using UnityEngine;
using System.Linq; // For string.Join
using System.Collections.Generic; // For List

public class LevelManager : MonoBehaviour
{
    [Header("Level Data")]
    [SerializeField] private LevelData currentLevelData; // Your ScriptableObject for level settings

    // --- References to other managers (Assign these in the Inspector) ---
    [SerializeField] private GridManager gridManager;
    [SerializeField] private WordInventory wordInventory;
    // [SerializeField] private ScoreManager scoreManager; // Add later

    [Header("Dependencies (Assign in Inspector)")]
    [SerializeField] private WordPlacementValidator wordPlacementValidator;

    // Game State
    private bool isLevelActive = false;

    void Start()
    {
        StartLevel();
    }

    public void StartLevel()
    {
        if (currentLevelData == null)
        {
            Debug.LogError("LevelManager: No LevelData ScriptableObject assigned in the Inspector!");
            isLevelActive = false;
            return;
        }

        if (gridManager == null)
        {
            Debug.LogError("LevelManager: GridManager reference not assigned in the Inspector!");
            return;
        }
        if (wordPlacementValidator == null)
        {
            Debug.LogError("LevelManager: WordPlacementValidator reference not assigned in the Inspector!");
            return;
        }
        if (wordInventory == null)
        {
            Debug.LogWarning("LevelManager: WordInventory reference not assigned. Word population will be skipped.", this);
        }

        Debug.Log($"LevelManager: Starting Level setup for: {currentLevelData.name}");
        if (currentLevelData.wordsForLevel != null)
        {
            Debug.Log($"LevelData - Words: {string.Join(", ", currentLevelData.wordsForLevel)}");
        }
        else
        {
            Debug.LogWarning("LevelManager: currentLevelData.wordsForLevel is null!");
        }

        // 1. Reset Validator State (Important for first word rule)
        // This ensures the validator expects the player's first word to follow the rules.
        wordPlacementValidator.ResetFirstWordState();

        // 2. Create/Ensure Grid is Ready
        // GridManager.CreateGrid() is called in its Awake() using its own Inspector settings.
        // This method is also responsible for coloring the center tile.
        // If GridManager.Awake doesn't call CreateGrid, you might need an explicit call here:
        // gridManager.CreateGrid();
        // For now, assuming GridManager's Awake handles its creation and center tile coloring.

        // 3. Initialize WordPlacementValidator's center coordinate for game rules.
        // This must happen *after* the grid is confirmed to be created and dimensions are known.
        if (gridManager.CurrentGridWidth > 0 && gridManager.CurrentGridHeight > 0)
        {
            wordPlacementValidator.InitializeCenterCoordinate(gridManager);
        }
        else
        {
            Debug.LogError("LevelManager: GridManager dimensions are zero when trying to initialize WordPlacementValidator center. Grid might not be created yet or failed to create.", this);
            isLevelActive = false;
            return; // Critical failure.
        }

        // 4. Populate Word Inventory
        if (wordInventory != null && currentLevelData.wordsForLevel != null)
        {
            wordInventory.PopulateInventory(currentLevelData.wordsForLevel);
            Debug.Log($"LevelManager: Word inventory populated with {currentLevelData.wordsForLevel.Count} words.");
        }
        else if (wordInventory == null)
        {
            Debug.LogWarning("LevelManager: WordInventory not assigned, skipping word population.");
        }
        else
        {
            Debug.LogWarning("LevelManager: No words found in LevelData to populate inventory, or wordsForLevel collection is null.");
        }

        // 5. Reset Score (if you have a scoreManager)
        // scoreManager?.ResetScore();

        isLevelActive = true;
        Debug.Log("LevelManager: Level setup completed. Center tile should be colored. No initial letter placed.");
    }

    // The PlaceInitialLetter() method is no longer called and can be removed or commented out.
    /*
    void PlaceInitialLetter()
    {
        // This method is no longer used.
        // The WordPlacementValidator.ConfirmFirstWordPlaced() will be called when the player
        // successfully places their first word.
    }
    */
}