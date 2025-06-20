using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WordScroll.Modifiers;

/// <summary>
/// Comprehensive debug UI for scoring system that shows detailed breakdown of all scoring calculations
/// </summary>
public class ScoringDebugUI : MonoBehaviour
{
    [Header("Debug Panel")]
    [SerializeField] private GameObject debugPanel;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Transform contentParent;
    
    [Header("Debug Controls")]
    [SerializeField] private Button toggleButton;
    [SerializeField] private Button clearLogButton;
    [SerializeField] private Button exportLogButton;
    [SerializeField] private Toggle enableDebugToggle;
    [SerializeField] private Toggle verboseToggle;
    
    [Header("Debug Display")]
    [SerializeField] private GameObject debugEntryPrefab;
    [SerializeField] private TextMeshProUGUI summaryText;
    [SerializeField] private TextMeshProUGUI currentScoreText;
    
    [Header("Visual Settings")]
    [SerializeField] private Color intersectionColor = Color.cyan;
    [SerializeField] private Color baseWordColor = Color.yellow;
    [SerializeField] private Color multiplierColor = Color.magenta;
    [SerializeField] private Color bonusColor = Color.green;
    [SerializeField] private Color finalColor = Color.gold;
    [SerializeField] private Color errorColor = Color.red;
    
    private List<ScoringDebugEntry> debugEntries = new List<ScoringDebugEntry>();
    private StringBuilder logBuilder = new StringBuilder();
    private bool isVisible = false;
    private bool debugEnabled = true;
    private bool verboseMode = false;
    
    public static ScoringDebugUI Instance { get; private set; }
    
    public bool IsDebugEnabled => debugEnabled;
    public bool IsVerboseMode => verboseMode;
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Set the debug tag for event handling
            if (gameObject.tag != "DebugSystem")
                gameObject.tag = "DebugSystem";
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        InitializeUI();
    }
    
    /// <summary>
    /// Handle scoring debug events from the scoring system
    /// </summary>
    public void OnScoringDebugEvent(NumericalScoringData scoringData)
    {
        if (debugEnabled)
        {
            LogScoringBreakdown(scoringData);
        }
    }
    
    private void InitializeUI()
    {
        // Setup initial state
        if (debugPanel != null)
            debugPanel.SetActive(false);
            
        // Setup toggle button
        if (toggleButton != null)
            toggleButton.onClick.AddListener(ToggleDebugPanel);
            
        // Setup control buttons
        if (clearLogButton != null)
            clearLogButton.onClick.AddListener(ClearDebugLog);
            
        if (exportLogButton != null)
            exportLogButton.onClick.AddListener(ExportDebugLog);
            
        // Setup toggles
        if (enableDebugToggle != null)
        {
            enableDebugToggle.isOn = debugEnabled;
            enableDebugToggle.onValueChanged.AddListener(SetDebugEnabled);
        }
        
        if (verboseToggle != null)
        {
            verboseToggle.isOn = verboseMode;
            verboseToggle.onValueChanged.AddListener(SetVerboseMode);
        }
        
        // Initialize summary
        UpdateSummaryText();
    }
    
    /// <summary>
    /// Log a comprehensive scoring breakdown
    /// </summary>
    public void LogScoringBreakdown(NumericalScoringData scoringData)
    {
        if (!debugEnabled) return;
        
        var entry = new ScoringDebugEntry
        {
            timestamp = System.DateTime.Now,
            entryType = ScoringDebugEntry.EntryType.ScoringBreakdown,
            title = $"SCORING BREAKDOWN - {scoringData.words.Count} Word(s)",
            words = scoringData.words.Select(w => w.Word).ToList(),
            intersectionScore = scoringData.intersectionScore,
            baseScore = scoringData.baseWordScore,
            finalScore = scoringData.finalScore,
            steps = scoringData.steps.ToList()
        };
        
        // Build detailed description
        StringBuilder detailBuilder = new StringBuilder();
        
        // Words formed
        detailBuilder.AppendLine($"Words Formed: {string.Join(", ", entry.words)}");
        detailBuilder.AppendLine();
        
        // Step-by-step breakdown
        detailBuilder.AppendLine("STEP-BY-STEP BREAKDOWN:");
        int runningTotal = 0;
        
        foreach (var step in entry.steps)
        {
            string stepDescription = GetStepDescription(step);
            runningTotal += step.scoreValue;
            
            detailBuilder.AppendLine($"  {stepDescription}");
            detailBuilder.AppendLine($"    Running Total: {runningTotal}");
            
            if (verboseMode && step.gridPositions.Count > 0)
            {
                detailBuilder.AppendLine($"    Grid Positions: {string.Join(", ", step.gridPositions.Select(p => $"({p.x},{p.y})"))}");
            }
            
            detailBuilder.AppendLine();
        }
        
        // Final summary
        detailBuilder.AppendLine($"FINAL SCORE: {entry.finalScore} points");
        detailBuilder.AppendLine($"Total Animation Duration: {(entry.steps.LastOrDefault()?.animationDelay ?? 0) + 1.5f}s");
        
        entry.details = detailBuilder.ToString();
        
        AddDebugEntry(entry);
        
        // Log to console as well
        Debug.Log($"[ScoringDebug] {entry.title}\n{entry.details}");
    }
    
    /// <summary>
    /// Log detailed letter-by-letter scoring information
    /// </summary>
    public void LogLetterScoring(List<FoundWordData> words, GameManager gameManager)
    {
        if (!debugEnabled || !verboseMode) return;
        
        var entry = new ScoringDebugEntry
        {
            timestamp = System.DateTime.Now,
            entryType = ScoringDebugEntry.EntryType.LetterBreakdown,
            title = "LETTER-BY-LETTER SCORING",
            words = words.Select(w => w.Word).ToList()
        };
        
        StringBuilder detailBuilder = new StringBuilder();
        
        foreach (var word in words)
        {
            detailBuilder.AppendLine($"Word: '{word.Word}'");
            int wordTotal = 0;
            
            for (int i = 0; i < word.Coordinates.Count; i++)
            {
                var coord = word.Coordinates[i];
                char letter = gameManager.GetLetterAtPosition(coord);
                int letterScore = gameManager.GetPointsForActualScoring(letter);
                wordTotal += letterScore;
                
                detailBuilder.AppendLine($"  [{i}] '{letter}' at ({coord.x},{coord.y}) = {letterScore} points");
            }
            
            detailBuilder.AppendLine($"  Word Total: {wordTotal} points");
            detailBuilder.AppendLine();
        }
        
        entry.details = detailBuilder.ToString();
        AddDebugEntry(entry);
    }
    
    /// <summary>
    /// Log modifier application details
    /// </summary>
    public void LogModifierApplication(List<ModifierCardData> activeModifiers)
    {
        if (!debugEnabled) return;
        
        var entry = new ScoringDebugEntry
        {
            timestamp = System.DateTime.Now,
            entryType = ScoringDebugEntry.EntryType.ModifierInfo,
            title = $"ACTIVE MODIFIERS ({activeModifiers.Count})"
        };
        
        StringBuilder detailBuilder = new StringBuilder();
        
        if (activeModifiers.Count == 0)
        {
            detailBuilder.AppendLine("No active modifiers");
        }
        else
        {
            foreach (var modifier in activeModifiers)
            {
                detailBuilder.AppendLine($"Modifier: {modifier.cardName}");
                detailBuilder.AppendLine($"  Type: {modifier.effectType}");
                detailBuilder.AppendLine($"  Card Type: {modifier.cardType}");
                
                // Detailed effect information
                switch (modifier.effectType)
                {
                    case ModifierEffectType.GeneralScoreBonusAndMoveReduction:
                        if (modifier.generalScoreMultiplier > 1f)
                            detailBuilder.AppendLine($"  Score Multiplier: ×{modifier.generalScoreMultiplier:F1}");
                        if (modifier.moveReductionPercentage > 0)
                            detailBuilder.AppendLine($"  Move Reduction: {modifier.moveReductionPercentage:F1}%");
                        break;
                        
                    case ModifierEffectType.SpecificWordLengthScoreBonus:
                        detailBuilder.AppendLine($"  Target Word Length: {modifier.targetWordLength}");
                        detailBuilder.AppendLine($"  Score Multiplier: ×{modifier.wordLengthScoreMultiplier}");
                        break;
                        
                    case ModifierEffectType.VowelCountBonus:
                        detailBuilder.AppendLine($"  Min Vowel Count: {modifier.minVowelCount}");
                        detailBuilder.AppendLine($"  Bonus Points: +{modifier.vowelBonusPoints}");
                        break;
                }
                
                detailBuilder.AppendLine();
            }
        }
        
        entry.details = detailBuilder.ToString();
        AddDebugEntry(entry);
    }
    
    /// <summary>
    /// Log error or warning information
    /// </summary>
    public void LogError(string title, string details)
    {
        if (!debugEnabled) return;
        
        var entry = new ScoringDebugEntry
        {
            timestamp = System.DateTime.Now,
            entryType = ScoringDebugEntry.EntryType.Error,
            title = $"ERROR: {title}",
            details = details
        };
        
        AddDebugEntry(entry);
        Debug.LogError($"[ScoringDebug] {entry.title}: {entry.details}");
    }
    
    private void AddDebugEntry(ScoringDebugEntry entry)
    {
        debugEntries.Add(entry);
        
        // Keep only last 100 entries
        if (debugEntries.Count > 100)
        {
            debugEntries.RemoveAt(0);
        }
        
        // Add to log builder
        logBuilder.AppendLine($"[{entry.timestamp:HH:mm:ss}] {entry.title}");
        logBuilder.AppendLine(entry.details);
        logBuilder.AppendLine("".PadRight(50, '-'));
        
        // Update UI if visible
        if (isVisible)
        {
            RefreshDebugDisplay();
        }
        
        UpdateSummaryText();
    }
    
    private void RefreshDebugDisplay()
    {
        if (contentParent == null || debugEntryPrefab == null) return;
        
        // Clear existing entries
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }
        
        // Create new entries (show last 20)
        var entriesToShow = debugEntries.TakeLast(20).ToList();
        
        foreach (var entry in entriesToShow)
        {
            CreateDebugEntryUI(entry);
        }
        
        // Scroll to bottom
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
    
    private void CreateDebugEntryUI(ScoringDebugEntry entry)
    {
        GameObject entryObj = Instantiate(debugEntryPrefab, contentParent);
        var entryUI = entryObj.GetComponent<ScoringDebugEntryUI>();
        
        if (entryUI == null)
        {
            entryUI = entryObj.AddComponent<ScoringDebugEntryUI>();
        }
        
        Color entryColor = GetColorForEntryType(entry.entryType);
        entryUI.Setup(entry, entryColor);
    }
    
    private Color GetColorForEntryType(ScoringDebugEntry.EntryType entryType)
    {
        return entryType switch
        {
            ScoringDebugEntry.EntryType.ScoringBreakdown => baseWordColor,
            ScoringDebugEntry.EntryType.LetterBreakdown => intersectionColor,
            ScoringDebugEntry.EntryType.ModifierInfo => multiplierColor,
            ScoringDebugEntry.EntryType.Error => errorColor,
            _ => Color.white
        };
    }
    
    private string GetStepDescription(ScoreStep step)
    {
        return step.stepType switch
        {
            ScoreStep.StepType.IntersectingLetters => $"Intersecting Letters: {step.scoreValue} points",
            ScoreStep.StepType.WordBase => $"Word Base Score: +{step.scoreValue} points",
            ScoreStep.StepType.Multiplier => $"Multiplier Applied: {step.displayText} ({step.scoreValue} additional points)",
            ScoreStep.StepType.AdditiveBonus => $"Additive Bonus: {step.displayText} points",
            ScoreStep.StepType.Final => $"Final Score: {step.scoreValue} points",
            _ => $"Unknown Step: {step.scoreValue} points"
        };
    }
    
    private void UpdateSummaryText()
    {
        if (summaryText == null) return;
        
        int totalEntries = debugEntries.Count;
        int errors = debugEntries.Count(e => e.entryType == ScoringDebugEntry.EntryType.Error);
        int scoringBreakdowns = debugEntries.Count(e => e.entryType == ScoringDebugEntry.EntryType.ScoringBreakdown);
        
        summaryText.text = $"Debug Entries: {totalEntries} | Errors: {errors} | Scoring Breakdowns: {scoringBreakdowns}";
    }
    
    public void ToggleDebugPanel()
    {
        isVisible = !isVisible;
        
        if (debugPanel != null)
        {
            debugPanel.SetActive(isVisible);
            
            if (isVisible)
            {
                RefreshDebugDisplay();
            }
        }
    }
    
    public void SetDebugEnabled(bool enabled)
    {
        debugEnabled = enabled;
    }
    
    public void SetVerboseMode(bool verbose)
    {
        verboseMode = verbose;
    }
    
    public void ClearDebugLog()
    {
        debugEntries.Clear();
        logBuilder.Clear();
        
        if (isVisible)
        {
            RefreshDebugDisplay();
        }
        
        UpdateSummaryText();
    }
    
    public void ExportDebugLog()
    {
        string logContent = logBuilder.ToString();
        string filename = $"ScoringDebugLog_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt";
        
        // In a real game, you might want to save to persistent data path
        string filepath = System.IO.Path.Combine(Application.persistentDataPath, filename);
        
        try
        {
            System.IO.File.WriteAllText(filepath, logContent);
            Debug.Log($"Debug log exported to: {filepath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to export debug log: {e.Message}");
        }
    }
    
    /// <summary>
    /// Update current score display
    /// </summary>
    public void UpdateCurrentScore(int score)
    {
        if (currentScoreText != null)
        {
            currentScoreText.text = $"Current Score: {score}";
        }
    }
}

/// <summary>
/// Data structure for debug entries
/// </summary>
[System.Serializable]
public class ScoringDebugEntry
{
    public enum EntryType
    {
        ScoringBreakdown,
        LetterBreakdown,
        ModifierInfo,
        Error,
        Warning,
        Info
    }
    
    public System.DateTime timestamp;
    public EntryType entryType;
    public string title;
    public string details;
    public List<string> words = new List<string>();
    public int intersectionScore;
    public int baseScore;
    public int finalScore;
    public List<ScoreStep> steps = new List<ScoreStep>();
}
