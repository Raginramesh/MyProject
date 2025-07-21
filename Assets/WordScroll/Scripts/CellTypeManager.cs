using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages different cell types and their generation probabilities
/// </summary>
[CreateAssetMenu(fileName = "Cell Type Manager", menuName = "Word Scroll/Cell Type Manager", order = 2)]
public class CellTypeManager : ScriptableObject
{
    [Header("Cell Type Configurations")]
    [SerializeField] private List<CellTypeData> availableCellTypes = new List<CellTypeData>();
    
    [Header("Generation Settings")]
    [SerializeField] private float blankCellProbability = 0.15f; // 15% chance for blank cells
    [SerializeField] private int minBlanksPerGrid = 1;
    [SerializeField] private int maxBlanksPerGrid = 3;
    [SerializeField] private bool preferBlanksAtEndOfCenterRow = true;
    
    [Header("Letter Distribution")]
    [SerializeField] private string letterDistribution = "AAAAAAAAABCCDDDDEEEEEEEEEEEEFGGGHHIIIIIIIIIJKLLLLMMNNNNNNOOOOOOOOPPQRRRRRRSSSSTTTTTTUUUUVVWWXYYZ";
    
    [Tooltip("Use advanced weighted letter system from WordGridManager (matches English frequency distribution)")]
    [SerializeField] private bool useAdvancedWeightedSystem = true;
    
    /// <summary>
    /// Cached weighted letters list for performance (populated with WordGridManager's weights)
    /// </summary>
    private List<char> cachedWeightedLetters = new List<char>();
    private bool isWeightedLettersInitialized = false;
    
    /// <summary>
    /// Get a random letter based on English frequency distribution
    /// </summary>
    public char GetRandomLetter()
    {
        if (useAdvancedWeightedSystem)
        {
            if (!isWeightedLettersInitialized)
            {
                InitializeWeightedLetters();
            }
            
            if (cachedWeightedLetters.Count > 0)
            {
                return cachedWeightedLetters[Random.Range(0, cachedWeightedLetters.Count)];
            }
        }
        
        // Fallback to simple letter distribution
        if (string.IsNullOrEmpty(letterDistribution))
        {
            return (char)('A' + Random.Range(0, 26));
        }
        
        return letterDistribution[Random.Range(0, letterDistribution.Length)];
    }
    
    /// <summary>
    /// Initialize weighted letters using the same distribution as WordGridManager
    /// This ensures compatibility and consistent letter frequency
    /// </summary>
    private void InitializeWeightedLetters()
    {
        cachedWeightedLetters.Clear();
        
        // Use the same weighted distribution as WordGridManager
        // E: 12, A: 9, I: 9, O: 8, N: 6, R: 6, T: 6, L: 4, S: 4, U: 4, D: 4, G: 3
        // B: 2, C: 2, M: 2, P: 2, F: 2, H: 2, V: 2, W: 2, Y: 2
        // K: 1, J: 1, X: 1, Q: 1, Z: 1
        
        AddLettersToCache("E", 12); AddLettersToCache("A", 9); AddLettersToCache("I", 9); AddLettersToCache("O", 8);
        AddLettersToCache("N", 6); AddLettersToCache("R", 6); AddLettersToCache("T", 6); AddLettersToCache("L", 4);
        AddLettersToCache("S", 4); AddLettersToCache("U", 4); AddLettersToCache("D", 4); AddLettersToCache("G", 3);
        AddLettersToCache("B", 2); AddLettersToCache("C", 2); AddLettersToCache("M", 2); AddLettersToCache("P", 2);
        AddLettersToCache("F", 2); AddLettersToCache("H", 2); AddLettersToCache("V", 2); AddLettersToCache("W", 2); AddLettersToCache("Y", 2);
        AddLettersToCache("K", 1); AddLettersToCache("J", 1); AddLettersToCache("X", 1); AddLettersToCache("Q", 1); AddLettersToCache("Z", 1);
        
        isWeightedLettersInitialized = true;
        
        Debug.Log($"📊 CellTypeManager: Initialized weighted letters with {cachedWeightedLetters.Count} entries");
    }
    
    /// <summary>
    /// Helper method to add letters to the weighted cache
    /// </summary>
    private void AddLettersToCache(string letters, int count)
    {
        foreach (char letter in letters)
        {
            for (int i = 0; i < count; i++) 
            { 
                cachedWeightedLetters.Add(letter); 
            }
        }
    }
    
    /// <summary>
    /// Get a cell type based on probabilities and constraints
    /// </summary>
    public CellTypeData GetRandomCellType(int currentBlankCount, int totalCells, Vector2Int position, int gridSize)
    {
        // Check if we should force a blank cell
        if (ShouldForceBlankCell(currentBlankCount, totalCells, position, gridSize))
        {
            return GetCellTypeByType(CellType.Blank);
        }
        
        // Check if we should avoid blank cells
        if (ShouldAvoidBlankCell(currentBlankCount, totalCells))
        {
            return GetCellTypeByType(CellType.Letter);
        }
        
        // Normal random selection based on weights
        return GetWeightedRandomCellType();
    }
    
    /// <summary>
    /// Generate a complete cell for the grid
    /// </summary>
    public CellData GenerateCell(int currentBlankCount, int totalCells, Vector2Int position, int gridSize)
    {
        var cellTypeData = GetRandomCellType(currentBlankCount, totalCells, position, gridSize);
        
        if (cellTypeData.CellType == CellType.Letter)
        {
            char randomLetter = GetRandomLetter();
            return CellData.CreateFromCellTypeData(cellTypeData, randomLetter);
        }
        else
        {
            return CellData.CreateFromCellTypeData(cellTypeData);
        }
    }
    
    /// <summary>
    /// Get cell type by specific type
    /// </summary>
    public CellTypeData GetCellTypeByType(CellType cellType)
    {
        var cellTypeData = availableCellTypes.FirstOrDefault(ct => ct.CellType == cellType);
        if (cellTypeData == null)
        {
            // Create default if not found
            return CreateDefaultCellTypeData(cellType);
        }
        return cellTypeData;
    }
    
    /// <summary>
    /// Check if we should force a blank cell at this position
    /// </summary>
    private bool ShouldForceBlankCell(int currentBlankCount, int totalCells, Vector2Int position, int gridSize)
    {
        // If we don't have minimum blanks and we're near the end, force blanks
        if (currentBlankCount < minBlanksPerGrid && totalCells > (gridSize * gridSize * 0.8f))
        {
            return true;
        }
        
        // If we prefer blanks at end of center row
        if (preferBlanksAtEndOfCenterRow)
        {
            int centerRow = (gridSize - 1) / 2;
            if (position.y == centerRow && position.x >= gridSize - 2) // Last 2 positions of center row
            {
                return Random.value < 0.4f; // 40% chance for end positions
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Check if we should avoid blank cells
    /// </summary>
    private bool ShouldAvoidBlankCell(int currentBlankCount, int totalCells)
    {
        // If we already have enough blanks
        if (currentBlankCount >= maxBlanksPerGrid)
        {
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Get a random cell type based on weights
    /// </summary>
    private CellTypeData GetWeightedRandomCellType()
    {
        if (availableCellTypes == null || availableCellTypes.Count == 0)
        {
            return CreateDefaultCellTypeData(CellType.Letter);
        }
        
        // Calculate total weight
        float totalWeight = availableCellTypes.Sum(ct => ct.SpawnWeight);
        float randomValue = Random.value * totalWeight;
        
        float currentWeight = 0f;
        foreach (var cellType in availableCellTypes)
        {
            currentWeight += cellType.SpawnWeight;
            if (randomValue <= currentWeight)
            {
                return cellType;
            }
        }
        
        // Fallback to first available
        return availableCellTypes[0];
    }
    
    /// <summary>
    /// Create default cell type data if none exists
    /// </summary>
    private CellTypeData CreateDefaultCellTypeData(CellType cellType)
    {
        var defaultData = CreateInstance<CellTypeData>();
        
        // Set basic properties via reflection or create a temporary one
        // For now, return null and handle in calling code
        Debug.LogWarning($"No CellTypeData found for {cellType}. Please create one in the CellTypeManager.");
        return null;
    }
    
    /// <summary>
    /// Validate the configuration
    /// </summary>
    private void OnValidate()
    {
        blankCellProbability = Mathf.Clamp01(blankCellProbability);
        minBlanksPerGrid = Mathf.Max(0, minBlanksPerGrid);
        maxBlanksPerGrid = Mathf.Max(minBlanksPerGrid, maxBlanksPerGrid);
        
        if (string.IsNullOrEmpty(letterDistribution))
        {
            letterDistribution = "AAAAAAAAABCCDDDDEEEEEEEEEEEEFGGGHHIIIIIIIIIJKLLLLMMNNNNNNOOOOOOOOPPQRRRRRRSSSSTTTTTTUUUUVVWWXYYZ";
        }
    }
    
    /// <summary>
    /// Debug info about current configuration
    /// </summary>
    [ContextMenu("Debug Cell Type Info")]
    public void DebugCellTypeInfo()
    {
        Debug.Log($"=== CELL TYPE MANAGER DEBUG ===");
        Debug.Log($"Available Cell Types: {availableCellTypes.Count}");
        Debug.Log($"Blank Cell Probability: {blankCellProbability:P1}");
        Debug.Log($"Blank Range: {minBlanksPerGrid}-{maxBlanksPerGrid}");
        Debug.Log($"Letter Distribution Length: {letterDistribution.Length}");
        
        foreach (var cellType in availableCellTypes)
        {
            if (cellType != null)
            {
                Debug.Log($"  - {cellType.CellTypeName}: {cellType.CellType} (Weight: {cellType.SpawnWeight})");
            }
        }
    }
}
