using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public struct PlacementResult
{
    public bool IsValid;
    public int Score;
    public List<Vector2Int> WordCoordinates;
    public string ErrorMessage;
}

public class WordPlacementValidator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;
    // [SerializeField] private ScoreManager scoreManager;
    // [SerializeField] private LetterDataAsset letterScores;

    private bool isFirstWordPlaced = false;

    public void ResetValidator()
    {
        isFirstWordPlaced = false;
    }

    public PlacementResult ValidatePlacement(string word, Vector2Int startCoordinate, bool isHorizontal)
    {
        PlacementResult result = new PlacementResult { IsValid = false, Score = 0, WordCoordinates = new List<Vector2Int>(), ErrorMessage = "Unknown validation error." };

        if (gridManager == null) { result.ErrorMessage = "GridManager reference not set!"; Debug.LogError("WordPlacementValidator: " + result.ErrorMessage); return result; }
        if (string.IsNullOrEmpty(word)) { result.ErrorMessage = "Cannot place an empty word."; return result; }

        word = word.ToUpper();
        bool connectsToExistingLetter = false;
        int currentWordScore = 0;

        for (int i = 0; i < word.Length; i++)
        {
            char letter = word[i];
            Vector2Int currentCoord = startCoordinate + (isHorizontal ? new Vector2Int(i, 0) : new Vector2Int(0, i));

            if (!gridManager.IsValidCoordinate(currentCoord)) { result.ErrorMessage = $"Word goes out of bounds at {currentCoord}."; return result; }

            result.WordCoordinates.Add(currentCoord);
            TileData tileData = gridManager.GetTileData(currentCoord);
            if (tileData == null) { result.ErrorMessage = $"Internal error: No tile data found at valid coordinate {currentCoord}."; return result; }

            // Blocker Check (Add IsBlocker later)
            // if (tileData.IsBlocker) { result.ErrorMessage = $"Placement blocked at {currentCoord}."; return result; }

            // **** Check TileData.IsOccupied ****
            if (tileData.IsOccupied)
            {
                // **** Check TileData.Letter ****
                if (tileData.Letter == letter) { connectsToExistingLetter = true; }
                else { result.ErrorMessage = $"Letter mismatch at {currentCoord}. Board has '{tileData.Letter}', word has '{letter}'."; return result; }
            }
            else // Tile is empty
            {
                if (!isFirstWordPlaced) { connectsToExistingLetter = true; }
                else if (!connectsToExistingLetter) { if (CheckAdjacentConnection(currentCoord)) { connectsToExistingLetter = true; } }

                int letterValue = GetLetterScore(letter);
                currentWordScore += letterValue;
            }
        }

        if (!isFirstWordPlaced && word.Length > 0) { connectsToExistingLetter = true; }
        if (!connectsToExistingLetter) { result.ErrorMessage = "Word must connect to existing letters on the board."; return result; }

        // If all checks passed:
        result.IsValid = true;
        result.Score = currentWordScore;
        result.ErrorMessage = string.Empty;
        Debug.Log($"Validation successful for '{word}' at {startCoordinate}. Score: {result.Score}");
        return result;
    }

    private bool CheckAdjacentConnection(Vector2Int coord)
    {
        Vector2Int[] neighbors = { coord + Vector2Int.up, coord + Vector2Int.down, coord + Vector2Int.left, coord + Vector2Int.right };
        foreach (var neighborCoord in neighbors)
        {
            if (gridManager.IsValidCoordinate(neighborCoord))
            {
                TileData neighborTile = gridManager.GetTileData(neighborCoord);
                // **** Check TileData.IsOccupied ****
                if (neighborTile != null && neighborTile.IsOccupied) { return true; }
            }
        }
        return false;
    }

    private int GetLetterScore(char letter)
    { /* ... score logic ... */
        switch (char.ToUpper(letter))
        {
            case 'A': case 'E': case 'I': case 'O': case 'U': case 'L': case 'N': case 'S': case 'T': case 'R': return 1;
            case 'D': case 'G': return 2;
            case 'B': case 'C': case 'M': case 'P': return 3;
            case 'F': case 'H': case 'V': case 'W': case 'Y': return 4;
            case 'K': return 5;
            case 'J': case 'X': return 8;
            case 'Q': case 'Z': return 10;
            default: return 0;
        }
    }

    public void ConfirmFirstWordPlaced() { isFirstWordPlaced = true; }
}