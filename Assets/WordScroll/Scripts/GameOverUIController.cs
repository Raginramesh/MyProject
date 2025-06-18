using UnityEngine;
using TMPro;

public class GameOverUIController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI winLossMessageText; // Assign your TextMeshProUGUI component here in the Inspector
    [SerializeField] private TextMeshProUGUI finalScoreText; // Assign your TextMeshProUGUI for the final score here

    void OnEnable()
    {
        if (winLossMessageText == null)
        {
            Debug.LogError("GameOverUIController: Win/Loss Message TextMeshProUGUI not assigned!");
        }

        if (finalScoreText == null)
        {
            Debug.LogError("GameOverUIController: Final Score TextMeshProUGUI not assigned!");
        }

        if (GameManager.instance == null)
        {
            Debug.LogError("GameOverUIController: GameManager instance not found!");
            if (winLossMessageText != null) winLossMessageText.text = "Game Over"; 
            if (finalScoreText != null) finalScoreText.text = "Score: N/A"; 
            return;
        }

        // Update Win/Loss Message
        if (winLossMessageText != null)
        {
            if (GameManager.instance.HasWon)
            {
                winLossMessageText.text = "You Win!";
            }
            else
            {
                winLossMessageText.text = "You Lose!";
            }
        }

        // Update Final Score Text
        if (finalScoreText != null)
        {
            // Use ScoreManager for final score
            int finalScore = 0;
            if (WordScroll.Managers.ScoreManager.Instance != null)
            {
                finalScore = WordScroll.Managers.ScoreManager.Instance.PlayerScore;
            }
            finalScoreText.text = "Final Score: " + finalScore.ToString();
        }
    }
}