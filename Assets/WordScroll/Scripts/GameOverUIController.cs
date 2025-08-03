using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameOverUIController : MonoBehaviour
{
    [Header("Level System UI")]
    [SerializeField] private GameObject levelSystemPanel; // Panel shown when using level system
    [SerializeField] private TextMeshProUGUI levelCompleteTitle;
    [SerializeField] private TextMeshProUGUI finalScoreText;
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
    
    // Helper property to reduce redundant level manager access
    private LevelManager levelManager => LevelManager.Instance;

    void OnEnable()
    {
        Debug.Log("🎮 GameOverUIController: OnEnable called - Level End UI triggered!");
        
        // Wait for GameManager to be ready if it's not available yet
        if (GameManager.instance == null)
        {
            Debug.Log("🎮 GameOverUIController: GameManager not ready, waiting...");
            StartCoroutine(WaitForGameManagerAndInitialize());
            return;
        }
        
        Debug.Log("🎮 GameOverUIController: GameManager ready, showing level system UI");
        // Always show level system UI (no traditional UI anymore)
        ShowLevelSystemUI();
        SetupButtons();
    }
    
    /// <summary>
    /// Wait for GameManager to be initialized before setting up UI
    /// </summary>
    private System.Collections.IEnumerator WaitForGameManagerAndInitialize()
    {
        int attempts = 0;
        const int maxAttempts = 30; // Wait up to 3 seconds (30 frames at 60fps)
        
        while (GameManager.instance == null && attempts < maxAttempts)
        {
            attempts++;
            yield return new WaitForEndOfFrame();
        }
        
        if (GameManager.instance == null)
        {
            Debug.LogError("GameOverUIController: GameManager instance still not found after waiting. Using fallback behavior.");
            ShowFallbackUI();
        }
        else
        {
            Debug.Log("GameOverUIController: GameManager found. Showing level system UI.");
            ShowLevelSystemUI();
            SetupButtons();
        }
    }
    
    /// <summary>
    /// Show fallback UI when GameManager is not available
    /// </summary>
    private void ShowFallbackUI()
    {
        // Ensure level system panel is visible
        if (levelSystemPanel != null)
            levelSystemPanel.SetActive(true);
            
        // Show basic level completion message (assume win for fallback)
        if (levelCompleteTitle != null)
            levelCompleteTitle.text = "Level Complete";
        if (finalScoreText != null)
            finalScoreText.text = "Score: N/A";
            
        // Configure buttons for win state (fallback)
        ConfigureButtons(true);
        
        // Setup basic button listeners
        SetupButtons();
    }
    
    void Start()
    {
        // Ensure level system panel is visible initially
        if (levelSystemPanel != null)
            levelSystemPanel.SetActive(true);
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
            Debug.LogError("GameOverUIController: LevelManager instance not found! Showing fallback UI.");
            ShowBasicLevelCompleteUI();
            return;
        }
        
        LevelData currentLevel = levelManager.CurrentLevel;
        if (currentLevel == null)
        {
            Debug.LogError("GameOverUIController: No current level found! Showing fallback UI.");
            ShowBasicLevelCompleteUI();
            return;
        }
        
        int finalScore = levelManager.CurrentScore;
        int movesUsed = levelManager.CurrentMoves;
        int starsEarned = currentLevel.GetStarRating(finalScore);
        bool levelCompleted = currentLevel.IsLevelCompletedByMoves(movesUsed);
        float scorePercentage = currentLevel.GetScorePercentage(finalScore);
        
        // Check if player won (from GameManager)
        bool playerWon = GameManager.instance != null ? GameManager.instance.HasWon : levelCompleted;
        
        Debug.Log($"🎮 Level End State: PlayerWon={playerWon}, LevelCompleted={levelCompleted}, GameManager.HasWon={GameManager.instance?.HasWon}, Stars={starsEarned}");
        
        // Update title based on win/loss
        if (levelCompleteTitle != null)
        {
            if (playerWon)
            {
                levelCompleteTitle.text = $"{currentLevel.LevelName} Complete! ({scorePercentage:F1}%)";
            }
            else
            {
                levelCompleteTitle.text = $"{currentLevel.LevelName} Failed";
            }
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
        
        // Set navigation state and button visibility
        canAdvance = playerWon;
        ConfigureButtons(playerWon);
        
        // Auto advance if level completed and player won
        if (playerWon && autoAdvanceDelay > 0)
        {
            Invoke(nameof(AutoAdvanceToNextLevel), autoAdvanceDelay);
        }
        
        Debug.Log($"🎮 Game Over UI: Level {(playerWon ? "won" : "lost")} with {starsEarned} stars");
        Debug.Log($"📊 Score: {finalScore} ({scorePercentage:F1}%) | {currentLevel.GetStarThresholdInfo()}");
    }
    
    /// <summary>
    /// Show basic level complete UI when level data is not available
    /// </summary>
    private void ShowBasicLevelCompleteUI()
    {
        // Check if player won (fallback)
        bool playerWon = GameManager.instance != null ? GameManager.instance.HasWon : true;
        
        if (levelCompleteTitle != null)
            levelCompleteTitle.text = playerWon ? "Level Complete!" : "Level Failed";
        if (finalScoreText != null)
            finalScoreText.text = GameManager.instance != null ? $"Score: {GameManager.instance.CurrentScore}" : "Score: N/A";
        if (targetScoreText != null)
            targetScoreText.text = "Target: N/A";
        if (movesUsedText != null)
            movesUsedText.text = "Moves: N/A";
            
        // Show 1 star as fallback
        UpdateStarDisplay(playerWon ? 1 : 0);
        canAdvance = playerWon;
        ConfigureButtons(playerWon);
    }
    
    /// <summary>
    /// Configure button visibility based on win/loss state
    /// </summary>
    private void ConfigureButtons(bool playerWon)
    {
        // Next Level Button: Show only if player won
        if (nextLevelButton != null)
        {
            nextLevelButton.gameObject.SetActive(playerWon);
            Debug.Log($"🎮 Next Level Button: {(playerWon ? "Shown" : "Hidden")}");
        }
        
        // Retry Button: Show only if player lost
        if (retryButton != null)
        {
            retryButton.gameObject.SetActive(!playerWon);
            Debug.Log($"🎮 Retry Button: {(!playerWon ? "Shown" : "Hidden")}");
        }
        
        // Home Button: Always show
        if (homeButton != null)
        {
            homeButton.gameObject.SetActive(true);
            Debug.Log("🎮 Home Button: Always shown");
        }
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
        if (!canAdvance) return;
        
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
        // Always use level system retry since we removed traditional mode
        if (levelManager != null && levelManager.CurrentLevel != null)
        {
            levelManager.StartLevel(levelManager.CurrentLevel);
            gameObject.SetActive(false);
        }
        else if (GameManager.instance != null)
        {
            // Fallback: restart the game if level manager is not available
            GameManager.instance.RestartGame();
            gameObject.SetActive(false);
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