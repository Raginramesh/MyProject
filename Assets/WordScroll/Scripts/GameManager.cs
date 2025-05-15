using UnityEngine;
using TMPro; // Required for TextMeshPro UI elements
using UnityEngine.SceneManagement; // Required for scene reloading
using System.Collections.Generic; // Needed for Dictionary, List
using DG.Tweening; // Required for DOTween animations (DelayedCall, DOShakePosition)
using System.Linq; // Required for LINQ Select

public class GameManager : MonoBehaviour
{
    // --- Enums ---
    public enum ScoringMode { LengthBased, ScrabbleBased }
    public enum GameState { Initializing, Playing, Paused, GameOver }
    // This DisplayMode enum will be used by GridInputHandler to check for "Moves" mode
    public enum DisplayMode { Timer, Moves, None }

    [Header("Game State")]
    [SerializeField] private GameState currentState = GameState.Initializing;
    /// <summary>Gets the current high-level state of the game.</summary>
    public GameState CurrentStatePublic => currentState; // Renamed to avoid conflict if 'CurrentState' is used differently

    /// <summary>Gets whether any major game animation (grid scroll, effects, OR INERTIA) is currently playing.</summary>
    // <<< MODIFIED: Added GridInputHandler inertia check >>>
    public bool IsAnyAnimationPlaying => 
        (wordGridManager != null && wordGridManager.isAnimating) || 
        (effectsManager != null && effectsManager.IsAnimating) ||
        (gridInputHandler != null && gridInputHandler.IsPerformingInertiaScroll);

    [Header("Game Mode & Display")]
    [SerializeField] private DisplayMode currentDisplayMode = DisplayMode.Timer;
    // <<< NEW: Public property for GridInputHandler to access the game mode >>>
    public DisplayMode CurrentGameDisplayMode => currentDisplayMode; 
    [SerializeField] private float gameTimeLimit = 120f;
    [SerializeField] private int startingMoves = 50;

    [Header("Scene Navigation")]
    [SerializeField] private string homeSceneName = "HomeScreen";

    [Header("Scoring")]
    [SerializeField] private ScoringMode currentScoringMode = ScoringMode.LengthBased;
    [Tooltip("Points per letter, ONLY used if Scoring Mode is LengthBased.")]
    [SerializeField] private int pointsPerLetter = 10;
    private Dictionary<char, int> scrabbleLetterValues; // Populated in Awake

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [Tooltip("RectTransform of the Score Text, used for shake effect.")]
    [SerializeField] private RectTransform scoreTextRectTransform;
    [SerializeField] private GameObject statusDisplayGroup; // Parent for Timer/Moves text
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI movesText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject pausePanel;

    [Header("Component References")]
    [SerializeField] private WordGridManager wordGridManager;
    [SerializeField] private WordValidator wordValidator;
    [SerializeField] private GridInputHandler gridInputHandler; // Already present, good!
    [SerializeField] private EffectsManager effectsManager;

    [Header("Timing")]
    [Tooltip("Delay after the fly-to-score effect STARTS before letters are replaced in the grid.")]
    [SerializeField] private float replacementDelayAfterEffectStart = 0.4f;

    [Header("Effects")]
    [Tooltip("Duration of the score text shake effect when score increases.")]
    [SerializeField] private float scoreShakeDuration = 0.2f;
    [Tooltip("Strength of the score text shake effect (pixels).")]
    [SerializeField] private float scoreShakeStrength = 3f;
    [Tooltip("Vibrato indicates how much the shake will vibrate.")]
    [SerializeField] private int scoreShakeVibrato = 15;

    // Internal State
    private float currentTimeRemaining;
    private int currentMovesRemaining;
    private int currentScore = 0;
    private static GameManager instance; // Basic singleton pattern

    // --- Unity Lifecycle Methods ---
    void Awake()
    {
        // Singleton setup
        if (instance == null)
        {
            instance = this;
            // Optional: Keep the GameManager alive across scene loads
            // DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            // If another instance exists, destroy this one
            Destroy(gameObject);
            return;
        }

        InitializeScrabbleValues(); // Setup letter scores

        // Attempt to find references if not set in Inspector
        if (wordGridManager == null) wordGridManager = FindFirstObjectByType<WordGridManager>();
        if (wordValidator == null) wordValidator = FindFirstObjectByType<WordValidator>();
        if (gridInputHandler == null) gridInputHandler = FindFirstObjectByType<GridInputHandler>();
        if (effectsManager == null) effectsManager = FindFirstObjectByType<EffectsManager>();
        // Automatically get RectTransform from scoreText if not assigned separately
        if (scoreTextRectTransform == null && scoreText != null)
        {
            scoreTextRectTransform = scoreText.GetComponent<RectTransform>();
        }

        // --- Validate Critical References ---
        if (wordGridManager == null) Debug.LogError("GM: WordGridManager missing! Assign in Inspector or ensure it exists in the scene.", this);
        if (wordValidator == null) Debug.LogError("GM: WordValidator missing! Assign in Inspector or ensure it exists in the scene.", this);
        if (gridInputHandler == null) Debug.LogError("GM: GridInputHandler missing! Assign in Inspector or ensure it exists in the scene.", this); // Crucial for inertia check
        if (effectsManager == null) Debug.LogError("GM: EffectsManager missing! Assign in Inspector or ensure it exists in the scene.", this);
        if (scoreText == null) Debug.LogError("GM: Score Text (TMP) missing! Assign in Inspector.", this);
        if (scoreTextRectTransform == null) Debug.LogError("GM: Score Text RectTransform missing! Needed for shake effect. Assign in Inspector or ensure Score Text has one.", this);
        
        // Ensure UI panels are initially hidden
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    // Populates the Scrabble score dictionary
    void InitializeScrabbleValues()
    {
        scrabbleLetterValues = new Dictionary<char, int>() {
            {'A', 1}, {'B', 3}, {'C', 3}, {'D', 2}, {'E', 1}, {'F', 4}, {'G', 2},
            {'H', 4}, {'I', 1}, {'J', 8}, {'K', 5}, {'L', 1}, {'M', 3}, {'N', 1},
            {'O', 1}, {'P', 3}, {'Q', 10},{'R', 1}, {'S', 1}, {'T', 1}, {'U', 1},
            {'V', 4}, {'W', 4}, {'X', 8}, {'Y', 4}, {'Z', 10}
        };
    }

    void Start()
    {
        // currentState might be set by another script if DontDestroyOnLoad is used,
        // but for a typical single-scene setup or fresh start, initialize.
        if (currentState != GameState.Initializing && currentState != GameState.Playing) // Avoid re-init if already playing (e.g. after scene reload from pause)
        {
             SetState(GameState.Initializing);
        }
        StartGame(); // StartGame will transition to Playing
    }

    void Update()
    {
        if (currentState == GameState.Playing && currentDisplayMode == DisplayMode.Timer)
        {
            UpdateTimer();
        }
    }

    // --- State Management ---\
    private void SetState(GameState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        // Debug.Log($"GameManager: State changed to {currentState}");

        switch (currentState)
        {
            case GameState.Initializing:
                Time.timeScale = 1f; // Ensure time is running during init
                if (gridInputHandler != null) gridInputHandler.enabled = false; // Disable input during setup
                break;
            case GameState.Playing:
                Time.timeScale = 1f;
                if (gridInputHandler != null) gridInputHandler.enabled = true;
                if (pausePanel != null) pausePanel.SetActive(false);
                break;
            case GameState.Paused:
                Time.timeScale = 0f; // Pause game
                if (gridInputHandler != null) gridInputHandler.enabled = false; // Disable input when paused
                if (pausePanel != null) pausePanel.SetActive(true);
                break;
            case GameState.GameOver:
                Time.timeScale = 1f; // Or 0f if you want to freeze everything instantly. 1f allows end-game animations.
                if (gridInputHandler != null) gridInputHandler.enabled = false;
                // GameOver panel is shown by EndGame()
                break;
        }
    }

    // Sets up a new game or restarts the current one
    private void StartGame()
    {
        // Debug.Log("GM: Starting New Game Setup...");
        SetState(GameState.Initializing); // Explicitly set to initializing for setup

        currentScore = 0; UpdateScoreUI();

        // Configure Timer/Moves display
        if (statusDisplayGroup != null)
        {
            bool isGroupActive = currentDisplayMode != DisplayMode.None;
            statusDisplayGroup.SetActive(isGroupActive);
            if (isGroupActive)
            {
                if (timerText != null) timerText.gameObject.SetActive(currentDisplayMode == DisplayMode.Timer);
                if (movesText != null) movesText.gameObject.SetActive(currentDisplayMode == DisplayMode.Moves);
                if (currentDisplayMode == DisplayMode.Timer) { currentTimeRemaining = gameTimeLimit; UpdateTimerUI(); }
                else if (currentDisplayMode == DisplayMode.Moves) { currentMovesRemaining = startingMoves; UpdateMovesUI(); }
            }
        }
        else { Debug.LogWarning("GM: StatusDisplayGroup not assigned."); }

        // Initialize Grid
        if (wordGridManager != null) { wordGridManager.InitializeGrid(); }
        else { Debug.LogError("GM: Cannot initialize grid - WordGridManager reference missing!", this); return; } // Critical failure

        // Reset Validator & Perform Initial Check
        if (wordValidator != null)
        {
            wordValidator.ResetFoundWordsList();
            wordValidator.SetGameManager(this); // Ensure validator has GM ref
            if (wordGridManager != null)
            {
                wordGridManager.TriggerValidationCheck(); // WordGridManager calls HandleValidationResult
            }
            else
            {
                Debug.LogError("GM StartGame: Cannot perform initial validation - WordGridManager missing!");
            }
        }
        else { Debug.LogError("GM: Cannot reset validator - WordValidator reference missing!", this); }

        // Hide Panels (already done in Awake, but good for restart)
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);

        SetState(GameState.Playing); // Transition to Playing state
        // Debug.Log("GM: New Game Started. State set to Playing.");
    }

    // Ends the current game session
    private void EndGame(bool timeout = false, bool noMoves = false)
    {
        if (currentState == GameState.GameOver) return; // Prevent multiple calls

        // Ensure the reason for game over matches the current mode
        if (timeout && currentDisplayMode != DisplayMode.Timer) return;
        if (noMoves && currentDisplayMode != DisplayMode.Moves) return;

        string reason = timeout ? "Time Ran Out" : (noMoves ? "No Moves Left" : "Game Over");
        // Debug.Log($"Game Over! Reason: {reason}");
        SetState(GameState.GameOver); // Set state first

        if (gameOverPanel != null) 
        {
            gameOverPanel.SetActive(true); 
            // Optionally, populate game over text here if you have specific TMP elements on gameOverPanel
            // e.g., gameOverPanel.transform.Find("ReasonText").GetComponent<TextMeshProUGUI>().text = reason;
            // e.g., gameOverPanel.transform.Find("FinalScoreText").GetComponent<TextMeshProUGUI>().text = "Score: " + currentScore;
        }
        else { Debug.LogWarning("GM: GameOver Panel reference missing!", this); }
    }


    // --- Word Processing Chain Reaction Logic (from your script) ---
    public void HandleValidationResult(List<FoundWordData> foundWords)
    {
        if (currentState != GameState.Playing) return; // Only process if playing
        if (foundWords == null || foundWords.Count == 0) { return; } 
        
        // Process the FIRST valid word found in this batch.
        // Your original logic processes only the first. If multiple should queue, this needs adjustment.
        FoundWordData wordToProcess = foundWords[0]; 
        ProcessSingleWordInternal(wordToProcess);
    }

    private void ProcessSingleWordInternal(FoundWordData wordData)
    {
        if (currentState != GameState.Playing || string.IsNullOrEmpty(wordData.Word) || wordData.Coordinates == null || wordData.Coordinates.Count == 0) 
        { 
            // If conditions not met, and if no other animations are playing, perhaps trigger validation again to clear out.
            // This path should ideally not be hit if validation is correct.
            // if (currentState == GameState.Playing && !IsAnyAnimationPlaying) wordGridManager?.TriggerValidationCheck();
            return; 
        }
        if (effectsManager == null || wordGridManager == null || wordValidator == null) 
        {
            Debug.LogError("GM ProcessSingleWord: Missing critical component reference!");
            return; 
        }

        List<RectTransform> sourceCellRects = GetRectTransformsForCoords(wordData.Coordinates);
        if (sourceCellRects == null || sourceCellRects.Count == 0) 
        {
             // This might happen if cells were already processed or grid changed.
             // Triggering validation again might be an option if the grid is expected to be stable.
             // Debug.LogWarning($"ProcessSingleWordInternal: No valid RectTransforms for word {wordData.Word}. Re-validating.");
             // if (currentState == GameState.Playing && !IsAnyAnimationPlaying) wordGridManager?.TriggerValidationCheck();
            return; 
        }

        // Hide original cells
        foreach (Vector2Int coord in wordData.Coordinates)
        {
            CellController cell = wordGridManager.GetCellController(coord);
            cell?.FadeOutImmediate(); // Or a brief animation
        }

        List<int> individualLetterScores = wordData.Word.Select(letter => CalculateScoreValueForLetter(letter)).ToList();
        effectsManager.PlayFlyToScoreEffect(sourceCellRects, wordData.Word, individualLetterScores, HandleSingleLetterScore);

        DOVirtual.DelayedCall(replacementDelayAfterEffectStart, () => {
            if (this == null || wordGridManager == null || wordValidator == null || currentState != GameState.Playing) { return; } 

            wordGridManager.ReplaceLettersAt(wordData.Coordinates, true); // Replace & Start Fade-In
            wordValidator.MarkWordAsFoundInSession(wordData.Word); // Mark word as found

            float fadeInDuration = wordGridManager.CellFadeInDuration;
            DOVirtual.DelayedCall(fadeInDuration + 0.05f, () => { // Add small buffer for fade to complete
                if (this == null || wordValidator == null || wordGridManager == null || currentState != GameState.Playing) { return; } 

                if (!IsAnyAnimationPlaying) // Check if other animations (like inertia) are NOT playing
                {
                    // Debug.Log($"[{Time.time:F3}] DelayedCall (Validation): Triggering next validation check for {wordData.Word}.");
                    wordGridManager.TriggerValidationCheck(); // Trigger the cycle again
                }
                // else { Debug.LogWarning($"[{Time.time:F3}] DelayedCall (Validation): Skipped validation chain (State={currentState}, Animating={IsAnyAnimationPlaying})."); }
            }, false); 
        }, false); 
    }

    // --- Scoring Callbacks & Calculations ---
    private void HandleSingleLetterScore(int pointsToAdd)
    {
        if (pointsToAdd <= 0 || currentState == GameState.GameOver) return; 

        currentScore += pointsToAdd;
        UpdateScoreUI();

        if (scoreTextRectTransform != null)
        {
            scoreTextRectTransform.DOKill(complete: true); 
            scoreTextRectTransform.DOShakePosition(
                duration: scoreShakeDuration,
                strength: new Vector3(scoreShakeStrength, scoreShakeStrength, 0),
                vibrato: scoreShakeVibrato,
                randomness: 90,
                snapping: false,
                fadeOut: true
            );
        }
    }

    private int CalculateScoreValueForLetter(char letter)
    {
        int scoreValue = 0;
        char upperLetter = char.ToUpperInvariant(letter);
        switch (currentScoringMode)
        {
            case ScoringMode.LengthBased: scoreValue = pointsPerLetter; break;
            case ScoringMode.ScrabbleBased: if (!scrabbleLetterValues.TryGetValue(upperLetter, out scoreValue)) { scoreValue = 0; } break;
            default: Debug.LogError($"Unhandled Scoring Mode: {currentScoringMode}"); break;
        }
        return scoreValue;
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null) { scoreText.text = "Score: " + currentScore.ToString(); }
    }

    // --- Timer Handling ---
    private void UpdateTimer()
    {
        // This is called from Update(), so it's frame-dependent
        if (currentTimeRemaining > 0)
        {
            currentTimeRemaining -= Time.deltaTime;
            UpdateTimerUI();
            if (currentTimeRemaining <= 0)
            {
                currentTimeRemaining = 0; UpdateTimerUI(); EndGame(timeout: true);
            }
        }
    }
    private void UpdateTimerUI()
    {
        if (currentDisplayMode == DisplayMode.Timer && timerText != null && statusDisplayGroup != null && statusDisplayGroup.activeSelf)
        {
            int minutes = Mathf.FloorToInt(currentTimeRemaining / 60);
            int seconds = Mathf.FloorToInt(currentTimeRemaining % 60);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    // --- Moves Handling ---
    public void DecrementMoves() // Called by WordGridManager (or GridInputHandler via WordGridManager)
    {
        if (currentState != GameState.Playing || currentDisplayMode != DisplayMode.Moves) return;
        currentMovesRemaining--; UpdateMovesUI();
        if (currentMovesRemaining <= 0)
        {
            currentMovesRemaining = 0; UpdateMovesUI(); EndGame(noMoves: true);
        }
    }
    private void UpdateMovesUI()
    {
        if (currentDisplayMode == DisplayMode.Moves && movesText != null && statusDisplayGroup != null && statusDisplayGroup.activeSelf)
        {
            movesText.text = "Moves: " + currentMovesRemaining.ToString();
        }
    }

    // --- UI Button Actions ---
    public void RestartGame()
    {
        Time.timeScale = 1f; // Ensure time is running before reload
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    public void PauseGame()
    {
        if (currentState == GameState.Playing && !IsAnyAnimationPlaying) { SetState(GameState.Paused); }
    }
    public void ResumeGame()
    {
        if (currentState == GameState.Paused) { SetState(GameState.Playing); }
    }
    public void GoToHomeScreen()
    {
        if (string.IsNullOrEmpty(homeSceneName)) { Debug.LogError("Home Scene Name not set!", this); return; }
        Time.timeScale = 1f;
        SceneManager.LoadScene(homeSceneName);
    }
    public void QuitGame()
    {
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    private List<RectTransform> GetRectTransformsForCoords(List<Vector2Int> coords)
    {
        List<RectTransform> rects = new List<RectTransform>();
        if (wordGridManager == null || coords == null) { Debug.LogError("GetRectTransformsForCoords: WGM or coords null."); return null; }
        foreach (var coord in coords)
        {
            CellController cell = wordGridManager.GetCellController(coord);
            if (cell != null && cell.RectTransform != null) { rects.Add(cell.RectTransform); }
            else { Debug.LogWarning($"GetRectTransformsForCoords: No valid Cell/RectTransform at {coord}"); return null; } 
        }
        return rects;
    }

}