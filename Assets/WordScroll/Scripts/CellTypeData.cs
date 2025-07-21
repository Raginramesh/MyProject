using UnityEngine;

/// <summary>
/// Defines the different types of cells that can exist in the grid
/// </summary>
public enum CellType
{
    Letter = 0,     // Standard A-Z letter
    Blank = 1       // Empty cell that doesn't contribute to word validation
}

/// <summary>
/// ScriptableObject that defines a cell type configuration
/// </summary>
[CreateAssetMenu(fileName = "New Cell Type", menuName = "Word Scroll/Cell Type", order = 1)]
public class CellTypeData : ScriptableObject
{
    [Header("Basic Properties")]
    [SerializeField] private string cellTypeName;
    [SerializeField] private CellType cellType = CellType.Letter;
    
    [Header("Display Properties")]
    [SerializeField] private string displayContent = "";
    [SerializeField] private Color backgroundColor = Color.white;
    [SerializeField] private Color textColor = Color.black;
    [SerializeField] private Color borderColor = Color.gray;
    
    [Header("Gameplay Properties")]
    [SerializeField] private int scoreValue = 0;
    [SerializeField] private bool participatesInValidation = true;
    [SerializeField] private float spawnWeight = 1f; // Higher = more likely to spawn
    
    [Header("Visual Effects")]
    [SerializeField] private bool hasSpecialEffect = false;
    [SerializeField] private float pulseSpeed = 1f;
    [SerializeField] private float glowIntensity = 0f;
    
    // Public Properties
    public string CellTypeName => cellTypeName;
    public CellType CellType => cellType;
    public string DisplayContent => displayContent;
    public Color BackgroundColor => backgroundColor;
    public Color TextColor => textColor;
    public Color BorderColor => borderColor;
    public int ScoreValue => scoreValue;
    public bool ParticipatesInValidation => participatesInValidation;
    public float SpawnWeight => spawnWeight;
    public bool HasSpecialEffect => hasSpecialEffect;
    public float PulseSpeed => pulseSpeed;
    public float GlowIntensity => glowIntensity;
    
    /// <summary>
    /// Validate the cell type configuration
    /// </summary>
    private void OnValidate()
    {
        // Ensure display content matches cell type
        if (cellType == CellType.Blank && string.IsNullOrEmpty(displayContent))
        {
            displayContent = "";
        }
        
        // Ensure spawn weight is positive
        if (spawnWeight < 0f) spawnWeight = 0f;
        
        // Auto-set validation participation based on type
        if (cellType == CellType.Blank)
        {
            participatesInValidation = false;
            if (scoreValue < 0) scoreValue = 0;
        }
    }
}
