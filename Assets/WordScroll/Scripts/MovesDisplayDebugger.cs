using UnityEngine;

/// <summary>
/// Debug helper to test moves and score display issues
/// </summary>
public class MovesDisplayDebugger : MonoBehaviour
{
    [Header("Manual Testing")]
    [SerializeField] private bool debugMoves = true;
    
    void Update()
    {
        if (debugMoves && Input.GetKeyDown(KeyCode.M))
        {
            DebugMovesState();
        }
        
        if (debugMoves && Input.GetKeyDown(KeyCode.R))
        {
            RefreshGameManagerUI();
        }
        
        if (debugMoves && Input.GetKeyDown(KeyCode.S))
        {
            DebugScoreState();
        }
        
        if (debugMoves && Input.GetKeyDown(KeyCode.F))
        {
            ForceCorrectScore();
        }
    }
    
    [ContextMenu("Debug Moves State")]
    void DebugMovesState()
    {
        Debug.Log("=== MOVES DEBUG STATE ===");
        
        // Check GameManager
        if (GameManager.instance != null)
        {
            Debug.Log($"GameManager Found: ✅");
            Debug.Log($"IsUsingLevelSystem: {GameManager.instance.IsUsingLevelSystem}");
            Debug.Log($"CurrentDisplayMode: {GameManager.instance.CurrentGameDisplayMode}");
            Debug.Log($"CurrentMovesRemaining: {GameManager.instance.CurrentMovesRemaining}");
            Debug.Log($"CurrentScore: {GameManager.instance.CurrentScore}");
        }
        else
        {
            Debug.LogError("GameManager.instance is NULL!");
        }
        
        // Check LevelManager
        if (LevelManager.Instance != null)
        {
            Debug.Log($"LevelManager Found: ✅");
            Debug.Log($"CurrentLevel: {(LevelManager.Instance.CurrentLevel != null ? LevelManager.Instance.CurrentLevel.LevelName : "NULL")}");
            if (LevelManager.Instance.CurrentLevel != null)
            {
                Debug.Log($"Level MaxMoves: {LevelManager.Instance.CurrentLevel.MaxMoves}");
                Debug.Log($"Level CurrentMoves: {LevelManager.Instance.CurrentMoves}");
                Debug.Log($"Level RemainingMoves: {LevelManager.Instance.CurrentLevel.GetRemainingMoves(LevelManager.Instance.CurrentMoves)}");
            }
        }
        else
        {
            Debug.LogError("LevelManager.Instance is NULL!");
        }
        
        Debug.Log("=== END MOVES DEBUG ===");
    }
    
    [ContextMenu("Debug Score State")]
    void DebugScoreState()
    {
        Debug.Log("=== SCORE DEBUG STATE ===");
        
        // Check GameManager
        if (GameManager.instance != null)
        {
            Debug.Log($"GameManager Found: ✅");
            Debug.Log($"IsUsingLevelSystem: {GameManager.instance.IsUsingLevelSystem}");
            Debug.Log($"CurrentScore (property): {GameManager.instance.CurrentScore}");
            
            // Check UI components
            var scoreTextFields = FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None);
            foreach (var textField in scoreTextFields)
            {
                if (textField.name.ToLower().Contains("score"))
                {
                    Debug.Log($"Score UI \"{textField.name}\": \"{textField.text}\" (Active: {textField.gameObject.activeInHierarchy})");
                }
            }
        }
        else
        {
            Debug.LogError("GameManager.instance is NULL!");
        }
        
        // Check LevelManager
        if (LevelManager.Instance != null)
        {
            Debug.Log($"LevelManager Found: ✅");
            Debug.Log($"LevelManager.CurrentScore: {LevelManager.Instance.CurrentScore}");
            Debug.Log($"CurrentLevel: {(LevelManager.Instance.CurrentLevel != null ? LevelManager.Instance.CurrentLevel.LevelName : "NULL")}");
            if (LevelManager.Instance.CurrentLevel != null)
            {
                Debug.Log($"Level TargetScore: {LevelManager.Instance.CurrentLevel.TargetScore}");
            }
        }
        else
        {
            Debug.LogError("LevelManager.Instance is NULL!");
        }
        
        Debug.Log("=== END SCORE DEBUG ===");
    }
    
    [ContextMenu("Refresh GameManager UI")]
    void RefreshGameManagerUI()
    {
        if (GameManager.instance != null)
        {
            GameManager.instance.RefreshGameUI();
            Debug.Log("🔄 Forced GameManager UI refresh");
        }
        else
        {
            Debug.LogError("GameManager.instance is NULL!");
        }
    }
    
    [ContextMenu("Force Correct Score")]
    void ForceCorrectScore()
    {
        if (GameManager.instance != null)
        {
            GameManager.instance.ForceCorrectScoreDisplay();
        }
        else
        {
            Debug.LogError("GameManager.instance is NULL!");
        }
    }
    
    [ContextMenu("Start Level 0")]
    void StartFirstLevel()
    {
        if (LevelManager.Instance != null)
        {
            bool started = LevelManager.Instance.StartLevel(0);
            Debug.Log($"🎮 StartLevel(0) result: {started}");
            
            // Refresh UI after starting level
            if (started && GameManager.instance != null)
            {
                GameManager.instance.RefreshGameUI();
            }
        }
        else
        {
            Debug.LogError("LevelManager.Instance is NULL!");
        }
    }
}
