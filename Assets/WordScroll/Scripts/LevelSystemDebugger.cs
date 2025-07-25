using UnityEngine;

[System.Serializable]
public class LevelSystemDebugger : MonoBehaviour
{
    [Header("Debug Testing")]
    [SerializeField] private bool testLevelCompletion = false;
    [SerializeField] private bool testLevelFailure = false;
    [SerializeField] private int testScore = 100;
    
    // Helper property to reduce redundant level manager access
    private LevelManager levelManager => LevelManager.Instance;
    
    void Update()
    {
        if (testLevelCompletion)
        {
            testLevelCompletion = false;
            TestLevelComplete();
        }
        
        if (testLevelFailure)
        {
            testLevelFailure = false;
            TestLevelFail();
        }
    }
    
    [ContextMenu("Test Level Complete")]
    public void TestLevelComplete()
    {
        if (levelManager != null && levelManager.CurrentLevel != null)
        {
            // Simulate level completion
            levelManager.AddScore(testScore);
            Debug.Log($"🧪 Testing level completion with score {testScore}");
        }
        else
        {
            Debug.LogError("❌ LevelManager not found or no current level!");
        }
    }
    
    [ContextMenu("Test Level Fail")]  
    public void TestLevelFail()
    {
        if (levelManager != null)
        {
            Debug.Log("🧪 Testing level failure");
            // This would trigger when moves run out or other failure condition
        }
        else
        {
            Debug.LogError("❌ LevelManager not found!");
        }
    }
    
    [ContextMenu("Check Level System Status")]
    public void CheckSystemStatus()
    {
        Debug.Log("=== LEVEL SYSTEM STATUS ===");
        
        // Check LevelManager
        if (levelManager != null)
        {
            Debug.Log($"✅ LevelManager found");
            Debug.Log($"📊 Current Level: {levelManager.CurrentLevel?.LevelName ?? "None"}");
            Debug.Log($"🎯 Score: {levelManager.CurrentScore}/{levelManager.CurrentLevel?.TargetScore}");
            Debug.Log($"🎮 Moves: {levelManager.CurrentMoves}/{levelManager.CurrentLevel?.MaxMoves}");
        }
        else
        {
            Debug.LogError("❌ LevelManager not found!");
        }
        
        // Check GameManager
        if (GameManager.instance != null)
        {
            Debug.Log($"🎮 GameManager found, Using Level System: {GameManager.instance.IsUsingLevelSystem}");
        }
        else
        {
            Debug.LogError("❌ GameManager not found!");
        }
        
        // Check Game Over UI
        var gameOverUI = FindFirstObjectByType<GameOverUIController>();
        if (gameOverUI != null)
        {
            Debug.Log($"🖥️ GameOverUIController found");
        }
        else
        {
            Debug.LogError("❌ GameOverUIController not found!");
        }
    }
}
