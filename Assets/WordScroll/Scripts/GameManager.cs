using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using System.Linq;
using System; // Required for System.Guid
using UnityEngine.UI; // Required for Slider
using WordScroll.Modifiers; // Required for ModifierManager and ModifierEffectType

public class GameManager : MonoBehaviour
{
    public enum ScoringMode { LengthBased, ScrabbleBased }
    public enum GameState { Initializing, Playing, Paused, GameOver } // Could add LevelComplete if more distinction is needed
    public enum DisplayMode { Timer, Moves, None }

    [Header("Game State")]
    [SerializeField] private GameState currentState = GameState.Initializing;
    public GameState CurrentStatePublic => currentState;
    private bool hasWon = false; // Flag to indicate if the player has won
    public bool HasWon => hasWon; // Public getter for UI scripts

    private bool isProcessingSequentialWords = false;
    public bool IsAnyAnimationPlaying
    {
        get
        {
            return isProcessingSequentialWords ||
                   (wordGridManager != null && wordGridManager.isAnimating) ||
                   (animatedScoringSystem != null && animatedScoringSystem.IsAnimating) ||
                   IsNumericalScoreAnimating;
        }
    }

    [Header("Game Mode & Display")]
    [SerializeField] private DisplayMode currentDisplayMode = DisplayMode.Timer;
    public DisplayMode CurrentGameDisplayMode => currentDisplayMode;
    [SerializeField] private float gameTimeLimit = 120f;
    
    [Header("Traditional Mode Settings (when not using Level System)")]
    [Tooltip("Starting moves for traditional mode (ignored when using Level System)")]
    [SerializeField] private int traditionalStartingMoves = 50;
    [Tooltip("Target score for traditional mode (ignored when using Level System)")]
    [SerializeField] private int traditionalTargetScore = 1000;

    [Header("Scene Navigation")]
    [SerializeField] private string homeSceneName = "HomeScreen";

    [Header("Scoring")]
    [SerializeField] private ScoringMode currentScoringMode = ScoringMode.LengthBased;
    public ScoringMode GetCurrentScoringModeSetting() => currentScoringMode;

    [Tooltip("Points per letter, used if Scoring Mode is LengthBased for actual scoring, and for ScrabbleBased if a letter has no defined Scrabble value (as a fallback, typically 1).")]
    [SerializeField] private int pointsPerLetter = 10;
    public int GetPointsPerLetterSetting() => pointsPerLetter;

    private Dictionary<char, int> scrabbleLetterValues;
    public IReadOnlyDictionary<char, int> GetScrabbleLetterValues() => scrabbleLetterValues;


    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI roundScoreText; // UI for current round score
    [SerializeField] private RectTransform scoreTextRectTransform;
    [SerializeField] private Slider scoreProgressBar;
    [SerializeField] private GameObject statusDisplayGroup;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI movesText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject pausePanel;

    [Header("Component References")]
    [SerializeField] private WordGridManager wordGridManager;
    [SerializeField] private WordValidator wordValidator;
    [SerializeField] private GridInputHandler gridInputHandler;
    [SerializeField] private NumericalScoreUI numericalScoreUI;
    [SerializeField] private AnimatedScoringSystem animatedScoringSystem;

    [Header("Timing & Combo Settings")]
    [SerializeField] private float replacementDelayAfterEffectStart = 0.4f;
    [SerializeField] private float visualPauseBetweenWordsInSequence = 0.25f;

    [Header("Effects")]
    [SerializeField] private float scoreShakeDuration = 0.2f;
    [SerializeField] private float scoreShakeStrength = 3f;
    [SerializeField] private int scoreShakeVibrato = 15;
    
    [Header("Score Transfer Animation")]
    [SerializeField] private float scoreTransferSpeed = 30f; // Points per second transfer rate
    [SerializeField] private float scoreTransferMinDelay = 0.02f; // Minimum delay between increments

    [Header("Level System Integration")]
    [SerializeField] private bool useLevelSystem = true;

    // Properties for level integration
    public bool IsUsingLevelSystem => useLevelSystem && LevelManager.Instance != null;
    
    // Get the target score based on current system
    private int targetScoreForLevel
    {
        get
        {
            if (IsUsingLevelSystem && LevelManager.Instance?.CurrentLevel != null)
            {
                return LevelManager.Instance.CurrentLevel.TargetScore;
            }
            return traditionalTargetScore;
        }
    }
    
    // Get the starting moves based on current system
    private int startingMoves
    {
        get
        {
            if (IsUsingLevelSystem && LevelManager.Instance?.CurrentLevel != null)
            {
                return LevelManager.Instance.CurrentLevel.MaxMoves;
            }
            return traditionalStartingMoves;
        }
    }

    private float currentTimeRemaining;
    private int currentMovesRemaining;
    private int currentScore = 0;
    private int currentRoundScore = 0; // Score to be transferred to total
    
    // Public getters that sync with LevelManager when appropriate
    public int CurrentScore 
    {
        get 
        { 
            if (IsUsingLevelSystem && LevelManager.Instance != null)
            {
                return LevelManager.Instance.CurrentScore;
            }
            return currentScore; 
        }
    }
    
    public int CurrentMovesRemaining
    {
        get
        {
            if (IsUsingLevelSystem && LevelManager.Instance != null && LevelManager.Instance.CurrentLevel != null)
            {
                return LevelManager.Instance.CurrentLevel.GetRemainingMoves(LevelManager.Instance.CurrentMoves);
            }
            return currentMovesRemaining;
        }
    }
    
    public int CurrentRoundScore => currentRoundScore; // Public getter for current round score
    public static GameManager instance;

    private List<FoundWordData> currentPotentialWords = new List<FoundWordData>();
    private Dictionary<System.Guid, Color> currentAppliedHighlightColors = new Dictionary<System.Guid, Color>();


    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        InitializeScrabbleValues();
        if (wordGridManager == null) wordGridManager = FindFirstObjectByType<WordGridManager>();
        if (wordValidator == null) wordValidator = FindFirstObjectByType<WordValidator>();
        if (gridInputHandler == null) gridInputHandler = FindFirstObjectByType<GridInputHandler>();
        if (animatedScoringSystem == null) animatedScoringSystem = FindFirstObjectByType<AnimatedScoringSystem>();
        if (scoreTextRectTransform == null && scoreText != null) scoreTextRectTransform = scoreText.GetComponent<RectTransform>();

        if (animatedScoringSystem == null) Debug.LogError("GM: AnimatedScoringSystem is MISSING! Scoring animations will not work.", this);
        if (wordGridManager == null) Debug.LogError("GM: WordGridManager missing!", this);
        if (wordValidator == null) Debug.LogError("GM: WordValidator missing!", this);
        if (gridInputHandler == null) Debug.LogError("GM: GridInputHandler missing! Tapping will not work.", this);
        if (scoreText == null) Debug.LogError("GM: Score Text (TMP) for progress bar missing!", this);
        if (scoreProgressBar == null) Debug.LogError("GM: Score Progress Bar (Slider) missing!", this);

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);

        if (wordValidator != null) wordValidator.SetGameManager(this);
        if (wordGridManager != null) wordGridManager.SetGameManager(this);
    }

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
        if (currentState != GameState.Initializing && currentState != GameState.Playing)
        {
            SetState(GameState.Initializing);
        }
        
        // Debug level system status
        Debug.Log($"🎮 GameManager Start: useLevelSystem={useLevelSystem}, LevelManager.Instance={(LevelManager.Instance != null ? "Found" : "NULL")}");
        Debug.Log($"🎮 IsUsingLevelSystem={IsUsingLevelSystem}");
        
        // Setup level system event listeners if using level system
        if (IsUsingLevelSystem)
        {
            LevelManager.OnLevelCompleted += OnLevelSystemCompleted;
            LevelManager.OnLevelFailed += OnLevelSystemFailed;
            LevelManager.OnMovesChanged += OnLevelSystemMovesChanged;
            LevelManager.OnScoreChanged += OnLevelSystemScoreChanged;
            Debug.Log($"🎮 Level System Events: Subscribed to LevelManager events");
        }
        else
        {
            Debug.Log($"🎮 Level System: Using traditional mode");
        }
        
        // DEBUG: Test scoring system
        #if UNITY_EDITOR
        if (Debug.isDebugBuild)
        {
            DebugTestScoring();
        }
        #endif
        
        StartGame();
    }

    void Update()
    {
        if (currentState == GameState.Playing && currentDisplayMode == DisplayMode.Timer)
        {
            UpdateTimer();
        }
    }

    private void SetState(GameState newState)
    {
        if (currentState == newState && newState != GameState.Initializing) return;

        GameState previousState = currentState;
        currentState = newState;

        if (previousState == GameState.Playing && (newState == GameState.GameOver || newState == GameState.Paused))
        {
            if (animatedScoringSystem != null)
            {
                animatedScoringSystem.ClearAllAnimations();
            }
        }
        isProcessingSequentialWords = false;

        switch (currentState)
        {
            case GameState.Initializing:
                Time.timeScale = 1f;
                if (gridInputHandler != null) gridInputHandler.enabled = false;
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
                Time.timeScale = 1f; // Keep time scale at 1 for game over animations/UI
                if (gridInputHandler != null) gridInputHandler.enabled = false;
                break;
        }
    }

    private void StartGame()
    {
        SetState(GameState.Initializing);
        
        // Initialize score and moves based on current system
        if (IsUsingLevelSystem)
        {
            // Let LevelManager handle score tracking - don't reset its score here
            currentScore = 0; // GameManager's internal tracking (not used for display)
            currentRoundScore = 0;
            Debug.Log($"🎮 Level System: Current level is {LevelManager.Instance?.CurrentLevel?.LevelName ?? "Unknown Level"}");
            Debug.Log($"🎮 Level System: LevelManager score is {LevelManager.Instance?.CurrentScore ?? 0}");
            
            // Ensure we have a current level - if not, start the first level
            if (LevelManager.Instance != null && LevelManager.Instance.CurrentLevel == null)
            {
                Debug.Log($"🎮 No current level found, starting level 0");
                bool levelStarted = LevelManager.Instance.StartLevel(0);
                Debug.Log($"🎮 Level start result: {levelStarted}");
            }
        }
        else
        {
            // Traditional mode initialization - reset everything
            currentScore = 0;
            currentRoundScore = 0;
            Debug.Log($"🎮 Traditional Mode: Reset score to 0");
        }
        
        hasWon = false; // Reset win state

        // Initialize animated scoring system
        if (animatedScoringSystem != null)
        {
            animatedScoringSystem.SetTotalScore(currentScore);
        }

        if (scoreProgressBar != null)
        {
            scoreProgressBar.maxValue = targetScoreForLevel;
            scoreProgressBar.value = 0;
        }
        UpdateScoreUI();
        UpdateRoundScoreUI(); // Initialize round score UI
        
        // DEBUG: Ensure main score elements are visible
        #if UNITY_EDITOR
        if (Debug.isDebugBuild)
        {
            Debug.Log($"[GameManager] Score UI Initialization:");
            Debug.Log($"  - scoreText: {(scoreText != null ? "✅ Found" : "❌ Missing")}");
            Debug.Log($"  - scoreText.gameObject.activeInHierarchy: {(scoreText != null ? scoreText.gameObject.activeInHierarchy : "N/A")}");
            Debug.Log($"  - roundScoreText: {(roundScoreText != null ? "✅ Found" : "❌ Missing")}");
            Debug.Log($"  - currentScore: {currentScore}");
            Debug.Log($"  - currentRoundScore: {currentRoundScore}");
        }
        #endif

        currentPotentialWords.Clear();
        currentAppliedHighlightColors.Clear();

        if (statusDisplayGroup != null)
        {
            bool isGroupActive = currentDisplayMode != DisplayMode.None;
            statusDisplayGroup.SetActive(isGroupActive);
            Debug.Log($"🎮 Status Display Group: Active={isGroupActive}, CurrentDisplayMode={currentDisplayMode}");
            
            if (isGroupActive)
            {
                if (timerText != null) timerText.gameObject.SetActive(currentDisplayMode == DisplayMode.Timer);
                if (movesText != null) 
                {
                    movesText.gameObject.SetActive(currentDisplayMode == DisplayMode.Moves);
                    Debug.Log($"📱 Moves Text GameObject: Active={movesText.gameObject.activeSelf}, DisplayMode={currentDisplayMode}");
                }
                
                if (currentDisplayMode == DisplayMode.Timer) 
                { 
                    currentTimeRemaining = gameTimeLimit; 
                    UpdateTimerUI(); 
                    Debug.Log($"⏰ Timer Mode: Time={currentTimeRemaining}");
                }
                else if (currentDisplayMode == DisplayMode.Moves) 
                { 
                    Debug.Log($"🎯 Moves Mode: IsUsingLevelSystem={IsUsingLevelSystem}");
                    
                    // Initialize moves - use traditional moves only if not using level system
                    if (!IsUsingLevelSystem)
                    {
                        currentMovesRemaining = startingMoves;
                        Debug.Log($"🎯 Traditional Moves: Set currentMovesRemaining={currentMovesRemaining} from startingMoves={startingMoves}");
                    }
                    else
                    {
                        Debug.Log($"🎯 Level System Moves: LevelManager.Instance={(LevelManager.Instance != null ? "Found" : "NULL")}, CurrentLevel={(LevelManager.Instance?.CurrentLevel != null ? LevelManager.Instance.CurrentLevel.LevelName : "NULL")}");
                        if (LevelManager.Instance?.CurrentLevel != null)
                        {
                            Debug.Log($"🎯 Level Data: MaxMoves={LevelManager.Instance.CurrentLevel.MaxMoves}, CurrentMoves={LevelManager.Instance.CurrentMoves}");
                        }
                    }
                    
                    UpdateMovesUI(); 
                }
            }
        }
        if (wordGridManager != null)
        {
            wordGridManager.InitializeGrid();
        }
        else
        {
            Debug.LogError("GM: Cannot initialize grid - WordGridManager missing!", this); return;
        }

        if (wordValidator != null)
        {
            wordValidator.ResetFoundWordsList();
            if (wordGridManager != null)
            {
                wordGridManager.TriggerValidationCheckAndHighlightUpdate();
            }
        }
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        
        // Force UI refresh when using level system to ensure everything displays correctly
        if (IsUsingLevelSystem)
        {
            // Use a small delay to ensure LevelManager has fully initialized
            Invoke(nameof(RefreshGameUI), 0.1f);
        }
        
        SetState(GameState.Playing);
    }

    private void PlayerWins()
    {
        if (currentState != GameState.Playing) return; // Already won or game ended otherwise

        Debug.Log("Player Wins! Target score reached.");
        EndGame(playerDidWin: true);
    }
    
    private void PlayerLoses()
    {
        if (currentState != GameState.Playing) return; // Already ended

        Debug.Log("Player Loses! Time/moves ran out.");
        EndGame(timeout: currentDisplayMode == DisplayMode.Timer, noMoves: currentDisplayMode == DisplayMode.Moves);
    }
    
    /// <summary>
    /// Shows the numerical score UI for a list of words
    /// </summary>
    private void ShowNumericalScore(List<FoundWordData> words)
    {
        if (numericalScoreUI != null && words != null && words.Count > 0)
        {
            numericalScoreUI.ShowNumericalScore(words);
        }
    }
    
    /// <summary>
    /// Checks if the numerical score UI is currently animating
    /// </summary>
    public bool IsNumericalScoreAnimating
    {
        get
        {
            return numericalScoreUI != null && numericalScoreUI.IsAnimating;
        }
    }

    private void EndGame(bool timeout = false, bool noMoves = false, bool playerDidWin = false)
    {
        if (currentState == GameState.GameOver) return; // Already in GameOver state

        if (playerDidWin)
        {
            hasWon = true;
        }
        else
        {
            // If EndGame is called for timeout or noMoves, check if player had already won.
            // If hasWon is true, it means PlayerWins() was called just before timer/moves ran out.
            if (hasWon) 
            {
                // Player reached target score just as time/moves ran out. Still a win.
                 Debug.Log("EndGame: Player had already won before timeout/noMoves.");
            }
            else // This is a loss due to timeout or no moves
            {
                if (timeout && currentDisplayMode != DisplayMode.Timer) return; // Invalid timeout call
                if (noMoves && currentDisplayMode != DisplayMode.Moves) return; // Invalid noMoves call
                hasWon = false;
                Debug.Log($"Game Over: Timeout={timeout}, NoMoves={noMoves}");
            }
        }

        SetState(GameState.GameOver);
        currentPotentialWords.Clear();
        currentAppliedHighlightColors.Clear();
        if (wordGridManager != null) wordGridManager.ClearAllCellHighlights();

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            // The script on gameOverPanel (e.g., GameOverUIController) should check GameManager.instance.HasWon
            // in its OnEnable() or Start() method to display the correct "You Win!" or "You Lose!" message.
        }
    }

    public void UpdatePotentialWordsDisplay(List<FoundWordData> potentialWordsFromValidator, Dictionary<System.Guid, Color> appliedColors)
    {
        if (currentState != GameState.Playing && currentState != GameState.Initializing)
        {
            currentPotentialWords.Clear();
            currentAppliedHighlightColors.Clear();
            if (wordGridManager != null) wordGridManager.ClearAllCellHighlights();
            return;
        }
        currentPotentialWords = potentialWordsFromValidator ?? new List<FoundWordData>();
        currentAppliedHighlightColors = appliedColors ?? new Dictionary<System.Guid, Color>();
    }

    public int GetPointsForActualScoring(char letter)
    {
        char upperLetter = char.ToUpperInvariant(letter);
        if (currentScoringMode == ScoringMode.ScrabbleBased)
        {
            if (scrabbleLetterValues.TryGetValue(upperLetter, out int val))
            {
                return val;
            }
            return 1;
        }
        return pointsPerLetter;
    }
    
    /// <summary>
    /// Gets the letter at the specified grid position
    /// </summary>
    public char GetLetterAtPosition(Vector2Int position)
    {
        if (wordGridManager != null && wordGridManager.gridData != null)
        {
            if (position.x >= 0 && position.x < wordGridManager.gridSize && 
                position.y >= 0 && position.y < wordGridManager.gridSize)
            {
                return wordGridManager.gridData[position.x, position.y];
            }
        }
        return ' '; // Return space if position is invalid
    }

    public int CalculateTotalScoreForWord(FoundWordData wordData)
    {
        if (wordData.Word == null) return 0;
        int totalScore = 0;
        foreach (char letter in wordData.Word)
        {
            totalScore += GetPointsForActualScoring(letter);
        }
        return totalScore;
    }

    public bool AttemptTapValidation(Vector2Int tappedCoordinate)
    {
        if (currentState != GameState.Playing || IsAnyAnimationPlaying)
        {
            return false;
        }

        List<FoundWordData> initialCandidatesFromTap = new List<FoundWordData>();
        foreach (var potentialWord in currentPotentialWords)
        {
            if (potentialWord.Coordinates.Contains(tappedCoordinate) &&
                !wordValidator.IsWordFoundThisSession(potentialWord.Word))
            {
                initialCandidatesFromTap.Add(potentialWord);
            }
        }

        if (initialCandidatesFromTap.Count == 0) return false;
        List<FoundWordData> allConnectedCandidates = FindAllConnectedWords(initialCandidatesFromTap);
        if (allConnectedCandidates.Count == 0) return false;

        List<FoundWordData> wordsToProcessInSequence = FilterSubWordsFromSelectedBatch(allConnectedCandidates);

        wordsToProcessInSequence = wordsToProcessInSequence
                                    .Where(w => !wordValidator.IsWordFoundThisSession(w.Word))
                                    .GroupBy(w => w.ID) 
                                    .Select(g => g.First())         
                                    .ToList();
        if (wordsToProcessInSequence.Count == 0) return false;

        wordsToProcessInSequence = wordsToProcessInSequence
            .OrderByDescending(w => CalculateTotalScoreForWord(w))
            .ThenByDescending(w => w.Word.Length)
            .ThenBy(w => w.ID.ToString())
            .ToList();

        Debug.Log("╔══════════════════════════════════════════════════════════════════╗");
        Debug.Log($"║ 🎮 WORDS FOUND: {wordsToProcessInSequence.Count.ToString().PadLeft(2)} words ready for processing {new string(' ', 20)}║");
        Debug.Log("╚══════════════════════════════════════════════════════════════════╝");
        
        foreach (var word in wordsToProcessInSequence)
        {
            int wordScore = CalculateTotalScoreForWord(word);
            Debug.Log($"📝 '{word.Word}' ({word.Word.Length} letters) = {wordScore} points");
        }
        
        int totalPotentialScore = wordsToProcessInSequence.Sum(w => CalculateTotalScoreForWord(w));
        Debug.Log($"💰 Total potential score from this tap: {totalPotentialScore} points");
        Debug.Log("");

        StartCoroutine(ProcessWordsSequentially(wordsToProcessInSequence));
        return true;
    }

    private List<FoundWordData> FilterSubWordsFromSelectedBatch(List<FoundWordData> candidates)
    {
        if (candidates == null || candidates.Count <= 1) return candidates ?? new List<FoundWordData>();

        var sortedCandidates = candidates.OrderByDescending(w => w.Word.Length).ThenBy(w => w.ID).ToList();
        List<FoundWordData> keptWords = new List<FoundWordData>();
        HashSet<System.Guid> discardedWordIds = new HashSet<System.Guid>();

        for (int i = 0; i < sortedCandidates.Count; i++)
        {
            FoundWordData currentWord = sortedCandidates[i];
            if (discardedWordIds.Contains(currentWord.ID)) continue;
            for (int j = 0; j < sortedCandidates.Count; j++)
            {
                if (i == j) continue;
                FoundWordData otherWord = sortedCandidates[j];
                if (discardedWordIds.Contains(otherWord.ID)) continue;
                if (otherWord.Word.Length < currentWord.Word.Length &&
                    currentWord.Word.Contains(otherWord.Word) &&
                    AreCoordinatesContainedAndAligned(otherWord.Coordinates, otherWord.GetOrientation(), currentWord.Coordinates, currentWord.GetOrientation()))
                {
                    discardedWordIds.Add(otherWord.ID);
                }
            }
        }
        foreach (var word in sortedCandidates)
        {
            if (!discardedWordIds.Contains(word.ID)) keptWords.Add(word);
        }
        return keptWords;
    }

    private bool AreCoordinatesContainedAndAligned(
        List<Vector2Int> innerCoords, FoundWordData.WordOrientation innerOrientation,
        List<Vector2Int> outerCoords, FoundWordData.WordOrientation outerOrientation)
    {
        if (innerCoords == null || outerCoords == null || innerCoords.Count == 0 || innerCoords.Count > outerCoords.Count) return false;
        if (innerOrientation != outerOrientation) return false;
        for (int i = 0; i <= outerCoords.Count - innerCoords.Count; i++)
        {
            bool match = true;
            for (int j = 0; j < innerCoords.Count; j++)
            {
                if (outerCoords[i + j] != innerCoords[j]) { match = false; break; }
            }
            if (match) return true;
        }
        return false;
    }

    private List<FoundWordData> FindAllConnectedWords(List<FoundWordData> startingWords)
    {
        List<FoundWordData> connectedWords = new List<FoundWordData>();
        Queue<FoundWordData> wordsToVisit = new Queue<FoundWordData>();
        HashSet<System.Guid> visitedWordIds = new HashSet<System.Guid>();

        foreach (var startWord in startingWords)
        {
            if (!wordValidator.IsWordFoundThisSession(startWord.Word) && visitedWordIds.Add(startWord.ID))
            {
                wordsToVisit.Enqueue(startWord);
                connectedWords.Add(startWord);
            }
        }

        while (wordsToVisit.Count > 0)
        {
            FoundWordData currentWord = wordsToVisit.Dequeue();
            Vector2Int intersectionPoint;
            foreach (var potentialWordOnGrid in currentPotentialWords)
            {
                if (potentialWordOnGrid.ID == currentWord.ID ||
                    visitedWordIds.Contains(potentialWordOnGrid.ID) ||
                    wordValidator.IsWordFoundThisSession(potentialWordOnGrid.Word))
                {
                    continue;
                }
                if (wordValidator.CheckIntersection(currentWord, potentialWordOnGrid, out intersectionPoint))
                {
                    if (visitedWordIds.Add(potentialWordOnGrid.ID))
                    {
                        connectedWords.Add(potentialWordOnGrid);
                        wordsToVisit.Enqueue(potentialWordOnGrid);
                    }
                }
            }
        }
        return connectedWords;
    }

    private IEnumerator ProcessWordsSequentially(List<FoundWordData> wordsToAnimateInOrder)
    {
        if (wordsToAnimateInOrder == null || wordsToAnimateInOrder.Count == 0) yield break;

        isProcessingSequentialWords = true;
        
        // NEW: Use the integrated scoring system at the beginning
        yield return StartCoroutine(ProcessScoringForWords(wordsToAnimateInOrder));
        
        // Check for win condition after scoring - LevelManager handles this for level system
        if (!IsUsingLevelSystem)
        {
            int currentGameScore = IsUsingLevelSystem && LevelManager.Instance != null ? LevelManager.Instance.CurrentScore : currentScore;
            if (currentGameScore >= targetScoreForLevel && currentState == GameState.Playing && !hasWon)
            {
                PlayerWins();
                if(currentState == GameState.GameOver) 
                {
                    isProcessingSequentialWords = false;
                    yield break;
                }
            }
        }
        List<Vector2Int> allUniqueAffectedCoordinatesFromThisSequence = new List<Vector2Int>();
        Dictionary<Vector2Int, int> cellUsageCountInSequence = new Dictionary<Vector2Int, int>();

        foreach (var wordData in wordsToAnimateInOrder)
        {
            if (wordData.Coordinates == null) continue;
            foreach (var coord in wordData.Coordinates)
            {
                if (cellUsageCountInSequence.ContainsKey(coord)) cellUsageCountInSequence[coord]++;
                else cellUsageCountInSequence[coord] = 1;
                allUniqueAffectedCoordinatesFromThisSequence.Add(coord);
            }
        }

        for (int wordIdx = 0; wordIdx < wordsToAnimateInOrder.Count; wordIdx++)
        {
            FoundWordData currentWordData = wordsToAnimateInOrder[wordIdx];
            if (wordValidator.IsWordFoundThisSession(currentWordData.Word)) continue;

            Debug.Log($"GM.ProcessSeq: Animating word {wordIdx + 1}/{wordsToAnimateInOrder.Count}: {currentWordData.Word}");

            if (wordGridManager != null && currentWordData.Coordinates != null)
            {
                foreach (Vector2Int coord in currentWordData.Coordinates)
                {
                    CellController cellController = wordGridManager.GetCellController(coord);
                    if (cellController == null) continue;

                    if (cellUsageCountInSequence.ContainsKey(coord))
                    {
                        cellUsageCountInSequence[coord]--;

                        if (cellUsageCountInSequence[coord] == 0)
                        {
                            cellController.SetAlpha(0f);
                        }
                        else
                        {
                            FoundWordData nextSharingWord = default;
                            bool foundNextUser = false;
                            for (int nextWordIdx = wordIdx + 1; nextWordIdx < wordsToAnimateInOrder.Count; nextWordIdx++)
                            {
                                if (wordsToAnimateInOrder[nextWordIdx].Coordinates != null &&
                                    wordsToAnimateInOrder[nextWordIdx].Coordinates.Contains(coord))
                                {
                                    nextSharingWord = wordsToAnimateInOrder[nextWordIdx];
                                    foundNextUser = true;
                                    break;
                                }
                            }
                            if (foundNextUser && currentAppliedHighlightColors.TryGetValue(nextSharingWord.ID, out Color nextWordColor))
                            {
                                cellController.SetHighlightState(true, nextWordColor);
                                cellController.SetAlpha(1f);
                            }
                            else
                            {
                                cellController.SetHighlightState(false, cellController.GetDefaultColor());
                                cellController.SetAlpha(1f);
                                Debug.LogWarning($"GM.ProcessSeq: Shared cell {coord} for '{currentWordData.Word}' had usage but couldn't find/color for next. Reverted.");
                            }
                        }
                    }
                    else
                    {
                        cellController.SetAlpha(0f);
                        Debug.LogError($"GM.ProcessSeq: Cell {coord} for '{currentWordData.Word}' not found in usage count. Made invisible.");
                    }
                }
            }

            // NOTE: Visual effects (cell floating, letter scoring) are now handled by AnimatedScoringSystem
            // in ProcessScoringForWords, so we skip the old EffectsManager calls here

            currentPotentialWords.RemoveAll(pwd => pwd.ID == currentWordData.ID);

            // Word is already marked as found in ProcessScoringForWords
            // No need to mark again or check win condition here


            if (wordIdx < wordsToAnimateInOrder.Count - 1 && visualPauseBetweenWordsInSequence > 0)
            {
                // If game ended due to win, don't continue with visual pause for next word
                if(currentState == GameState.GameOver) 
                {
                     isProcessingSequentialWords = false;
                     yield break;
                }
                yield return new WaitForSeconds(visualPauseBetweenWordsInSequence);
            }
        }

        // If game ended during the loop, this part might not be reached or necessary.
        if (currentState == GameState.GameOver && hasWon)
        {
            isProcessingSequentialWords = false;
            yield break;
        }

        if (replacementDelayAfterEffectStart > 0) yield return new WaitForSeconds(replacementDelayAfterEffectStart);

        List<Vector2Int> distinctAffectedCoordinates = allUniqueAffectedCoordinatesFromThisSequence.Distinct().ToList();
        if (wordGridManager != null && distinctAffectedCoordinates.Count > 0)
        {
            wordGridManager.ReplaceLettersAt(distinctAffectedCoordinates, true);
            yield return new WaitUntil(() => !wordGridManager.isAnimating);
            // Only trigger validation if the game is still playing
            if (currentState == GameState.Playing)
            {
                wordGridManager.TriggerValidationCheckAndHighlightUpdate();
            }
        }
        isProcessingSequentialWords = false;
    }

    public void ClearPotentialWords()
    {
        currentPotentialWords.Clear();
        currentAppliedHighlightColors.Clear();
    }

    /// <summary>
    /// New integrated scoring system that calculates scores for multiple words with modifiers
    /// and displays them through the NumericalScoreUI
    /// <summary>
    /// Process scoring for the given words with complete animated sequence
    /// </summary>
    private IEnumerator ProcessScoringForWords(List<FoundWordData> words)
    {
        if (words == null || words.Count == 0) yield break;
        
        // Generate the complete scoring data
        var scoringData = NumericalScoringData.GenerateFromWords(words, this);
        
        // Get cell transforms for animation
        List<RectTransform> cellTransforms = new List<RectTransform>();
        foreach (var word in words)
        {
            foreach (var coord in word.Coordinates)
            {
                var cellController = wordGridManager?.GetCellController(coord);
                if (cellController != null && cellController.RectTransform != null)
                {
                    cellTransforms.Add(cellController.RectTransform);
                }
            }
        }
        
        // Start the complete animated scoring sequence
        if (animatedScoringSystem != null)
        {
            // Start both systems simultaneously for parallel execution
            Coroutine numericalUICoroutine = null;
            if (numericalScoreUI != null)
            {
                numericalUICoroutine = StartCoroutine(numericalScoreUI.ShowNumericalScoreParallel(words));
            }
            
            // Start cell animation system
            animatedScoringSystem.StartScoringAnimation(scoringData, cellTransforms);
            
            // Wait for both to complete
            if (numericalUICoroutine != null)
            {
                yield return numericalUICoroutine;
            }
            yield return new WaitUntil(() => !animatedScoringSystem.IsAnimating);
            
            // Handle cleanup after animation
            animatedScoringSystem.OnScoringComplete(words);
            
            // Update game score from animated scoring system (only in traditional mode)
            if (!IsUsingLevelSystem)
            {
                int newTotalScore = animatedScoringSystem.GetTotalScore();
                currentScore = newTotalScore;
                Debug.Log($"[GameManager] Traditional mode: Updated currentScore to {currentScore} from animated scoring system.");
            }
            else
            {
                Debug.Log($"[GameManager] Level system mode: Skipping currentScore update from animated scoring system.");
                Debug.Log($"[GameManager] ↳ LevelManager.Instance exists: {LevelManager.Instance != null}");
                if (LevelManager.Instance != null)
                {
                    Debug.Log($"[GameManager] ↳ LevelManager.CurrentScore: {LevelManager.Instance.CurrentScore}");
                }
                Debug.Log($"[GameManager] ↳ GameManager.CurrentScore property: {CurrentScore}");
                Debug.Log($"[GameManager] ↳ animatedScoringSystem.GetTotalScore(): {animatedScoringSystem.GetTotalScore()}");
                
                // CRITICAL FIX: Add score to LevelManager immediately so UpdateScoreUI shows correct value
                if (LevelManager.Instance != null && scoringData.finalScore > 0)
                {
                    int scoreBeforeAdd = LevelManager.Instance.CurrentScore;
                    Debug.Log($"[GameManager] ↳ BEFORE AddScore: LevelManager.CurrentScore = {scoreBeforeAdd}");
                    Debug.Log($"[GameManager] ↳ Adding {scoringData.finalScore} points to LevelManager immediately");
                    LevelManager.Instance.AddScore(scoringData.finalScore);
                    int scoreAfterAdd = LevelManager.Instance.CurrentScore;
                    Debug.Log($"[GameManager] ↳ AFTER AddScore: LevelManager.CurrentScore = {scoreAfterAdd}");
                    Debug.Log($"[GameManager] ↳ Expected total: {scoreBeforeAdd} + {scoringData.finalScore} = {scoreBeforeAdd + scoringData.finalScore}");
                    
                    if (scoreAfterAdd != scoreBeforeAdd + scoringData.finalScore)
                    {
                        Debug.LogError($"🚨 LEVELMANAGER SCORE MISMATCH! Expected {scoreBeforeAdd + scoringData.finalScore}, got {scoreAfterAdd}");
                    }
                }
            }
            
            // Update UI - the safeguards in UpdateScoreUI will prevent score resets
            UpdateScoreUI();
        }
        else
        {
            // Fallback to instant scoring if animated system not available
            Debug.LogWarning("[GameManager] AnimatedScoringSystem not found. Using instant scoring.");
            
            // Still show numerical score breakdown even without animated system
            if (numericalScoreUI != null)
            {
                yield return StartCoroutine(numericalScoreUI.ShowNumericalScoreParallel(words));
            }
            
            ApplyFinalScore(scoringData.finalScore);
        }
        
        // Handle move reduction from modifiers
        ApplyMoveReductionFromModifiers();
        
        // Mark all words as found
        foreach (var word in words)
        {
            wordValidator.MarkWordAsFoundInSession(word.Word);
        }
    }
    
    /// <summary>
    /// Applies the final calculated score to the game total with counting animation
    /// </summary>
    private void ApplyFinalScore(int finalScore)
    {
        if (finalScore <= 0 || currentState == GameState.GameOver) return;
        
        Debug.Log("╔══════════════════════════════════════════════════════════════════╗");
        Debug.Log($"║ 🎯 STARTING SCORE TRANSFER: {finalScore} points                    ║");
        Debug.Log("╚══════════════════════════════════════════════════════════════════╝");
        
        // Set the round score and start the transfer animation
        currentRoundScore = finalScore;
        StartCoroutine(TransferScoreAnimation());
    }
    
    /// <summary>
    /// Animates the transfer of points from round score to total score
    /// </summary>
    private IEnumerator TransferScoreAnimation()
    {
        // Calculate transfer rate
        float pointsPerSecond = scoreTransferSpeed;
        float delayBetweenPoints = Mathf.Max(1f / pointsPerSecond, scoreTransferMinDelay);
        
        int totalPointsToTransfer = currentRoundScore;
        int pointsTransferred = 0;
        
        Debug.Log($"🔄 Starting score transfer: {totalPointsToTransfer} points at {pointsPerSecond:F1} points/sec");
        
        if (IsUsingLevelSystem)
        {
            // For level system: Score already added to LevelManager in ProcessScoringForWords
            // Just animate the visual transfer without affecting actual scores
            Debug.Log($"🔄 Level system: Score already added to LevelManager, animating visual transfer only");
            
            // Animate the visual transfer without affecting actual scores
            while (currentRoundScore > 0 && currentState == GameState.Playing)
            {
                currentRoundScore--;
                pointsTransferred++;
                
                // Update UI every few points instead of every single point to reduce conflicts
                if (pointsTransferred % 3 == 0 || currentRoundScore == 0)
                {
                    UpdateScoreUI();
                    UpdateRoundScoreUI();
                }
                
                // Optional: Add shake effect every few points
                if (pointsTransferred % 5 == 0 && scoreTextRectTransform != null)
                {
                    scoreTextRectTransform.DOKill(true);
                    scoreTextRectTransform.DOPunchScale(Vector3.one * 0.1f, 0.1f, 1, 0f);
                }
                
                yield return new WaitForSeconds(delayBetweenPoints);
            }
        }
        else
        {
            // For traditional system: Transfer points one by one to currentScore
            while (currentRoundScore > 0 && currentState == GameState.Playing)
            {
                // Transfer one point
                currentRoundScore--;
                currentScore++;
                pointsTransferred++;
                
                // Update UI
                UpdateScoreUI();
                UpdateRoundScoreUI();
                
                // Optional: Add shake effect every few points
                if (pointsTransferred % 5 == 0 && scoreTextRectTransform != null)
                {
                    scoreTextRectTransform.DOKill(true);
                    scoreTextRectTransform.DOPunchScale(Vector3.one * 0.1f, 0.1f, 1, 0f);
                }
                
                yield return new WaitForSeconds(delayBetweenPoints);
            }
        }
        
        // Ensure we're at the correct final values
        int remainingPoints = currentRoundScore;
        if (remainingPoints > 0)
        {
            if (IsUsingLevelSystem)
            {
                // Points already added to LevelManager, just clear round score
                currentRoundScore = 0;
            }
            else
            {
                // Add remaining points to traditional score
                currentScore += remainingPoints;
                currentRoundScore = 0;
            }
            
            UpdateScoreUI();
            UpdateRoundScoreUI();
        }
        
        Debug.Log("╔══════════════════════════════════════════════════════════════════╗");
        Debug.Log($"║ ✅ SCORE TRANSFER COMPLETE: +{totalPointsToTransfer} points (Total: {(IsUsingLevelSystem ? LevelManager.Instance?.CurrentScore ?? 0 : currentScore)})   ║");
        Debug.Log("╚══════════════════════════════════════════════════════════════════╝");
        
        // Final effects and checks
        OnScoreTransferComplete();
    }
    
    /// <summary>
    /// Called when score transfer animation is complete
    /// </summary>
    private void OnScoreTransferComplete()
    {
        // Update debug system
        try
        {
            var debugSystem = GameObject.FindGameObjectWithTag("DebugSystem");
            if (debugSystem != null)
            {
                debugSystem.SendMessage("UpdateCurrentScore", currentScore, SendMessageOptions.DontRequireReceiver);
            }
        }
        catch (UnityException ex)
        {
            if (!ex.Message.Contains("Tag: DebugSystem is not defined"))
            {
                Debug.LogWarning($"[GameManager] Debug system error: {ex.Message}");
            }
        }
        
        // Game progress info
        int currentGameScore = IsUsingLevelSystem && LevelManager.Instance != null ? LevelManager.Instance.CurrentScore : currentScore;
        float progressToTarget = targetScoreForLevel > 0 ? (float)currentGameScore / targetScoreForLevel * 100f : 0f;
        Debug.Log($"📊 Game Progress: {progressToTarget:F1}% to target ({currentGameScore}/{targetScoreForLevel})");
        
        if (currentState == GameState.Playing)
        {
            if (IsUsingLevelSystem && LevelManager.Instance != null && LevelManager.Instance.CurrentLevel != null)
            {
                int movesRemaining = LevelManager.Instance.CurrentLevel.GetRemainingMoves(LevelManager.Instance.CurrentMoves);
                Debug.Log($"⏱️  Game Status: Playing | Moves Remaining: {(movesRemaining == -1 ? "∞" : movesRemaining.ToString())}");
            }
            else
            {
                Debug.Log($"⏱️  Game Status: Playing | Moves Remaining: {currentMovesRemaining}");
            }
        }
        
        // Check for win condition - LevelManager handles this for level system
        if (!IsUsingLevelSystem && currentGameScore >= targetScoreForLevel && currentState == GameState.Playing && !hasWon)
        {
            PlayerWins();
        }
        
        // Final score shake effect
        if (scoreTextRectTransform != null && currentState == GameState.Playing)
        {
            scoreTextRectTransform.DOKill(true);
            scoreTextRectTransform.DOShakePosition(scoreShakeDuration, scoreShakeStrength, scoreShakeVibrato, 90, false, true).SetUpdate(true);
        }
    }
    
    /// <summary>
    /// Sets the round score immediately (for visual feedback when words are found)
    /// </summary>
    public void SetRoundScore(int score)
    {
        currentRoundScore = score;
        UpdateRoundScoreUI();
        
        Debug.Log($"📝 Round score set to: {score} points");
    }
    
    /// <summary>
    /// Skip the current score transfer animation and apply remaining points instantly
    /// </summary>
    public void SkipScoreTransferAnimation()
    {
        if (currentRoundScore > 0)
        {
            Debug.Log($"⏭️  Skipping score transfer animation. Applying {currentRoundScore} points instantly.");
            
            int pointsToAdd = currentRoundScore;
            currentRoundScore = 0;
            
            if (IsUsingLevelSystem)
            {
                // Points already added to LevelManager in TransferScoreAnimation, just clear round score
                Debug.Log($"🎮 Level System: Score already added to LevelManager");
            }
            else
            {
                // Add points to traditional score
                currentScore += pointsToAdd;
            }
            
            UpdateScoreUI();
            UpdateRoundScoreUI();
            
            StopCoroutine(TransferScoreAnimation());
            OnScoreTransferComplete();
        }
    }
    
    /// <summary>
    /// Applies move reduction from active modifiers
    /// </summary>
    private void ApplyMoveReductionFromModifiers()
    {
        if (currentDisplayMode != DisplayMode.Moves) return;
        
        var modifierManager = ModifierManager.Instance;
        if (modifierManager == null) return;
        
        var activeModifiers = modifierManager.GetAllActiveModifiers();
        int totalMoveReduction = 0;
        
        foreach (var modifier in activeModifiers)
        {
            // Check if this modifier has move reduction effects
            if (modifier.effectType == ModifierEffectType.GeneralScoreBonusAndMoveReduction)
            {
                int moveReduction = Mathf.RoundToInt(modifier.moveReductionPercentage * startingMoves / 100f);
                if (moveReduction > 0)
                {
                    totalMoveReduction += moveReduction;
                    Debug.Log($"⚡ Modifier '{modifier.cardName}' reduces moves by {moveReduction} ({modifier.moveReductionPercentage}%)");
                }
            }
        }
        
        if (totalMoveReduction > 0)
        {
            currentMovesRemaining = Mathf.Max(0, currentMovesRemaining - totalMoveReduction);
            UpdateMovesUI();
            
            Debug.Log($"🎯 Total move reduction applied: -{totalMoveReduction} moves (Remaining: {currentMovesRemaining})");
            
            // Check if we've run out of moves
            if (currentMovesRemaining <= 0)
            {
                currentMovesRemaining = 0;
                PlayerLoses();
            }
        }
        else
        {
            Debug.Log("ℹ️  No move reduction modifiers active");
        }
    }
    
    /// <summary>
    /// Updates the score display UI
    /// </summary>
    private void UpdateScoreUI()
    {
        int displayScore;
        
        Debug.Log($"🔍 UpdateScoreUI ENTRY: IsUsingLevelSystem={IsUsingLevelSystem}, LevelManager.Instance={LevelManager.Instance != null}");
        
         // Use appropriate score source based on system
        if (IsUsingLevelSystem && LevelManager.Instance != null)
        {
            displayScore = LevelManager.Instance.CurrentScore;
            Debug.Log($"🎯 UpdateScoreUI (Level System): LevelManager.CurrentScore={displayScore}, currentRoundScore={currentRoundScore}");
            Debug.Log($"🎯 ↳ IsUsingLevelSystem: {IsUsingLevelSystem}");
            Debug.Log($"🎯 ↳ LevelManager.Instance != null: {LevelManager.Instance != null}");
            Debug.Log($"🎯 ↳ LevelManager.Instance.CurrentScore (direct): {LevelManager.Instance.CurrentScore}");
            Debug.Log($"🎯 ↳ GameManager.currentScore (internal): {currentScore}");
            
            // CRITICAL: Check if LevelManager score is 0 when it shouldn't be
            if (displayScore == 0 && currentRoundScore > 0)
            {
                Debug.LogError($"🚨 CRITICAL: LevelManager.CurrentScore is 0 but currentRoundScore is {currentRoundScore}! This suggests LevelManager was reset!");
            }
        }
        else
        {
            displayScore = currentScore;
            Debug.Log($"🎯 UpdateScoreUI (Traditional): currentScore={displayScore}, currentRoundScore={currentRoundScore}");
            Debug.Log($"🎯 ↳ IsUsingLevelSystem: {IsUsingLevelSystem}");
            Debug.Log($"🎯 ↳ LevelManager.Instance != null: {LevelManager.Instance != null}");
        }

        // CRITICAL: Debug what LevelManager actually contains before safeguard
        if (IsUsingLevelSystem && LevelManager.Instance != null)
        {
            int levelManagerScore = LevelManager.Instance.CurrentScore;
            Debug.Log($"🔍 PRE-SAFEGUARD: displayScore={displayScore}, levelManagerScore={levelManagerScore}");
            
            if (displayScore < levelManagerScore)
            {
                Debug.LogError($"🚨 SCORE RESET DETECTED! Preventing UI from showing {displayScore}, forcing to {levelManagerScore}");
                displayScore = levelManagerScore;
            }
            else
            {
                Debug.Log($"✅ Score safeguard: displayScore ({displayScore}) >= levelManagerScore ({levelManagerScore})");
            }
        }
        
        if (scoreText != null)
        {
            string previousText = scoreText.text;
            scoreText.text = displayScore.ToString();
            Debug.Log($"📱 Score Text: \"{previousText}\" → \"{scoreText.text}\"");
            
            // CRITICAL: Detect if total score is going backwards
            if (int.TryParse(previousText, out int previousScore) && displayScore < previousScore && displayScore >= 0)
            {
                Debug.LogError($"🚨 TOTAL SCORE REGRESSION! Score went from {previousScore} to {displayScore} - this should NEVER happen!");
            }
        }
        else
        {
            Debug.LogError("❌ scoreText is NULL in UpdateScoreUI!");
        }
        
        if (scoreProgressBar != null)
        {
            float progress = targetScoreForLevel > 0 ? (float)displayScore / targetScoreForLevel : 0f;
            scoreProgressBar.value = Mathf.Clamp01(progress);
        }
    }
    
    /// <summary>
    /// Updates the current round score display UI
    /// </summary>
    private void UpdateRoundScoreUI()
    {
        if (roundScoreText != null)
        {
            if (currentRoundScore > 0)
            {
                roundScoreText.text = $"+{currentRoundScore}";
                roundScoreText.gameObject.SetActive(true);
            }
            else
            {
                // OPTION 1: Always show, display "0" when no round score
                roundScoreText.text = "+0";
                roundScoreText.gameObject.SetActive(true);
                
                // OPTION 2: Keep original behavior (hide when 0)
                // roundScoreText.gameObject.SetActive(false);
            }
        }
    }
    
    /// <summary>
    /// Updates the timer display UI
    /// </summary>
    private void UpdateTimerUI()
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(currentTimeRemaining / 60f);
            int seconds = Mathf.FloorToInt(currentTimeRemaining % 60f);
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }
    
    /// <summary>
    /// Updates the moves display UI
    /// </summary>
    private void UpdateMovesUI()
    {
        if (movesText != null)
        {
            if (IsUsingLevelSystem && LevelManager.Instance != null && LevelManager.Instance.CurrentLevel != null)
            {
                int currentMoves = LevelManager.Instance.CurrentMoves;
                int movesRemaining = LevelManager.Instance.CurrentLevel.GetRemainingMoves(currentMoves);
                
                // Debug logging to see what's happening
                Debug.Log($"🎯 UpdateMovesUI (Level System): CurrentMoves={currentMoves}, MovesRemaining={movesRemaining}, MaxMoves={LevelManager.Instance.CurrentLevel.MaxMoves}");
                
                if (movesRemaining == -1)
                {
                    movesText.text = "Moves: ∞";
                }
                else
                {
                    movesText.text = $"Moves: {movesRemaining}";
                }
            }
            else
            {
                Debug.Log($"🎯 UpdateMovesUI (Traditional): CurrentMovesRemaining={currentMovesRemaining}, IsUsingLevelSystem={IsUsingLevelSystem}");
                movesText.text = $"Moves: {currentMovesRemaining}";
            }
            
            Debug.Log($"📱 Moves Text Updated: \"{movesText.text}\"");
        }
        else
        {
            Debug.LogError("❌ UpdateMovesUI: movesText is NULL!");
        }
    }
    
    /// <summary>
    /// Updates the timer each frame
    /// </summary>
    private void UpdateTimer()
    {
        if (currentDisplayMode == DisplayMode.Timer && currentState == GameState.Playing)
        {
            currentTimeRemaining -= Time.deltaTime;
            UpdateTimerUI();
            
            if (currentTimeRemaining <= 0f)
            {
                currentTimeRemaining = 0f;
                PlayerLoses(); // Time's up!
            }
        }
    }
    
    /// <summary>
    /// Gets RectTransforms for the given coordinates
    /// </summary>
    private List<RectTransform> GetRectTransformsForCoords(List<Vector2Int> coordinates)
    {
        List<RectTransform> rectTransforms = new List<RectTransform>();
        
        if (wordGridManager == null || coordinates == null) return rectTransforms;
        
        foreach (var coord in coordinates)
        {
            var cellController = wordGridManager.GetCellController(coord);
            if (cellController != null && cellController.RectTransform != null)
            {
                rectTransforms.Add(cellController.RectTransform);
            }
        }
        
        return rectTransforms;
    }
    
    /// <summary>
    /// Calculates the score value for a specific letter (for UI display purposes)
    /// </summary>
    public int CalculateScoreValueForLetter(char letter)
    {
        return GetPointsForActualScoring(letter);
    }
    
    /// <summary>
    /// Decrements the move counter by 1
    /// </summary>
    public void DecrementMoves()
    {
        // Handle level system integration
        if (IsUsingLevelSystem)
        {
            // Let LevelManager handle move counting and game end logic
            LevelManager.Instance.AddMove();
            return;
        }
        
        // Original move counting logic for non-level gameplay
        if (currentDisplayMode != DisplayMode.Moves) return;
        
        currentMovesRemaining--;
        UpdateMovesUI();
        
        if (currentMovesRemaining <= 0)
        {
            currentMovesRemaining = 0;
            PlayerLoses(); // No moves left!
        }
    }
    
    /// <summary>
    /// Navigate back to the home screen/main menu
    /// </summary>
    public void GoToHomeScreen()
    {
        Debug.Log($"[GameManager] Navigating to home screen: {homeSceneName}");
        
        // Stop any ongoing tweens to prevent conflicts
        DOTween.KillAll();
        
        // Load the home screen scene
        SceneManager.LoadScene(homeSceneName);
    }
    
    /// <summary>
    /// Restart the current game scene
    /// </summary>
    public void RestartGame()
    {
        Debug.Log("[GameManager] Restarting current game scene");
        
        // Stop any ongoing tweens to prevent conflicts
        DOTween.KillAll();
        
        // Reload the current scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    /// <summary>
    /// Skip the current animated scoring sequence
    /// </summary>
    public void SkipAnimatedScoring()
    {
        if (animatedScoringSystem != null && animatedScoringSystem.IsAnimating)
        {
            animatedScoringSystem.SkipAnimation();
            
            // Update game score
            int newTotalScore = animatedScoringSystem.GetTotalScore();
            currentScore = newTotalScore;
            UpdateScoreUI();
            
            Debug.Log("[GameManager] Animated scoring skipped");
        }
    }
    
    /// <summary>
    /// Gets the current game score
    /// </summary>
    public int GetCurrentScore()
    {
        return currentScore;
    }
    
    /// <summary>
    /// Manually refresh the moves UI - useful for ensuring UI is updated after level initialization
    /// </summary>
    public void RefreshMovesUI()
    {
        Debug.Log($"🔄 RefreshMovesUI called manually");
        if (currentDisplayMode == DisplayMode.Moves)
        {
            UpdateMovesUI();
        }
    }
    
    /// <summary>
    /// Manually refresh both score and moves UI
    /// </summary>
    public void RefreshGameUI()
    {
        Debug.Log($"🔄 RefreshGameUI called manually");
        UpdateScoreUI();
        if (currentDisplayMode == DisplayMode.Moves)
        {
            UpdateMovesUI();
        }
    }
    
    /// <summary>
    /// Force score UI to show the correct LevelManager score (for debugging)
    /// </summary>
    public void ForceCorrectScoreDisplay()
    {
        if (IsUsingLevelSystem && LevelManager.Instance != null && scoreText != null)
        {
            int correctScore = LevelManager.Instance.CurrentScore;
            scoreText.text = correctScore.ToString();
            Debug.Log($"🔧 Forced score display to correct value: {correctScore}");
        }
    }

    /// <summary>
    /// DEBUG: Test method to verify scoring system is working correctly
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugTestScoring()
    {
        Debug.Log("=== SCORING SYSTEM DEBUG TEST ===");
        Debug.Log($"Current Scoring Mode: {currentScoringMode}");
        Debug.Log($"Points Per Letter (fallback): {pointsPerLetter}");
        
        // Test some common letters
        char[] testLetters = {'A', 'E', 'Q', 'Z', 'T', 'H'};
        foreach (char letter in testLetters)
        {
            int score = GetPointsForActualScoring(letter);
            Debug.Log($"Letter '{letter}': {score} points");
        }
        
        // Test if scrabble values are initialized
        if (scrabbleLetterValues != null)
        {
            Debug.Log($"Scrabble dictionary has {scrabbleLetterValues.Count} entries");
            Debug.Log($"Sample: Q = {(scrabbleLetterValues.ContainsKey('Q') ? scrabbleLetterValues['Q'] : "NOT FOUND")}");
        }
        else
        {
            Debug.LogError("❌ Scrabble dictionary is NULL!");
        }
        
        Debug.Log("=== END SCORING DEBUG TEST ===");
    }
    
    /// <summary>
    /// DEBUG: Switch scoring mode for testing
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugToggleScoringMode()
    {
        currentScoringMode = (currentScoringMode == ScoringMode.ScrabbleBased) 
            ? ScoringMode.LengthBased 
            : ScoringMode.ScrabbleBased;
            
        Debug.Log($"🔄 Scoring mode switched to: {currentScoringMode}");
        
        // Reconfigure systems that cache scoring settings
        if (wordValidator != null)
        {
            wordValidator.SetGameManager(this); // This will trigger ConfigureScoring
        }
        
        // Test the new mode
        DebugTestScoring();
    }

    /// <summary>
    /// Handle level completion in level system
    /// </summary>
    private void OnLevelSystemCompleted(LevelData level, int finalScore, int stars)
    {
        hasWon = true;
        SetState(GameState.GameOver);
        
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
        
        Debug.Log($"🌟 Level System: {level.LevelName} completed with {stars} stars!");
    }
    
    /// <summary>
    /// Handle level failure in level system
    /// </summary>
    private void OnLevelSystemFailed(LevelData level)
    {
        hasWon = false;
        SetState(GameState.GameOver);
        
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
        
        Debug.Log($"❌ Level System: {level.LevelName} failed!");
    }
    
    /// <summary>
    /// Handle moves change in level system - update UI
    /// </summary>
    private void OnLevelSystemMovesChanged(int newMoveCount)
    {
        Debug.Log($"🎯 OnLevelSystemMovesChanged: newMoveCount={newMoveCount}, currentDisplayMode={currentDisplayMode}");
        
        // Update the moves UI when LevelManager changes moves
        if (currentDisplayMode == DisplayMode.Moves)
        {
            UpdateMovesUI();
        }
    }
    
    /// <summary>
    /// Handle score change in level system - update UI
    /// </summary>
    private void OnLevelSystemScoreChanged(int newScore)
    {
        Debug.Log($"📊 OnLevelSystemScoreChanged: newScore={newScore}");
        
        // Only update UI if we're not in the middle of a score transfer animation
        // to avoid conflicts with the animation's own UI updates
        if (currentRoundScore == 0)
        {
            UpdateScoreUI();
        }
        else
        {
            Debug.Log($"📊 Skipping UI update during score transfer animation");
        }
    }

    void OnDestroy()
    {
        // Clean up level system event listeners
        if (IsUsingLevelSystem)
        {
            LevelManager.OnLevelCompleted -= OnLevelSystemCompleted;
            LevelManager.OnLevelFailed -= OnLevelSystemFailed;
            LevelManager.OnMovesChanged -= OnLevelSystemMovesChanged;
            LevelManager.OnScoreChanged -= OnLevelSystemScoreChanged;
        }
    }
}