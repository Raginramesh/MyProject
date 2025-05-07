using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Required for .Any()

public class PlacementResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; }
    public List<Vector2Int> WordCoordinates { get; set; } // The grid cells the word would occupy
    public int Score { get; set; } // Placeholder for scoring logic
    public List<Vector2Int> IntersectionCoordinates { get; set; } // Tiles where the new word intersects existing letters

    public PlacementResult()
    {
        IsValid = false;
        ErrorMessage = "Unknown validation error.";
        WordCoordinates = new List<Vector2Int>();
        Score = 0;
        IntersectionCoordinates = new List<Vector2Int>();
    }
}

public class WordPlacementValidator : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private GridManager gridManager;

    [Header("Game Rules")]
    private Vector2Int centerCoordinate = new Vector2Int(7, 7); // Example, adjust to your grid center
    [SerializeField] private bool requireFirstWordOnCenter = true;
    [SerializeField] private bool requireConnectionToExisting = true;

    private bool isFirstWordPlaced = false;

    void Start()
    {
        if (gridManager == null)
        {
            Debug.LogError("WordPlacementValidator: GridManager reference not set in Inspector!", this);
        }
    }

    public void InitializeCenterCoordinate(GridManager gridManager) // Call this from LevelManager
    {
        if (gridManager != null && gridManager.CurrentGridWidth > 0 && gridManager.CurrentGridHeight > 0)
        {
            centerCoordinate = new Vector2Int(gridManager.CurrentGridWidth / 2, gridManager.CurrentGridHeight / 2);
            Debug.Log($"WordPlacementValidator: Center coordinate for rules updated to {centerCoordinate}");
        }
        else
        {
            Debug.LogError("WordPlacementValidator: Could not initialize center coordinate from GridManager.");
        }
    }

    public void ConfirmFirstWordPlaced()
    {
        isFirstWordPlaced = true;
        Debug.Log("WordPlacementValidator: First word placement confirmed state set to true.");
    }

    public void ResetFirstWordState()
    {
        isFirstWordPlaced = false;
        Debug.Log("WordPlacementValidator: First word state reset.");
    }

    public bool IsFirstWordPlaced()
    {
        return isFirstWordPlaced;
    }

    public PlacementResult ValidatePlacement(string word, Vector2Int startCoordinate, bool isHorizontal)
    {
        PlacementResult result = new PlacementResult();
        word = word.ToUpper(); // Ensure consistent casing

        if (gridManager == null)
        {
            result.ErrorMessage = "GridManager not available for validation.";
            Debug.LogError("WordPlacementValidator.ValidatePlacement: GridManager is NULL.");
            return result; // Cannot validate
        }
        if (string.IsNullOrEmpty(word))
        {
            result.ErrorMessage = "Cannot place an empty word.";
            Debug.LogWarning("WordPlacementValidator.ValidatePlacement: Empty word provided.");
            return result;
        }
        // Debug.Log($"WordPlacementValidator.ValidatePlacement: Validating '{word}' at {startCoordinate}, IsHorizontal: {isHorizontal}, IsFirstWordPlaced: {isFirstWordPlaced}");


        bool intersectsWithExistingLetters = false;
        int lettersFromNewWordUsed = 0;

        for (int i = 0; i < word.Length; i++)
        {
            Vector2Int currentCoords = startCoordinate + (isHorizontal ? new Vector2Int(i, 0) : new Vector2Int(0, i));
            result.WordCoordinates.Add(currentCoords);

            TileData tile = gridManager.GetTileData(currentCoords);
            if (tile == null) // Checks if coordinate is off the grid
            {
                result.ErrorMessage = "Word goes off a grid boundary.";
                result.IsValid = false;
                // Debug.Log($"WordPlacementValidator.ValidatePlacement: Invalid - off grid at {currentCoords} for letter '{word[i]}'.");
                return result; // Early exit
            }

            if (tile.IsOccupied)
            {
                if (tile.Letter != word[i]) // If tile has a letter, and it's not the one we're trying to place
                {
                    result.ErrorMessage = $"Cell {currentCoords} is occupied by '{tile.Letter}', cannot place '{word[i]}'.";
                    result.IsValid = false;
                    // Debug.Log($"WordPlacementValidator.ValidatePlacement: Invalid - cell occupied by different letter at {currentCoords}. Expected '{word[i]}', found '{tile.Letter}'.");
                    return result; // Conflict with existing letter
                }
                else
                {
                    // Letter matches an existing letter on the grid - this is an intersection
                    intersectsWithExistingLetters = true;
                    result.IntersectionCoordinates.Add(currentCoords);
                    // This letter is "provided" by the grid, not the new word being placed
                }
            }
            else
            {
                // This is a new letter being placed on an empty tile
                lettersFromNewWordUsed++;
            }
        }

        // Rule: At least one new letter must be placed (word can't entirely consist of existing letters)
        if (lettersFromNewWordUsed == 0 && word.Length > 0)
        {
            result.ErrorMessage = "Word must place at least one new letter.";
            result.IsValid = false;
            // Debug.Log("WordPlacementValidator.ValidatePlacement: Invalid - no new letters placed.");
            return result;
        }


        // Rule: First word placement
        if (!isFirstWordPlaced)
        {
            if (requireFirstWordOnCenter)
            {
                // Check if any *newly placed part* of the word is on the center square.
                // Or if an *intersection point* is on the center square.
                bool touchesCenterSquare = false;
                foreach (Vector2Int wcCoord in result.WordCoordinates)
                {
                    if (wcCoord == centerCoordinate) // Is any part of the word on the center coordinate?
                    {
                        // If this coord is NOT an intersection, it's a new letter on center.
                        // If it IS an intersection, it's an existing letter on center. Both are fine.
                        touchesCenterSquare = true;
                        break;
                    }
                }

                if (!touchesCenterSquare)
                {
                    result.ErrorMessage = $"First word must be placed on the center square ({centerCoordinate}).";
                    result.IsValid = false;
                    // Debug.Log($"WordPlacementValidator.ValidatePlacement: Invalid - first word not on center. Word coords: {string.Join(";", result.WordCoordinates)}");
                    return result;
                }
            }
            // For the first word, intersection isn't strictly required *with other words* (as there are none).
            // The fact that it's placing letters is enough. The "intersectsWithExistingLetters" flag
            // will remain false if it's a standalone first word, which is fine for this block.
        }
        else // Subsequent words
        {
            if (requireConnectionToExisting && !intersectsWithExistingLetters)
            {
                result.ErrorMessage = "Word must connect to or cross existing letters.";
                result.IsValid = false;
                // Debug.Log("WordPlacementValidator.ValidatePlacement: Invalid - subsequent word does not connect to existing letters.");
                return result;
            }
        }

        // If all checks pass
        result.IsValid = true;
        result.ErrorMessage = "Placement is valid.";
        result.Score = CalculateScore(word, result.WordCoordinates, result.IntersectionCoordinates);
        // Debug.Log($"WordPlacementValidator.ValidatePlacement: Valid for '{word}'. Score: {result.Score}. Coords: {string.Join(";", result.WordCoordinates)}");
        return result;
    }

    private int CalculateScore(string word, List<Vector2Int> wordCoordinates, List<Vector2Int> intersectionCoordinates)
    {
        // Basic scoring: 1 point per letter. More advanced would use letter values.
        int score = 0;
        for (int i = 0; i < word.Length; i++)
        {
            // Only score letters that are NOT part of an intersection (i.e., new letters being placed)
            if (!intersectionCoordinates.Contains(wordCoordinates[i]))
            {
                score += GetLetterValue(word[i]); // Assuming GetLetterValue gives points for char
            }
        }
        // TODO: Add scoring for newly formed words (crosswords)
        return score;
    }

    private int GetLetterValue(char letter)
    {
        // Basic Scrabble-like letter values (can be expanded)
        switch (char.ToUpper(letter))
        {
            case 'A':
            case 'E':
            case 'I':
            case 'O':
            case 'U':
            case 'L':
            case 'N':
            case 'S':
            case 'T':
            case 'R':
                return 1;
            case 'D':
            case 'G':
                return 2;
            case 'B':
            case 'C':
            case 'M':
            case 'P':
                return 3;
            case 'F':
            case 'H':
            case 'V':
            case 'W':
            case 'Y':
                return 4;
            case 'K':
                return 5;
            case 'J':
            case 'X':
                return 8;
            case 'Q':
            case 'Z':
                return 10;
            default:
                return 0;
        }
    }
}