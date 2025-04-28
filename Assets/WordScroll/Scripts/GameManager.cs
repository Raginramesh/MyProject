using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic; // Needed for Dictionary

public class GameManager : MonoBehaviour
{
    // --- NEW: Enum for Scoring Mode ---
    public enum ScoringMode
    {
        LengthBased, // Original system: score = word.Length * pointsPerLetter
        ScrabbleBased // New system: score = sum of letter values
    }

    public enum GameState { Initializing, Playing, Paused, GameOver }

    [Header("Game State")]
    [SerializeField] private GameState currentState = GameState.Initializing;
    public GameState CurrentState => currentState;

    [Header("Game Settings")]
    [SerializeField] private float gameTimeLimit = 120f;
    [SerializeField] private int startingMoves = 50;

    [Header("Scoring")]
    // --- NEW: Inspector setting to choose scoring mode ---
    [SerializeField] private ScoringMode currentScoringMode = ScoringMode.LengthBased;
    [Tooltip("Points per letter (Used only for LengthBased scoring)")]
    [SerializeField] private int pointsPerLetter = 10; // Kept for LengthBased mode

    // --- NEW: Dictionary for Scrabble letter values ---
    private Dictionary<char, int> scrabbleLetterValues;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI movesText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject pausePanel;

    [Header("Component References")]
    [SerializeField] private WordGridManager wordGridManager;
    [SerializeField] private WordValidator wordValidator;
    [SerializeField] private GridInputHandler gridInputHandler;

    // Internal State
    private float currentTimeRemaining;
    private int currentMovesRemaining;
    private int currentScore = 0;

    // --- Unity Lifecycle Methods ---

    void Awake()
    {
        // --- NEW: Initialize Scrabble values ---
        InitializeScrabbleValues();

        // ... (rest of existing Awake logic for finding references) ...
        if (wordGridManager == null) wordGridManager = FindFirstObjectByType<WordGridManager>();
        if (wordValidator == null) wordValidator = FindFirstObjectByType<WordValidator>();
        if (gridInputHandler == null) gridInputHandler = FindFirstObjectByType<GridInputHandler>();

        // Error checking for critical components
        if (wordGridManager == null) Debug.LogError("GameManager: WordGridManager not found!", this);
        if (wordValidator == null) Debug.LogError("GameManager: WordValidator not found!", this);
        if (gridInputHandler == null) Debug.LogError("GameManager: GridInputHandler not found!", this);

        // Ensure UI panels are initially hidden (or set active state based on design)
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);

        Debug.Log($"GameManager Awake: Initializing references. Scoring Mode: {currentScoringMode}", this);
    }

    // --- NEW: Method to set up Scrabble points ---
    void InitializeScrabbleValues()
    {
        scrabbleLetterValues = new Dictionary<char, int>()
        {
            {'A', 1}, {'E', 1}, {'I', 1}, {'O', 1}, {'U', 1}, {'L', 1}, {'N', 1}, {'S', 1}, {'T', 1}, {'R', 1},
            {'D', 2}, {'G', 2},
            {'B', 3}, {'C', 3}, {'M', 3}, {'P', 3},
            {'F', 4}, {'H', 4}, {'V', 4}, {'W', 4}, {'Y', 4},
            {'K', 5},
            {'J', 8}, {'X', 8},
            {'Q', 10}, {'Z', 10}
        };
        Debug.Log("Initialized Scrabble letter values.");
    }


    void Start()
    {
        // Set initial state and start the game setup
        SetState(GameState.Initializing);
        StartGame();
    }

    void Update()
    {
        // Only update timer if playing
        if (currentState == GameState.Playing)
        {
            UpdateTimer();
        }
    }

    // --- State Management ---
    // ... (Keep existing SetState method) ...
    private void SetState(GameState newState)
    {
        if (currentState == newState) return; // No change

        currentState = newState;
        Debug.Log($"GameManager: State changed to {currentState}");

        // Handle state transitions
        switch (currentState)
        {
            case GameState.Initializing:
                Time.timeScale = 1f;
                break;
            case GameState.Playing:
                Time.timeScale = 1f;
                if (gridInputHandler != null) gridInputHandler.enabled = true;
                if (pausePanel != null) pausePanel.SetActive(false);
                break;
            case GameState.Paused:
                Time.timeScale = 0f;
                if (gridInputHandler != null) gridInputHandler.enabled = false;
                if (pausePanel != null) pausePanel.SetActive(true);
                break;
            case GameState.GameOver:
                Time.timeScale = 1f;
                if (gridInputHandler != null) gridInputHandler.enabled = false;
                break;
        }
    }


    private void StartGame()
    {
        Debug.Log("GameManager: Starting New Game Setup...");
        SetState(GameState.Initializing); // Start in initializing state

        // Reset Score
        currentScore = 0;
        UpdateScoreUI(); // Update UI at the start

        // Reset Timer
        currentTimeRemaining = gameTimeLimit;
        UpdateTimerUI();

        // Reset Moves
        currentMovesRemaining = startingMoves;
        UpdateMovesUI();

        // Reset/Initialize Grid
        if (wordGridManager != null)
        {
            wordGridManager.InitializeGrid();
        }
        else { Debug.LogError("Cannot initialize grid - WordGridManager reference missing!", this); return; }

        // Reset Validator (found words list) and pass reference
        if (wordValidator != null)
        {
            wordValidator.ResetFoundWordsList();
            wordValidator.SetGameManager(this); // Pass reference to WordValidator
                                                // Initial validation after grid setup
            wordValidator.ValidateWords();
        }
        else { Debug.LogError("Cannot reset validator - WordValidator reference missing!", this); }


        // Enable Input Handler
        if (gridInputHandler != null)
        {
            gridInputHandler.enabled = true;
        }
        else { Debug.LogError("Cannot enable input - GridInputHandler reference missing!", this); }

        // Hide UI Panels
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);

        // Transition to Playing state
        SetState(GameState.Playing);
        Debug.Log("GameManager: New Game Started. State set to Playing.", this);
    }

    private void EndGame(bool timeout = false, bool noMoves = false)
    {
        if (currentState == GameState.GameOver) return; // Prevent multiple calls

        string reason = timeout ? "Time Ran Out" : (noMoves ? "No Moves Left" : "Manual Trigger");
        Debug.Log($"Game Over! Reason: {reason}");
        SetState(GameState.GameOver);

        // Show Game Over screen
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        else { Debug.LogWarning("GameManager: GameOver Panel reference not set!", this); }
    }

    // --- MODIFIED: Score Handling ---
    public void AddScoreForWord(string word)
    {
        if (currentState != GameState.Playing) return;
        if (string.IsNullOrEmpty(word)) return;

        int scoreToAdd = 0;

        // --- Calculate score based on selected mode ---
        switch (currentScoringMode)
        {
            case ScoringMode.LengthBased:
                scoreToAdd = word.Length * pointsPerLetter;
                // Debug.Log($"Scoring (LengthBased): Word '{word}', Length {word.Length}, Score {scoreToAdd}");
                break;

            case ScoringMode.ScrabbleBased:
                scoreToAdd = 0; // Start score at 0 for this word
                foreach (char letter in word)
                {
                    // Ensure dictionary lookup uses uppercase
                    char upperLetter = char.ToUpperInvariant(letter);
                    if (scrabbleLetterValues.TryGetValue(upperLetter, out int letterValue))
                    {
                        scoreToAdd += letterValue;
                    }
                    else
                    {
                        // Optional: Handle unexpected characters (e.g., punctuation if it somehow gets in)
                        // Debug.LogWarning($"Scrabble scoring: Character '{upperLetter}' not found in value dictionary.");
                    }
                }
                // Debug.Log($"Scoring (ScrabbleBased): Word '{word}', Score {scoreToAdd}");
                break;

            default:
                Debug.LogError($"Unknown Scoring Mode: {currentScoringMode}");
                break;
        }

        // --- Add calculated score and update UI ---
        if (scoreToAdd > 0)
        {
            currentScore += scoreToAdd;
            Debug.Log($"Added {scoreToAdd} points (Mode: {currentScoringMode}) for word '{word}'. New Total Score: {currentScore}");
            UpdateScoreUI();
        }
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + currentScore.ToString();
        }
    }

    // --- Timer Handling ---
    // ... (Keep existing UpdateTimer and UpdateTimerUI methods) ...
    private void UpdateTimer()
    {
        if (currentState == GameState.Playing)
        {
            if (currentTimeRemaining > 0)
            {
                currentTimeRemaining -= Time.deltaTime;
                UpdateTimerUI();

                if (currentTimeRemaining <= 0)
                {
                    currentTimeRemaining = 0; // Clamp to zero
                    UpdateTimerUI(); // Update UI one last time
                    EndGame(timeout: true); // End game due to time running out
                }
            }
        }
    }

    private void UpdateTimerUI()
    {
        if (timerText != null)
        {
            // Format as minutes:seconds
            int minutes = Mathf.FloorToInt(currentTimeRemaining / 60);
            int seconds = Mathf.FloorToInt(currentTimeRemaining % 60);
            // --- MODIFIED LINE: Removed "Time: " prefix ---
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }


    // --- Moves Handling ---
    // ... (Keep existing DecrementMoves and UpdateMovesUI methods) ...
    public void DecrementMoves() // Made public if called externally, though usually internal
    {
        if (currentState != GameState.Playing) return; // Only decrement while playing

        currentMovesRemaining--;
        UpdateMovesUI();

        if (currentMovesRemaining <= 0)
        {
            currentMovesRemaining = 0; // Clamp
            UpdateMovesUI(); // Update UI one last time
            EndGame(noMoves: true); // End game due to running out of moves
        }
    }

    private void UpdateMovesUI()
    {
        if (movesText != null)
        {
            movesText.text = "Moves: " + currentMovesRemaining.ToString();
        }
    }


    // --- UI Button Actions ---
    // ... (Keep existing RestartGame, PauseGame, ResumeGame, QuitGame methods) ...
    public void RestartGame()
    {
        Debug.Log("Restarting Game...");
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void PauseGame()
    {
        if (currentState == GameState.Playing)
        {
            SetState(GameState.Paused);
        }
    }

    public void ResumeGame()
    {
        if (currentState == GameState.Paused)
        {
            SetState(GameState.Playing);
        }
    }

    public void QuitGame() // Example Quit button action
    {
        Debug.Log("Quitting Game...");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }


} // End of GameManager class