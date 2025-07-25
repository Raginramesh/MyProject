using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameOverUIController : MonoBehaviour
{
    [Header("Traditional Game Over")]
    [SerializeField] private TextMeshProUGUI winLossMessageText; // Assign your TextMeshProUGUI component here in the Inspector
    [SerializeField] private TextMeshProUGUI finalScoreText; // Assign your TextMeshProUGUI for the final score here
    
    [Header("Level System Integration")]
    [SerializeField] private GameObject levelSystemPanel; // Panel shown when using level system
    [SerializeField] private TextMeshProUGUI levelCompleteTitle;
    [SerializeField] private TextMeshProUGUI targetScoreText;
    [SerializeField] private TextMeshProUGUI movesUsedText;
    
    [Header("Star Display")]
    [SerializeField] private GameObject[] starIcons; // 3 star icons
    [SerializeField] private Color starEarnedColor = Color.yellow;
    [SerializeField] private Color starUnearnedColor = Color.gray;
    
    [Header("Navigation")]
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button homeButton;
    [SerializeField] private float autoAdvanceDelay = 3f; // Auto advance to next level after 3 seconds
    
    private bool canAdvance = false;
    private bool isUsingLevelSystem = false;
    
    // Helper property to reduce redundant level manager access
    private LevelManager levelManager => LevelManager.Instance;

    void OnEnable()
    {
        // Check if we're using the level system
        isUsingLevelSystem = GameManager.instance != null && GameManager.instance.IsUsingLevelSystem;
        
        if (isUsingLevelSystem)
        {
            ShowLevelSystemUI();
        }
        else
        {
            ShowTraditionalGameOverUI();
        }
        
        // Setup button listeners
        SetupButtons();
    }
    
    void Start()
    {
        // Hide level system panel initially if it exists
        if (levelSystemPanel != null)
            levelSystemPanel.SetActive(false);
    }
    
    /// <summary>
    /// Show traditional game over UI (non-level system)
    /// </summary>
    private void ShowTraditionalGameOverUI()
    {
        // Hide level system panel
        if (levelSystemPanel != null)
            levelSystemPanel.SetActive(false);
            
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
            // Use the new public property CurrentScore from GameManager
            finalScoreText.text = "Final Score: " + GameManager.instance.CurrentScore.ToString();
        }
    }
    
    /// <summary>
    /// Show level system UI with level complete/failed information
    /// </summary>
    private void ShowLevelSystemUI()
    {
        // Show level system panel
        if (levelSystemPanel != null)
            levelSystemPanel.SetActive(true);
            
        if (levelManager == null)
        {
            Debug.LogError("GameOverUIController: LevelManager instance not found!");
            ShowTraditionalGameOverUI();
            return;
        }
        
        LevelData currentLevel = levelManager.CurrentLevel;
        if (currentLevel == null)
        {
            Debug.LogError("GameOverUIController: No current level found!");
            ShowTraditionalGameOverUI();
            return;
        }
        
        int finalScore = levelManager.CurrentScore;
        int movesUsed = levelManager.CurrentMoves;
        int starsEarned = currentLevel.GetStarRating(finalScore);
        bool levelCompleted = currentLevel.IsLevelCompletedByMoves(movesUsed);
        float scorePercentage = currentLevel.GetScorePercentage(finalScore);
        
        // Update title - level always completes when moves are exhausted
        if (levelCompleteTitle != null)
        {
            levelCompleteTitle.text = $"{currentLevel.LevelName} Complete! ({scorePercentage:F1}%)";
        }
        
        // Update score displays with percentage info
        if (finalScoreText != null)
            finalScoreText.text = $"Score: {finalScore:N0} ({scorePercentage:F1}%)";
        if (targetScoreText != null)
            targetScoreText.text = $"Target: {currentLevel.TargetScore:N0} (for 3⭐)";
        if (movesUsedText != null)
            movesUsedText.text = $"Moves Used: {movesUsed}/{currentLevel.MaxMoves}";
        
        // Update star display
        UpdateStarDisplay(starsEarned);
        
        // Set navigation state
        canAdvance = levelCompleted;
        
        // Auto advance if level completed
        if (levelCompleted && autoAdvanceDelay > 0)
        {
            Invoke(nameof(AutoAdvanceToNextLevel), autoAdvanceDelay);
        }
        
        Debug.Log($"🎮 Game Over UI: Level {(levelCompleted ? "completed" : "failed")} with {starsEarned} stars");
        Debug.Log($"📊 Score: {finalScore} ({scorePercentage:F1}%) | {currentLevel.GetStarThresholdInfo()}");
    }
    
    /// <summary>
    /// Setup button event listeners
    /// </summary>
    private void SetupButtons()
    {
        if (nextLevelButton != null)
            nextLevelButton.onClick.AddListener(GoToNextLevel);
        if (retryButton != null)
            retryButton.onClick.AddListener(RetryLevel);
        if (homeButton != null)
            homeButton.onClick.AddListener(GoToHome);
    }
    
    /// <summary>
    /// Update the star display based on earned stars
    /// </summary>
    private void UpdateStarDisplay(int earnedStars)
    {
        if (starIcons == null) return;
        
        for (int i = 0; i < starIcons.Length && i < 3; i++)
        {
            if (starIcons[i] != null)
            {
                var image = starIcons[i].GetComponent<Image>();
                if (image != null)
                {
                    image.color = i < earnedStars ? starEarnedColor : starUnearnedColor;
                }
            }
        }
    }
    
    /// <summary>
    /// Go to next level
    /// </summary>
    private void GoToNextLevel()
    {
        if (!isUsingLevelSystem || !canAdvance) return;
        
        CancelInvoke(); // Cancel auto advance
        
        if (levelManager != null)
        {
            bool hasNextLevel = levelManager.StartNextLevel();
            if (hasNextLevel)
            {
                // Hide the game over panel and let the game continue
                gameObject.SetActive(false);
            }
            else
            {
                // No more levels - show completion message
                Debug.Log("🏆 All levels completed!");
                if (levelCompleteTitle != null)
                    levelCompleteTitle.text = "All Levels Complete!";
                
                // Could implement a "Game Complete" screen here or go home
                Invoke(nameof(GoToHome), 2f);
            }
        }
    }
    
    /// <summary>
    /// Auto advance to next level (called by Invoke)
    /// </summary>
    private void AutoAdvanceToNextLevel()
    {
        if (canAdvance)
        {
            GoToNextLevel();
        }
    }
    
    /// <summary>
    /// Retry the current level
    /// </summary>
    private void RetryLevel()
    {
        if (isUsingLevelSystem)
        {
            // In level system, retry means restart current level
            if (levelManager != null && levelManager.CurrentLevel != null)
            {
                levelManager.StartLevel(levelManager.CurrentLevel);
                gameObject.SetActive(false);
            }
        }
        else
        {
            // Traditional game restart
            if (GameManager.instance != null)
            {
                GameManager.instance.RestartGame();
                gameObject.SetActive(false);
            }
        }
    }
    
    /// <summary>
    /// Go to home/main menu
    /// </summary>
    private void GoToHome()
    {
        CancelInvoke(); // Cancel auto advance
        
        // Implement your home navigation logic here
        Debug.Log("Going to home screen");
        
        // Example: Load main menu scene
        // SceneManager.LoadScene("MainMenu");
        
        gameObject.SetActive(false);
    }
}