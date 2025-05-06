using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [Header("Level Data")]
    [SerializeField] private LevelData currentLevelData;

    // --- References to other managers (Assign these in the Inspector) ---
    [SerializeField] private GridManager gridManager; // **** THIS LINE WAS MISSING/COMMENTED ****
    [SerializeField] private WordInventory wordInventory; // **** ADDED THIS ONE TOO - NEEDED NEXT ****
    // [SerializeField] private ScoreManager scoreManager; // Add later

    [Header("Dependencies (Assign in Inspector)")]
    [SerializeField] private WordPlacementValidator wordPlacementValidator; // Needed for WordInventory setup


    // Game State (simplified for now)
    private bool isLevelActive = false;

    void Start()
    {
        StartLevel();
    }

    public void StartLevel()
    {
        if (currentLevelData == null)
        {
            Debug.LogError("LevelManager: No LevelData assigned!");
            isLevelActive = false;
            return;
        }
        // --- Check Dependencies ---
        // Make sure manager references are assigned before using them
        if (gridManager == null)
        {
            Debug.LogError("LevelManager: GridManager reference not assigned in the Inspector!");
            return;
        }
        if (wordInventory == null)
        {
            Debug.LogError("LevelManager: WordInventory reference not assigned in the Inspector!");
            return;
        }
        if (wordPlacementValidator == null)
        {
            Debug.LogError("LevelManager: WordPlacementValidator reference not assigned in the Inspector! (Needed for WordInventory)");
            return;
        }


        Debug.Log($"Starting Level: {currentLevelData.name}");
        Debug.Log($"Grid Size: {currentLevelData.gridWidth}x{currentLevelData.gridHeight}");
        Debug.Log($"Target Score: {currentLevelData.targetScore}");
        Debug.Log($"Words: {string.Join(", ", currentLevelData.wordsForLevel)}");

        // --- Initialization Steps ---
        // 1. Reset Score (Add later)
        // scoreManager?.ResetScore();

        // 2. Reset Validator State (Important for first word rule)
        //wordPlacementValidator.ResetValidator(); // Use the reference assigned above

        // In LevelManager.cs, line 65 (or wherever CreateGrid is called)
        gridManager.CreateGrid(); // Calls the parameterless version

        // 4. Populate Word Inventory (Use the reference assigned above)
        wordInventory.PopulateInventory(currentLevelData.wordsForLevel);


        isLevelActive = true;
        Debug.Log("Level setup completed.");
    }

    // Other methods...
}