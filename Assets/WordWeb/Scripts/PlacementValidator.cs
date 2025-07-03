using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Validates word placement according to game rules.
/// Ensures proper placement constraints and intersections.
/// </summary>
public class PlacementValidator : MonoBehaviour
{
    [Header("Validation Rules")]
    [SerializeField] private bool requireCenterStart = true;
    [SerializeField] private bool requireIntersection = true;
    [SerializeField] private bool allowIsolatedWords = false;
    [SerializeField] private int maxWordLength = 15;
    [SerializeField] private int minWordLength = 2;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // References
    private DynamicGridManager gridManager;
    private Dictionary<Vector2Int, char> placedLetters = new Dictionary<Vector2Int, char>();
    private List<PlacedWordInfo> placedWords = new List<PlacedWordInfo>();
    private bool isFirstWordPlaced = false;

    // Events
    public System.Action<string, Vector2Int, PlacementOrientation> OnValidPlacement;
    public System.Action<string, string> OnInvalidPlacement; // word, reason

    #region Initialization

    void Awake()
    {
        gridManager = FindObjectOfType<DynamicGridManager>();
        
        if (gridManager == null)
        {
            Debug.LogError("PlacementValidator: No DynamicGridManager found!");
        }
    }

    void Start()
    {
        // Subscribe to grid events
        if (gridManager != null)
        {
            gridManager.OnCellPlaced += OnCellPlaced;
            gridManager.OnCellCleared += OnCellCleared;
            gridManager.OnGridCleared += OnGridCleared;
        }
    }

    #endregion

    #region Main Validation

    /// <summary>
    /// Validates if a word can be placed at the specified position and orientation
    /// </summary>
    public ValidationResult ValidatePlacement(string word, Vector2Int startPosition, PlacementOrientation orientation)
    {
        ValidationResult result = new ValidationResult();
        result.isValid = true;
        result.word = word;
        result.startPosition = startPosition;
        result.orientation = orientation;

        // Basic validation
        if (!ValidateBasicConstraints(word, startPosition, orientation, result))
        {
            return result;
        }

        // Check if word fits in grid
        if (!ValidateGridBounds(word, startPosition, orientation, result))
        {
            return result;
        }

        // Check for conflicts with existing letters
        if (!ValidateLetterConflicts(word, startPosition, orientation, result))
        {
            return result;
        }

        // Check game-specific rules
        if (!ValidateGameRules(word, startPosition, orientation, result))
        {
            return result;
        }

        // Calculate intersections
        result.intersections = CalculateIntersections(word, startPosition, orientation);
        
        // Validate intersections if required
        if (!ValidateIntersections(word, startPosition, orientation, result))
        {
            return result;
        }

        // Calculate affected cells
        result.affectedCells = CalculateAffectedCells(word, startPosition, orientation);

        if (enableDebugLogs)
        {
            Debug.Log($"Validation successful for '{word}' at {startPosition} {orientation}");
        }

        if (result.isValid)
        {
            OnValidPlacement?.Invoke(word, startPosition, orientation);
        }
        else
        {
            OnInvalidPlacement?.Invoke(word, result.invalidReason);
        }

        return result;
    }

    #endregion

    #region Validation Methods

    private bool ValidateBasicConstraints(string word, Vector2Int startPosition, PlacementOrientation orientation, ValidationResult result)
    {
        // Check word length
        if (word.Length < minWordLength)
        {
            result.isValid = false;
            result.invalidReason = $"Word too short (minimum {minWordLength} letters)";
            return false;
        }

        if (word.Length > maxWordLength)
        {
            result.isValid = false;
            result.invalidReason = $"Word too long (maximum {maxWordLength} letters)";
            return false;
        }

        // Check for valid characters
        foreach (char c in word)
        {
            if (!char.IsLetter(c))
            {
                result.isValid = false;
                result.invalidReason = "Word contains invalid characters";
                return false;
            }
        }

        return true;
    }

    private bool ValidateGridBounds(string word, Vector2Int startPosition, PlacementOrientation orientation, ValidationResult result)
    {
        if (gridManager == null) return false;

        Vector2Int direction = GetDirectionVector(orientation);
        Vector2Int endPosition = startPosition + direction * (word.Length - 1);

        // Check if word fits within grid bounds
        if (!gridManager.IsPositionValid(startPosition) || !gridManager.IsPositionValid(endPosition))
        {
            result.isValid = false;
            result.invalidReason = "Word extends outside grid bounds";
            return false;
        }

        return true;
    }

    private bool ValidateLetterConflicts(string word, Vector2Int startPosition, PlacementOrientation orientation, ValidationResult result)
    {
        Vector2Int direction = GetDirectionVector(orientation);
        Vector2Int currentPos = startPosition;

        for (int i = 0; i < word.Length; i++)
        {
            char wordLetter = char.ToUpper(word[i]);
            
            // Check if position is already occupied
            if (placedLetters.ContainsKey(currentPos))
            {
                char existingLetter = placedLetters[currentPos];
                
                // Letters must match if overlapping
                if (existingLetter != wordLetter)
                {
                    result.isValid = false;
                    result.invalidReason = $"Letter conflict at position {currentPos}: '{existingLetter}' vs '{wordLetter}'";
                    return false;
                }
            }

            currentPos += direction;
        }

        return true;
    }

    private bool ValidateGameRules(string word, Vector2Int startPosition, PlacementOrientation orientation, ValidationResult result)
    {
        // First word must pass through center
        if (requireCenterStart && !isFirstWordPlaced)
        {
            if (!DoesWordPassThroughCenter(word, startPosition, orientation))
            {
                result.isValid = false;
                result.invalidReason = "First word must pass through the center cell";
                return false;
            }
        }

        // Subsequent words must intersect with existing words (unless isolated words are allowed)
        if (requireIntersection && isFirstWordPlaced && !allowIsolatedWords)
        {
            var intersections = CalculateIntersections(word, startPosition, orientation);
            if (intersections.Count == 0)
            {
                result.isValid = false;
                result.invalidReason = "Word must intersect with existing words";
                return false;
            }
        }

        return true;
    }

    private bool ValidateIntersections(string word, Vector2Int startPosition, PlacementOrientation orientation, ValidationResult result)
    {
        var intersections = result.intersections;

        // Check that all intersections are valid
        foreach (var intersection in intersections)
        {
            char wordLetter = char.ToUpper(word[intersection.wordIndex]);
            char gridLetter = placedLetters[intersection.position];

            if (wordLetter != gridLetter)
            {
                result.isValid = false;
                result.invalidReason = $"Invalid intersection at {intersection.position}";
                return false;
            }
        }

        return true;
    }

    #endregion

    #region Helper Methods

    private Vector2Int GetDirectionVector(PlacementOrientation orientation)
    {
        switch (orientation)
        {
            case PlacementOrientation.Horizontal:
                return Vector2Int.right;
            case PlacementOrientation.Vertical:
                return Vector2Int.up;
            default:
                return Vector2Int.right;
        }
    }

    private bool DoesWordPassThroughCenter(string word, Vector2Int startPosition, PlacementOrientation orientation)
    {
        if (gridManager == null) return false;

        Vector2Int centerPosition = gridManager.CenterPosition;
        Vector2Int direction = GetDirectionVector(orientation);
        Vector2Int currentPos = startPosition;

        for (int i = 0; i < word.Length; i++)
        {
            if (currentPos == centerPosition)
            {
                return true;
            }
            currentPos += direction;
        }

        return false;
    }

    private List<IntersectionInfo> CalculateIntersections(string word, Vector2Int startPosition, PlacementOrientation orientation)
    {
        List<IntersectionInfo> intersections = new List<IntersectionInfo>();
        Vector2Int direction = GetDirectionVector(orientation);
        Vector2Int currentPos = startPosition;

        for (int i = 0; i < word.Length; i++)
        {
            if (placedLetters.ContainsKey(currentPos))
            {
                intersections.Add(new IntersectionInfo
                {
                    position = currentPos,
                    wordIndex = i,
                    existingLetter = placedLetters[currentPos],
                    newLetter = char.ToUpper(word[i])
                });
            }

            currentPos += direction;
        }

        return intersections;
    }

    private List<Vector2Int> CalculateAffectedCells(string word, Vector2Int startPosition, PlacementOrientation orientation)
    {
        List<Vector2Int> affectedCells = new List<Vector2Int>();
        Vector2Int direction = GetDirectionVector(orientation);
        Vector2Int currentPos = startPosition;

        for (int i = 0; i < word.Length; i++)
        {
            affectedCells.Add(currentPos);
            currentPos += direction;
        }

        return affectedCells;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Register a successfully placed word
    /// </summary>
    public void RegisterPlacedWord(string word, Vector2Int startPosition, PlacementOrientation orientation)
    {
        PlacedWordInfo wordInfo = new PlacedWordInfo
        {
            word = word.ToUpper(),
            startPosition = startPosition,
            orientation = orientation,
            placedCells = CalculateAffectedCells(word, startPosition, orientation)
        };

        placedWords.Add(wordInfo);

        // Update placed letters
        Vector2Int direction = GetDirectionVector(orientation);
        Vector2Int currentPos = startPosition;

        for (int i = 0; i < word.Length; i++)
        {
            placedLetters[currentPos] = char.ToUpper(word[i]);
            currentPos += direction;
        }

        if (!isFirstWordPlaced)
        {
            isFirstWordPlaced = true;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"Registered placed word: '{word}' at {startPosition} {orientation}");
        }
    }

    /// <summary>
    /// Remove a placed word (for undo functionality)
    /// </summary>
    public void RemovePlacedWord(string word, Vector2Int startPosition, PlacementOrientation orientation)
    {
        // Find and remove the word info
        PlacedWordInfo wordToRemove = placedWords.FirstOrDefault(w => 
            w.word == word.ToUpper() && 
            w.startPosition == startPosition && 
            w.orientation == orientation);

        if (wordToRemove != null)
        {
            placedWords.Remove(wordToRemove);

            // Remove letters that are not part of other words
            foreach (var cell in wordToRemove.placedCells)
            {
                bool isPartOfOtherWord = placedWords.Any(w => w.placedCells.Contains(cell));
                if (!isPartOfOtherWord)
                {
                    placedLetters.Remove(cell);
                }
            }

            if (enableDebugLogs)
            {
                Debug.Log($"Removed placed word: '{word}' from {startPosition} {orientation}");
            }
        }
    }

    /// <summary>
    /// Clear all placed words
    /// </summary>
    public void ClearAllPlacedWords()
    {
        placedWords.Clear();
        placedLetters.Clear();
        isFirstWordPlaced = false;

        if (enableDebugLogs)
        {
            Debug.Log("Cleared all placed words");
        }
    }

    /// <summary>
    /// Get all currently placed words
    /// </summary>
    public List<PlacedWordInfo> GetPlacedWords()
    {
        return new List<PlacedWordInfo>(placedWords);
    }

    #endregion

    #region Event Handlers

    private void OnCellPlaced(Vector2Int position, char letter)
    {
        // Grid manager handles individual cell placement
        // We track word-level placement through RegisterPlacedWord
    }

    private void OnCellCleared(Vector2Int position)
    {
        // Handle individual cell clearing if needed
    }

    private void OnGridCleared()
    {
        ClearAllPlacedWords();
    }

    #endregion

    #region Getters

    public bool IsFirstWordPlaced => isFirstWordPlaced;
    public int PlacedWordCount => placedWords.Count;
    public Dictionary<Vector2Int, char> PlacedLetters => new Dictionary<Vector2Int, char>(placedLetters);

    #endregion
}

/// <summary>
/// Result of a placement validation
/// </summary>
[System.Serializable]
public class ValidationResult
{
    public bool isValid;
    public string word;
    public Vector2Int startPosition;
    public PlacementOrientation orientation;
    public string invalidReason;
    public List<IntersectionInfo> intersections = new List<IntersectionInfo>();
    public List<Vector2Int> affectedCells = new List<Vector2Int>();
}

/// <summary>
/// Information about a word intersection
/// </summary>
[System.Serializable]
public class IntersectionInfo
{
    public Vector2Int position;
    public int wordIndex;
    public char existingLetter;
    public char newLetter;
}

/// <summary>
/// Information about a placed word
/// </summary>
[System.Serializable]
public class PlacedWordInfo
{
    public string word;
    public Vector2Int startPosition;
    public PlacementOrientation orientation;
    public List<Vector2Int> placedCells;
}
