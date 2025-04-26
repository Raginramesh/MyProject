using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        // Adding a state before Playing helps ensure the first transition works
        Initializing,
        Playing,
        Paused,
        GameOver
    }
    // Initialize to a state other than Playing to ensure the first SetState(Playing) runs fully
    public GameState CurrentState { get; private set; } = GameState.Initializing;

    [Header("Game Mode Settings")]
    [SerializeField] private bool enableTimer = false;
    [SerializeField] private bool enableMoveLimit = false;

    public int Score { get; private set; }

    [Header("Timer Settings (if enabled)")]
    [SerializeField] private float maxTime = 60f;
    private float currentTime;
    private bool timerIsRunning = false;

    [Header("Move Limit Settings (if enabled)")]
    [SerializeField] private int maxMoves = 20;
    private int currentMoves;

    [Header("Scene Names")]
    [SerializeField] private string homeSceneName = "HomeScreen";

    [Header("Core Components")]
    [SerializeField] private WordGridManager wordGridManager;
    [SerializeField] private GridInputHandler gridInputHandler;
    [SerializeField] private WordValidator wordValidator;

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI movesText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI finalStatusText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button homeButton;

    void Start()
    {
        if (!ValidateReferences()) { enabled = false; return; }

        // Setup button listeners
        restartButton.onClick.AddListener(RestartGame);
        homeButton.onClick.AddListener(GoToHome);

        // Initialize references in other scripts
        wordValidator.SetGameManager(this);
        wordGridManager.SetGameManager(this);

        // Explicitly start the game by transitioning to the Playing state
        SetState(GameState.Playing);
    }

    bool ValidateReferences()
    {
        // ... (keep your existing validation code) ...
        bool isValid = true;
        if (wordGridManager == null || gridInputHandler == null || wordValidator == null)
        {
            Debug.LogError("GameManager: Core component references missing!"); isValid = false;
        }
        if (scoreText == null || gameOverPanel == null || finalStatusText == null || restartButton == null || homeButton == null)
        {
            Debug.LogError("GameManager: Core UI references missing (Score, GameOver Panel/Text, Restart, Home buttons)!"); isValid = false;
        }
        if (enableTimer && timerText == null)
        {
            Debug.LogError("GameManager: Timer is enabled, but Timer Text UI is not assigned!"); isValid = false;
        }
        if (enableMoveLimit && movesText == null)
        {
            Debug.LogError("GameManager: Move Limit is enabled, but Moves Text UI is not assigned!"); isValid = false;
        }
        if (string.IsNullOrEmpty(homeSceneName))
        {
            Debug.LogError("GameManager: Home Scene Name is not set in Inspector!"); isValid = false;
        }
        return isValid;
    }

    void Update()
    {
        if (enableTimer && timerIsRunning && CurrentState == GameState.Playing)
        {
            currentTime -= Time.deltaTime;
            UpdateTimerUI();
            if (currentTime <= 0f)
            {
                currentTime = 0f;
                timerIsRunning = false;
                UpdateTimerUI();
                EndGame("Time's Up!");
            }
        }
    }

    void SetState(GameState newState)
    {
        // Allow re-entry into Playing state for restart logic
        // if (CurrentState == newState) return; // Removed this check

        GameState previousState = CurrentState;
        CurrentState = newState;
        Debug.Log($"Game State Changed: {previousState} -> {newState}");

        // --- Configure based on NEW state ---
        timerIsRunning = (newState == GameState.Playing && enableTimer);
        gridInputHandler.enabled = (newState == GameState.Playing);
        gameOverPanel.SetActive(newState == GameState.GameOver);
        // pausePanel.SetActive(newState == GameState.Paused);

        UpdateScoreUI();
        UpdateTimerUI();
        UpdateMovesUI();

        // --- Actions on ENTERING the new state ---
        switch (CurrentState)
        {
            case GameState.Initializing:
                // Should not really enter this state after Start()
                Debug.LogWarning("Entered Initializing state unexpectedly.");
                break;

            case GameState.Playing:
                // --- THIS BLOCK IS KEY ---
                // Always reset score, timer, moves, and grid when entering Playing state
                // (Handles both initial start and restart)
                Debug.Log("Entering Playing State: Resetting game...");
                Score = 0;
                currentTime = maxTime;
                currentMoves = maxMoves;
                UpdateScoreUI(); // Update UI after resetting
                if (enableTimer) UpdateTimerUI();
                if (enableMoveLimit) UpdateMovesUI();

                // Ensure GridManager reference is valid before calling
                if (wordGridManager != null)
                {
                    wordGridManager.InitializeGrid(); // <<< CALL GRID INIT HERE
                }
                else
                {
                    Debug.LogError("Cannot Initialize Grid: WordGridManager reference is missing!");
                    // Maybe force game over if grid can't init?
                    // EndGame("Initialization Error!");
                    break; // Exit case if grid manager is missing
                }

                // Ensure WordValidator reference is valid before calling
                if (wordValidator != null)
                {
                    wordValidator.ValidateWords(); // Initial validation after grid setup
                }
                else
                {
                    Debug.LogError("WordValidator reference is missing! Cannot perform initial validation.");
                }
                break;

            case GameState.Paused:
                // Logic for pausing (timer stops, input disabled - handled above)
                break;

            case GameState.GameOver:
                // Final status text is set in EndGame()
                timerIsRunning = false; // Ensure timer is stopped
                                        // --- MAKE SURE InitializeGrid() IS NOT CALLED HERE ---
                break;
        }
    }

    public void EndGame(string reason = "Game Over!")
    {
        if (CurrentState == GameState.Playing || CurrentState == GameState.Paused) // Allow ending from Playing or Paused
        {
            Debug.Log($"Ending Game: {reason}");
            finalStatusText.text = $"{reason}\nFinal Score: {Score}";
            SetState(GameState.GameOver);
        }
        else { Debug.LogWarning($"EndGame called from non-Playing/Paused state: {CurrentState}. Ignoring."); }
    }

    public void RestartGame()
    {
        // Only allow restart if game is actually over
        if (CurrentState == GameState.GameOver)
        {
            Debug.Log("Restarting Game...");
            SetState(GameState.Playing); // Transition back to Playing, which handles reset
        }
        else { Debug.LogWarning($"RestartGame called from invalid state: {CurrentState}. Ignoring."); }
    }

    public void GoToHome()
    {
        Debug.Log($"Returning to Home Scene: {homeSceneName}");
        SceneManager.LoadScene(homeSceneName);
    }

    public void AddScore(int amount)
    {
        if (CurrentState == GameState.Playing)
        {
            Score += amount;
            UpdateScoreUI();
        }
    }

    public void DecrementMoves()
    {
        if (!enableMoveLimit || CurrentState != GameState.Playing) return;
        currentMoves--;
        UpdateMovesUI();
        if (currentMoves <= 0)
        {
            currentMoves = 0;
            UpdateMovesUI();
            EndGame("Moves Used Up!");
        }
    }

    private void UpdateScoreUI() { /* ... */ }
    private void UpdateTimerUI() { /* ... */ }
    private void UpdateMovesUI() { /* ... */ }

    void OnDestroy()
    {
        if (restartButton != null) restartButton.onClick.RemoveListener(RestartGame);
        if (homeButton != null) homeButton.onClick.RemoveListener(GoToHome);
    }
}