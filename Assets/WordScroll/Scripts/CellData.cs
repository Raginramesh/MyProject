using UnityEngine;

/// <summary>
/// Enhanced cell data structure that supports different cell types
/// Replaces the simple char system with rich cell information
/// </summary>
[System.Serializable]
public struct CellData
{
    [Header("Basic Data")]
    public CellType cellType;
    public string displayContent;    // What appears in the cell ("A", "", etc.)
    public char letterValue;        // For validation - actual letter or '\0' for blanks
    
    [Header("Visual Data")]
    public Color backgroundColor;
    public Color textColor;
    public Color borderColor;
    
    [Header("Gameplay Data")]
    public int scoreValue;
    public bool participatesInValidation;
    public bool isUsed;             // For tracking one-time use cells (future)
    
    [Header("Effects Data")]
    public bool hasSpecialEffect;
    public float effectIntensity;
    
    /// <summary>
    /// Create a standard letter cell
    /// </summary>
    public static CellData CreateLetterCell(char letter)
    {
        return new CellData
        {
            cellType = CellType.Letter,
            displayContent = letter.ToString(),
            letterValue = letter,
            backgroundColor = Color.white,
            textColor = Color.black,
            borderColor = Color.gray,
            scoreValue = GetDefaultScoreForLetter(letter),
            participatesInValidation = true,
            isUsed = false,
            hasSpecialEffect = false,
            effectIntensity = 0f
        };
    }
    
    /// <summary>
    /// Create a blank/empty cell
    /// These cells appear empty but participate in grid-size validation for strategic gameplay
    /// </summary>
    public static CellData CreateBlankCell()
    {
        return new CellData
        {
            cellType = CellType.Blank,
            displayContent = "",
            letterValue = '\0',
            backgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f), // Very light gray - more "empty" looking
            textColor = Color.clear, // Completely transparent text
            borderColor = new Color(0.8f, 0.8f, 0.8f, 0.5f),     // Light, semi-transparent border
            scoreValue = 0,
            participatesInValidation = true, // Blanks participate in grid-size validation
            isUsed = false,
            hasSpecialEffect = false,
            effectIntensity = 0f
        };
    }
    
    /// <summary>
    /// Create a cell from ScriptableObject configuration
    /// </summary>
    public static CellData CreateFromCellTypeData(CellTypeData cellTypeData, char letter = '\0')
    {
        return new CellData
        {
            cellType = cellTypeData.CellType,
            displayContent = cellTypeData.CellType == CellType.Letter ? letter.ToString() : cellTypeData.DisplayContent,
            letterValue = cellTypeData.CellType == CellType.Letter ? letter : '\0',
            backgroundColor = cellTypeData.BackgroundColor,
            textColor = cellTypeData.TextColor,
            borderColor = cellTypeData.BorderColor,
            scoreValue = cellTypeData.CellType == CellType.Letter ? GetDefaultScoreForLetter(letter) : cellTypeData.ScoreValue,
            participatesInValidation = cellTypeData.ParticipatesInValidation,
            isUsed = false,
            hasSpecialEffect = cellTypeData.HasSpecialEffect,
            effectIntensity = cellTypeData.GlowIntensity
        };
    }
    
    /// <summary>
    /// Check if this cell is empty/blank
    /// </summary>
    public bool IsBlank => cellType == CellType.Blank;
    
    /// <summary>
    /// Check if this cell contributes to word formation
    /// In Option A validation, blanks participate in grid-size validation but not dictionary lookup
    /// </summary>
    public bool IsValidationCell => participatesInValidation;
    
    /// <summary>
    /// Get the visual display string for this cell
    /// </summary>
    public string GetDisplayString()
    {
        if (IsBlank) return ""; // Blank cells show nothing
        return displayContent;
    }
    
    /// <summary>
    /// Get default score for a letter (basic Scrabble-like values)
    /// </summary>
    private static int GetDefaultScoreForLetter(char letter)
    {
        // Basic scoring - can be overridden by GameManager's scoring system
        letter = char.ToUpper(letter);
        switch (letter)
        {
            case 'A': case 'E': case 'I': case 'O': case 'U': case 'L': case 'N': case 'S': case 'T': case 'R':
                return 1;
            case 'D': case 'G':
                return 2;
            case 'B': case 'C': case 'M': case 'P':
                return 3;
            case 'F': case 'H': case 'V': case 'W': case 'Y':
                return 4;
            case 'K':
                return 5;
            case 'J': case 'X':
                return 8;
            case 'Q': case 'Z':
                return 10;
            default:
                return 1;
        }
    }
    
    /// <summary>
    /// Debug representation
    /// </summary>
    public override string ToString()
    {
        return $"CellData[{cellType}]:'{displayContent}'({letterValue})-{scoreValue}pts";
    }
}
