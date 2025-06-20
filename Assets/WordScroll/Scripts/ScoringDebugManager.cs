using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Integration manager for the scoring debug system - shows how to set up and use all debug components
/// </summary>
public class ScoringDebugManager : MonoBehaviour
{
    [Header("Debug System Setup")]
    [SerializeField] private ScoringDebugUI debugUI;
    [SerializeField] private DebugToggleButton toggleButton;
    
    [Header("Debug Settings")]
    [SerializeField] private bool enableVerboseLogging = true;
    [SerializeField] private bool enableRichTextConsole = true;
    [SerializeField] private bool showDebugInRelease = false;
    
    [Header("Test Controls (Development Only)")]
    [SerializeField] private Button testScoringButton;
    [SerializeField] private Button testModifierButton;
    [SerializeField] private Button clearLogButton;
    [SerializeField] private Button exportLogButton;
    
    private void Start()
    {
        SetupDebugSystem();
        SetupTestControls();
    }
    
    private void SetupDebugSystem()
    {
        // Configure enhanced logging
        ScoringDebugLogger.VerboseLogging = enableVerboseLogging;
        ScoringDebugLogger.UseRichText = enableRichTextConsole;
        
        // Ensure debug UI has proper tag
        if (debugUI != null && debugUI.gameObject.tag != "DebugSystem")
        {
            debugUI.gameObject.tag = "DebugSystem";
        }
        
        // Hide debug UI in release builds unless explicitly enabled
        #if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        if (!showDebugInRelease && debugUI != null)
        {
            debugUI.gameObject.SetActive(false);
        }
        #endif
        
        Debug.Log("[ScoringDebugManager] Debug system initialized");
        LogSystemStatus();
    }
    
    private void SetupTestControls()
    {
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Setup test buttons for development
        if (testScoringButton != null)
            testScoringButton.onClick.AddListener(TestScoringDebug);
            
        if (testModifierButton != null)
            testModifierButton.onClick.AddListener(TestModifierDebug);
            
        if (clearLogButton != null)
            clearLogButton.onClick.AddListener(ClearDebugLog);
            
        if (exportLogButton != null)
            exportLogButton.onClick.AddListener(ExportDebugLog);
        #else
        // Hide test controls in release builds
        if (testScoringButton != null) testScoringButton.gameObject.SetActive(false);
        if (testModifierButton != null) testModifierButton.gameObject.SetActive(false);
        if (clearLogButton != null) clearLogButton.gameObject.SetActive(false);
        if (exportLogButton != null) exportLogButton.gameObject.SetActive(false);
        #endif
    }
    
    private void LogSystemStatus()
    {
        Debug.Log($"[ScoringDebugManager] System Status:\n" +
                  $"  • Verbose Logging: {ScoringDebugLogger.VerboseLogging}\n" +
                  $"  • Rich Text Console: {ScoringDebugLogger.UseRichText}\n" +
                  $"  • Debug UI Available: {debugUI != null}\n" +
                  $"  • Toggle Button Available: {toggleButton != null}\n" +
                  $"  • Show in Release: {showDebugInRelease}");
    }
    
    // Test methods for development
    #if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void TestScoringDebug()
    {
        Debug.Log("[ScoringDebugManager] Testing scoring debug output...");
        
        // Create mock scoring data for testing
        var mockWords = new System.Collections.Generic.List<FoundWordData>
        {
            new FoundWordData 
            { 
                Word = "TEST", 
                Coordinates = new System.Collections.Generic.List<Vector2Int> 
                { 
                    new Vector2Int(0, 0), 
                    new Vector2Int(1, 0), 
                    new Vector2Int(2, 0), 
                    new Vector2Int(3, 0) 
                } 
            },
            new FoundWordData 
            { 
                Word = "HELLO", 
                Coordinates = new System.Collections.Generic.List<Vector2Int> 
                { 
                    new Vector2Int(0, 1), 
                    new Vector2Int(1, 1), 
                    new Vector2Int(2, 1), 
                    new Vector2Int(3, 1), 
                    new Vector2Int(4, 1) 
                } 
            }
        };
        
        var gameManager = GameObject.FindFirstObjectByType<GameManager>();
        if (gameManager != null)
        {
            var scoringData = NumericalScoringData.GenerateFromWords(mockWords, gameManager);
            Debug.Log($"[ScoringDebugManager] Test scoring completed. Final score: {scoringData.finalScore}");
        }
        else
        {
            Debug.LogWarning("[ScoringDebugManager] GameManager not found for test");
        }
    }
    
    private void TestModifierDebug()
    {
        Debug.Log("[ScoringDebugManager] Testing modifier debug output...");
        
        var modifierManager = WordScroll.Modifiers.ModifierManager.Instance;
        if (modifierManager != null)
        {
            var activeModifiers = modifierManager.GetAllActiveModifiers();
            
            if (debugUI != null)
            {
                debugUI.LogModifierApplication(activeModifiers);
            }
            
            // Also use the enhanced logger if available
            try
            {
                ScoringDebugLogger.LogModifierInfo(activeModifiers);
            }
            catch (System.Exception)
            {
                Debug.Log("[ScoringDebugManager] Enhanced logger not available");
            }
        }
        else
        {
            Debug.LogWarning("[ScoringDebugManager] ModifierManager not found for test");
        }
    }
    
    private void ClearDebugLog()
    {
        if (debugUI != null)
        {
            debugUI.ClearDebugLog();
            Debug.Log("[ScoringDebugManager] Debug log cleared");
        }
    }
    
    private void ExportDebugLog()
    {
        if (debugUI != null)
        {
            debugUI.ExportDebugLog();
        }
    }
    #endif
    
    /// <summary>
    /// Toggle verbose logging at runtime
    /// </summary>
    public void ToggleVerboseLogging()
    {
        enableVerboseLogging = !enableVerboseLogging;
        ScoringDebugLogger.VerboseLogging = enableVerboseLogging;
        
        if (debugUI != null)
        {
            debugUI.SetVerboseMode(enableVerboseLogging);
        }
        
        Debug.Log($"[ScoringDebugManager] Verbose logging: {enableVerboseLogging}");
    }
    
    /// <summary>
    /// Toggle rich text console output
    /// </summary>
    public void ToggleRichTextConsole()
    {
        enableRichTextConsole = !enableRichTextConsole;
        ScoringDebugLogger.UseRichText = enableRichTextConsole;
        
        Debug.Log($"[ScoringDebugManager] Rich text console: {enableRichTextConsole}");
    }
    
    /// <summary>
    /// Show debug UI
    /// </summary>
    public void ShowDebugUI()
    {
        if (debugUI != null)
        {
            debugUI.ToggleDebugPanel();
        }
    }
    
    /// <summary>
    /// Log current game state for debugging
    /// </summary>
    public void LogGameState()
    {
        var gameManager = GameObject.FindFirstObjectByType<GameManager>();
        if (gameManager != null)
        {
            Debug.Log($"[ScoringDebugManager] Game State Debug:\n" +
                      $"  • Current Score: {gameManager.CurrentScore}\n" +
                      $"  • Has Won: {gameManager.HasWon}\n" +
                      $"  • Game State: {gameManager.CurrentStatePublic}\n" +
                      $"  • Display Mode: {gameManager.CurrentGameDisplayMode}");
        }
    }
}
