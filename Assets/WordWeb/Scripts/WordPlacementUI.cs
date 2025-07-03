using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;

/// <summary>
/// UI Manager for the Word Placement Game.
/// Handles all UI elements, animations, and user interactions.
/// </summary>
public class WordPlacementUI : MonoBehaviour
{
    [Header("HUD Elements")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI wordsPlacedText;
    [SerializeField] private TextMeshProUGUI targetScoreText;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Image timerFillImage;

    [Header("Game Buttons")]
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button hintsButton;

    [Header("Menu Panels")]
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private GameObject gameOverMenu;
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Pause Menu")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button pauseRestartButton;
    [SerializeField] private Button pauseQuitButton;

    [Header("Game Over Menu")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;
    [SerializeField] private TextMeshProUGUI finalScoreText;
    [SerializeField] private TextMeshProUGUI finalWordsText;
    [SerializeField] private TextMeshProUGUI highScoreText;
    [SerializeField] private Button playAgainButton;
    [SerializeField] private Button gameOverQuitButton;

    [Header("Feedback Elements")]
    [SerializeField] private GameObject timeWarningPanel;
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private Image flashOverlay;

    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private Ease animationEase = Ease.OutQuart;
    [SerializeField] private float scoreUpdateDuration = 1f;

    [Header("Color Settings")]
    [SerializeField] private Color normalTimerColor = Color.green;
    [SerializeField] private Color warningTimerColor = Color.yellow;
    [SerializeField] private Color criticalTimerColor = Color.red;
    [SerializeField] private Color flashColor = Color.white;

    // Animation tracking
    private Tweener scoreAnimationTweener;
    private Tweener timerAnimationTweener;
    private int displayedScore = 0;
    private float displayedTime = 0f;

    // State
    private bool isTimeWarningActive = false;
    private GameState currentGameState = GameState.NotStarted;

    // Events
    public System.Action OnPauseClicked;
    public System.Action OnResumeClicked;
    public System.Action OnRestartClicked;
    public System.Action OnQuitClicked;
    public System.Action OnHintsClicked;

    #region Initialization

    void Awake()
    {
        ValidateReferences();
        SetupButtonListeners();
        HideAllMenus();
    }

    void Start()
    {
        InitializeUI();
    }

    private void ValidateReferences()
    {
        // Validate critical UI elements
        if (scoreText == null) Debug.LogWarning("WordPlacementUI: Score text not assigned!");
        if (timerText == null) Debug.LogWarning("WordPlacementUI: Timer text not assigned!");
        if (pauseButton == null) Debug.LogWarning("WordPlacementUI: Pause button not assigned!");
    }

    private void SetupButtonListeners()
    {
        // Game buttons
        pauseButton?.onClick.AddListener(() => OnPauseClicked?.Invoke());
        restartButton?.onClick.AddListener(() => OnRestartClicked?.Invoke());
        quitButton?.onClick.AddListener(() => OnQuitClicked?.Invoke());
        hintsButton?.onClick.AddListener(() => OnHintsClicked?.Invoke());

        // Pause menu buttons
        resumeButton?.onClick.AddListener(() => OnResumeClicked?.Invoke());
        pauseRestartButton?.onClick.AddListener(() => OnRestartClicked?.Invoke());
        pauseQuitButton?.onClick.AddListener(() => OnQuitClicked?.Invoke());

        // Game over buttons
        playAgainButton?.onClick.AddListener(() => OnRestartClicked?.Invoke());
        gameOverQuitButton?.onClick.AddListener(() => OnQuitClicked?.Invoke());
    }

    private void InitializeUI()
    {
        // Initialize display values
        UpdateScore(0);
        UpdateTime(300f); // Default 5 minutes
        UpdateWordsPlaced(0);
        UpdateTargetScore(500);
        
        // Hide feedback elements
        HideTimeWarning();
        HideFeedback();
        
        // Setup progress slider
        if (progressSlider != null)
        {
            progressSlider.value = 0f;
        }
    }

    #endregion

    #region Public UI Updates

    public void UpdateScore(int newScore)
    {
        if (scoreText == null) return;

        // Animate score change
        if (scoreAnimationTweener != null)
        {
            scoreAnimationTweener.Kill();
        }

        scoreAnimationTweener = DOTween.To(
            () => displayedScore,
            x => {
                displayedScore = x;
                scoreText.text = $"Score: {displayedScore:N0}";
            },
            newScore,
            scoreUpdateDuration
        ).SetEase(Ease.OutQuart);

        // Animate text color for positive changes
        if (newScore > displayedScore)
        {
            scoreText.DOColor(Color.green, 0.2f)
                .OnComplete(() => scoreText.DOColor(Color.white, 0.2f));
        }
    }

    public void UpdateTime(float timeRemaining)
    {
        if (timerText == null) return;

        displayedTime = timeRemaining;
        
        // Format time display
        int minutes = Mathf.FloorToInt(timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(timeRemaining % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";

        // Update timer color based on remaining time
        Color timerColor = GetTimerColor(timeRemaining);
        if (timerFillImage != null)
        {
            timerFillImage.color = timerColor;
        }
        timerText.color = timerColor;

        // Animate timer if critical
        if (timeRemaining <= 30f && timeRemaining > 0f)
        {
            AnimateCriticalTimer();
        }
    }

    public void UpdateWordsPlaced(int wordsPlaced)
    {
        if (wordsPlacedText != null)
        {
            wordsPlacedText.text = $"Words: {wordsPlaced}";
        }
    }

    public void UpdateTargetScore(int targetScore)
    {
        if (targetScoreText != null)
        {
            targetScoreText.text = $"Target: {targetScore:N0}";
        }
    }

    public void UpdateMinWords(int minWords)
    {
        // Could add a separate UI element for minimum words
    }

    public void UpdateProgress(float progress)
    {
        if (progressSlider != null)
        {
            progressSlider.DOValue(progress, animationDuration)
                .SetEase(animationEase);
        }
    }

    #endregion

    #region Game State Updates

    public void UpdateGameState(GameState newState)
    {
        currentGameState = newState;

        switch (newState)
        {
            case GameState.Ready:
                ShowGameReadyUI();
                break;
            case GameState.Playing:
                ShowGamePlayingUI();
                break;
            case GameState.Paused:
                ShowPauseMenu();
                break;
            case GameState.GameOver:
                // Handle in ShowWinScreen/ShowLoseScreen
                break;
        }
    }

    private void ShowGameReadyUI()
    {
        HideAllMenus();
        EnableGameButtons(true);
    }

    private void ShowGamePlayingUI()
    {
        HideAllMenus();
        EnableGameButtons(true);
    }

    #endregion

    #region Menu Management

    public void ShowPauseMenu()
    {
        if (pauseMenu != null)
        {
            pauseMenu.SetActive(true);
            AnimateMenuIn(pauseMenu);
        }
        
        EnableGameButtons(false);
    }

    public void HidePauseMenu()
    {
        if (pauseMenu != null)
        {
            AnimateMenuOut(pauseMenu, () => pauseMenu.SetActive(false));
        }
        
        EnableGameButtons(true);
    }

    public void ShowWinScreen(int finalScore, int wordsPlaced)
    {
        ShowGameOverScreen(true, finalScore, wordsPlaced);
    }

    public void ShowLoseScreen(int finalScore, int wordsPlaced)
    {
        ShowGameOverScreen(false, finalScore, wordsPlaced);
    }

    private void ShowGameOverScreen(bool won, int finalScore, int wordsPlaced)
    {
        if (gameOverMenu != null)
        {
            gameOverMenu.SetActive(true);
        }

        // Show appropriate panel
        if (winPanel != null && losePanel != null)
        {
            winPanel.SetActive(won);
            losePanel.SetActive(!won);
        }

        // Update final score display
        if (finalScoreText != null)
        {
            finalScoreText.text = $"Final Score: {finalScore:N0}";
        }

        if (finalWordsText != null)
        {
            finalWordsText.text = $"Words Placed: {wordsPlaced}";
        }

        // Show high score
        if (highScoreText != null)
        {
            int highScore = PlayerPrefs.GetInt("WordPlacement_HighScore", 0);
            highScoreText.text = $"Best: {highScore:N0}";
        }

        // Animate menu in
        if (gameOverMenu != null)
        {
            AnimateMenuIn(gameOverMenu);
        }

        EnableGameButtons(false);
    }

    public void ShowTutorial()
    {
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(true);
            AnimateMenuIn(tutorialPanel);
        }
    }

    public void HideTutorial()
    {
        if (tutorialPanel != null)
        {
            AnimateMenuOut(tutorialPanel, () => tutorialPanel.SetActive(false));
        }
    }

    private void HideAllMenus()
    {
        pauseMenu?.SetActive(false);
        gameOverMenu?.SetActive(false);
        tutorialPanel?.SetActive(false);
        settingsPanel?.SetActive(false);
    }

    #endregion

    #region Feedback System

    public void ShowTimeWarning()
    {
        if (isTimeWarningActive) return;

        isTimeWarningActive = true;
        
        if (timeWarningPanel != null)
        {
            timeWarningPanel.SetActive(true);
            
            // Flash animation
            CanvasGroup canvasGroup = timeWarningPanel.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.DOFade(1f, 0.3f)
                    .SetLoops(-1, LoopType.Yoyo);
            }
        }
    }

    public void HideTimeWarning()
    {
        isTimeWarningActive = false;
        
        if (timeWarningPanel != null)
        {
            CanvasGroup canvasGroup = timeWarningPanel.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.DOKill();
            }
            
            timeWarningPanel.SetActive(false);
        }
    }

    public void ShowFeedback(string message, float duration = 2f)
    {
        if (feedbackText != null)
        {
            feedbackText.text = message;
            feedbackText.gameObject.SetActive(true);
            
            // Animate text
            feedbackText.transform.localScale = Vector3.zero;
            feedbackText.transform.DOScale(Vector3.one, 0.3f)
                .SetEase(Ease.OutBack)
                .OnComplete(() => {
                    DOVirtual.DelayedCall(duration, () => {
                        feedbackText.transform.DOScale(Vector3.zero, 0.3f)
                            .SetEase(Ease.InBack)
                            .OnComplete(() => feedbackText.gameObject.SetActive(false));
                    });
                });
        }
    }

    public void HideFeedback()
    {
        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
        }
    }

    public void ShowFlashEffect(Color color)
    {
        if (flashOverlay != null)
        {
            flashOverlay.color = color;
            flashOverlay.gameObject.SetActive(true);
            
            CanvasGroup canvasGroup = flashOverlay.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0.8f;
                canvasGroup.DOFade(0f, 0.5f)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() => flashOverlay.gameObject.SetActive(false));
            }
        }
    }

    #endregion

    #region Animation Helpers

    private void AnimateMenuIn(GameObject menu)
    {
        if (menu == null) return;

        RectTransform rectTransform = menu.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = menu.GetComponent<CanvasGroup>();

        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.zero;
            rectTransform.DOScale(Vector3.one, animationDuration)
                .SetEase(animationEase);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, animationDuration);
        }
    }

    private void AnimateMenuOut(GameObject menu, System.Action onComplete = null)
    {
        if (menu == null) return;

        RectTransform rectTransform = menu.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = menu.GetComponent<CanvasGroup>();

        if (rectTransform != null)
        {
            rectTransform.DOScale(Vector3.zero, animationDuration)
                .SetEase(Ease.InBack)
                .OnComplete(() => onComplete?.Invoke());
        }

        if (canvasGroup != null)
        {
            canvasGroup.DOFade(0f, animationDuration);
        }
    }

    private Color GetTimerColor(float timeRemaining)
    {
        if (timeRemaining <= 10f)
        {
            return criticalTimerColor;
        }
        else if (timeRemaining <= 30f)
        {
            return warningTimerColor;
        }
        else
        {
            return normalTimerColor;
        }
    }

    private void AnimateCriticalTimer()
    {
        if (timerAnimationTweener != null && timerAnimationTweener.IsActive())
        {
            return; // Already animating
        }

        if (timerText != null)
        {
            timerAnimationTweener = timerText.transform.DOScale(Vector3.one * 1.2f, 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }
    }

    #endregion

    #region Utility

    private void EnableGameButtons(bool enabled)
    {
        pauseButton?.gameObject.SetActive(enabled);
        restartButton?.gameObject.SetActive(enabled);
        quitButton?.gameObject.SetActive(enabled);
        hintsButton?.gameObject.SetActive(enabled);
    }

    #endregion

    #region Cleanup

    void OnDestroy()
    {
        // Kill all tweeners to prevent errors
        scoreAnimationTweener?.Kill();
        timerAnimationTweener?.Kill();
        
        DOTween.Kill(transform);
    }

    #endregion
}
