using UnityEngine;
using TMPro;

public class LetterCell : MonoBehaviour
{
    [Tooltip("Initial number of moves for this cell")]
    [SerializeField] private int initialMoves = 5;
    private int movesLeft;
    [Tooltip("Enable or disable the move reduction for this cell")]
    [SerializeField] private bool enableMoves = true; // Default to true
    [SerializeField] private TextMeshProUGUI movesTextDisplay; // Optional: To visualize moves

    public int MovesLeft => movesLeft; // Public getter
    public bool EnableMoves => enableMoves; // Public getter for the enabled state

    private void Awake()
    {
        // Initialize moves from the public variable when the cell is created
        movesLeft = initialMoves;
        UpdateMovesDisplay(); // If you have a UI element to show moves
    }

    // Returns true if move was successful, false if no moves left or moves are disabled
    public bool ReduceMove()
    {
        if (enableMoves) // Only reduce moves if enabled
        {
            if (movesLeft > 0)
            {
                movesLeft--;
                UpdateMovesDisplay();
                return true;
            }
            return false; // No moves left
        }
        return true; // Moves are disabled, so consider the "reduction" successful (no change)
    }

    private void UpdateMovesDisplay()
    {
        if (movesTextDisplay != null)
        {
            // --- NEW: Control visibility based on enableMoves ---
            movesTextDisplay.gameObject.SetActive(enableMoves);

            // Only update the text content if moves are enabled and visible
            if (enableMoves)
            {
                movesTextDisplay.text = movesLeft.ToString();
            }
            // You might also want to change the visual appearance (e.g., color) based on moves left
        }
    }

    // Optional: Method to set moves directly (if needed for debugging or other logic)
    public void SetMoves(int newMoves)
    {
        movesLeft = newMoves;
        UpdateMovesDisplay(); // Ensure display updates if moves are set directly
    }

    // Optional: Method to directly enable/disable moves (could be called from other scripts)
    public void SetEnableMoves(bool shouldEnable)
    {
        enableMoves = shouldEnable;
        // --- UPDATED: Call UpdateMovesDisplay after changing the state ---
        UpdateMovesDisplay(); // Ensure the text visibility updates immediately
        // You might want to update other visual appearance based on this state
    }
}