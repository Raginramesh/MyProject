using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelCompleteUI : MonoBehaviour
{
    [Header("Level Complete Panel")]
    [SerializeField] private GameObject levelCompletePanel;
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
    [SerializeField] private Button homeButton;
    [SerializeField] private float autoAdvanceDelay = 3f; // Auto advance to next level after 3 seconds
    
    [Header("Level Failed Panel")]
    [SerializeField] private GameObject levelFailedPanel;
    [SerializeField] private TextMeshProUGUI failedScoreText;
    [SerializeField] private TextMeshProUGUI failedTargetText;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button failedHomeButton;
    
    private bool canAdvance = false;
    
    void OnEnable()
    {
        // Subscribe to level events
        LevelManager.OnLevelCompleted += OnLevelCompleted;
        LevelManager.OnLevelFailed += OnLevelFailed;
    }
    
    void OnDisable()
    {
        // Unsubscribe from level events
        LevelManager.OnLevelCompleted -= OnLevelCompleted;
        LevelManager.OnLevelFailed -= OnLevelFailed;
    }
    
    void Start()
    {
        // Setup button listeners
        if (nextLevelButton != null)
            nextLevelButton.onClick.AddListener(GoToNextLevel);
        if (homeButton != null)
            homeButton.onClick.AddListener(GoToHome);
        if (retryButton != null)
            retryButton.onClick.AddListener(RetryLevel);
        if (failedHomeButton != null)
            failedHomeButton.onClick.AddListener(GoToHome);
        
        // Hide panels initially
        if (levelCompletePanel != null)
            levelCompletePanel.SetActive(false);
        if (levelFailedPanel != null)
            levelFailedPanel.SetActive(false);
    }
    
    private void OnLevelCompleted(LevelData level, int finalScore, int stars)
    {
        if (levelCompletePanel != null)
        {
            levelCompletePanel.SetActive(true);
            
            // Update text displays
            if (levelCompleteTitle != null)
                levelCompleteTitle.text = $"{level.LevelName} Complete!";
            if (finalScoreText != null)
                finalScoreText.text = $"Final Score: {finalScore:N0}";
            if (targetScoreText != null)
                targetScoreText.text = $"Target: {level.TargetScore:N0}";
            if (movesUsedText != null)
                movesUsedText.text = $"Moves Used: {LevelManager.Instance.CurrentMoves}";
            
            // Update star display
            UpdateStarDisplay(stars);
            
            canAdvance = true;
            
            // Auto advance to next level after delay
            if (autoAdvanceDelay > 0)
            {
                Invoke(nameof(AutoAdvanceToNextLevel), autoAdvanceDelay);
            }
        }
        
        Debug.Log($"🌟 Level Complete UI: {level.LevelName} completed with {stars} stars!");
    }
    
    private void OnLevelFailed(LevelData level)
    {
        if (levelFailedPanel != null)
        {
            levelFailedPanel.SetActive(true);
            
            if (failedScoreText != null)
                failedScoreText.text = $"Score: {LevelManager.Instance.CurrentScore:N0}";
            if (failedTargetText != null)
                failedTargetText.text = $"Target: {level.TargetScore:N0}";
        }
        
        Debug.Log($"❌ Level Failed UI: {level.LevelName} failed");
    }
    
    /// <summary>
    /// Update the star display
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
        
        if (LevelManager.Instance != null)
        {
            bool hasNextLevel = LevelManager.Instance.StartNextLevel();
            if (hasNextLevel)
            {
                HidePanels();
            }
            else
            {
                // No more levels - could show "Game Complete" or go to home
                Debug.Log("🏆 All levels completed!");
                GoToHome();
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
        // For simplified system, we don't allow going back to levels
        // Instead, just hide the failed panel and let them continue with next level
        Debug.Log("Retry not allowed in this system - advancing to next level");
        GoToNextLevel();
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
        
        HidePanels();
    }
    
    /// <summary>
    /// Hide all panels
    /// </summary>
    private void HidePanels()
    {
        if (levelCompletePanel != null)
            levelCompletePanel.SetActive(false);
        if (levelFailedPanel != null)
            levelFailedPanel.SetActive(false);
        
        canAdvance = false;
    }
}
