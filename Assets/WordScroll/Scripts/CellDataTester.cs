using UnityEngine;

/// <summary>
/// Helper script to test CellData system setup
/// Add to a GameObject in scene for testing
/// </summary>
public class CellDataTester : MonoBehaviour
{
    [Header("Test Components")]
    public CellTypeManager cellTypeManager;
    public WordGridManager wordGridManager; 
    public WordValidator wordValidator;
    
    [Header("Test Controls")]
    [SerializeField] private bool testOnStart = true;
    
    void Start()
    {
        if (testOnStart)
        {
            TestCellDataSystem();
        }
    }
    
    [ContextMenu("Test Cell Data System")]
    public void TestCellDataSystem()
    {
        Debug.Log("=== CELL DATA SYSTEM TEST ===");
        
        // Test 1: Create different cell types
        CellData letterCell = CellData.CreateLetterCell('A');
        CellData blankCell = CellData.CreateBlankCell();
        
        Debug.Log($"Letter Cell: {letterCell}");
        Debug.Log($"Blank Cell: {blankCell}");
        
        // Test 2: Check validation participation
        Debug.Log($"Letter participates in validation: {letterCell.IsValidationCell}");
        Debug.Log($"Blank participates in validation: {blankCell.IsValidationCell}");
        
        // Test 3: Test new leading/trailing blank validation
        TestBlankValidationScenarios();
        
        // Test 4: Check managers
        if (cellTypeManager != null)
            Debug.Log("✅ CellTypeManager found");
        else
            Debug.LogError("❌ CellTypeManager missing!");
            
        if (wordGridManager != null)
            Debug.Log("✅ WordGridManager found");
        else
            Debug.LogError("❌ WordGridManager missing!");
            
        if (wordValidator != null)
            Debug.Log("✅ WordValidator found");  
        else
            Debug.LogError("❌ WordValidator missing!");
            
        Debug.Log("=== TEST COMPLETE ===");
    }
    
    private void TestBlankValidationScenarios()
    {
        Debug.Log("=== TESTING BLANK VALIDATION SCENARIOS ===");
        
        // These scenarios should be valid with the new system:
        string[] validScenarios = {
            "CATS ",     // [C][A][T][S][_] - trailing blank
            " CATS",     // [_][C][A][T][S] - leading blank  
            "  CAT",     // [_][_][C][A][T] - leading blanks
            "CAT  ",     // [C][A][T][_][_] - trailing blanks
            " CAT ",     // [_][C][A][T][_] - leading and trailing (5 cells total)
        };
        
        // These scenarios should be invalid:
        string[] invalidScenarios = {
            "CA T",      // [C][A][_][T] - blank in middle
            "C A T",     // [C][_][A][_][T] - blanks in middle
            "WO RD",     // [W][O][_][R][D] - blank in middle
            "     ",     // All blanks
        };
        
        Debug.Log("Testing VALID scenarios:");
        foreach (string scenario in validScenarios)
        {
            bool isValid = TestSequenceStructure(scenario);
            string word = TestExtractWord(scenario);
            Debug.Log($"  '{scenario.Replace(' ', '_')}' → Valid: {isValid}, Word: '{word}'");
        }
        
        Debug.Log("Testing INVALID scenarios:");
        foreach (string scenario in invalidScenarios)
        {
            bool isValid = TestSequenceStructure(scenario);
            string word = TestExtractWord(scenario);
            Debug.Log($"  '{scenario.Replace(' ', '_')}' → Valid: {isValid}, Word: '{word}'");
        }
    }
    
    // Helper methods to test the private validation methods through reflection
    private bool TestSequenceStructure(string sequence)
    {
        if (wordValidator == null) return false;
        
        var method = typeof(WordValidator).GetMethod("HasValidSequenceStructure", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method != null)
        {
            return (bool)method.Invoke(wordValidator, new object[] { sequence });
        }
        return false;
    }
    
    private string TestExtractWord(string sequence)
    {
        if (wordValidator == null) return "";
        
        var method = typeof(WordValidator).GetMethod("ExtractWordFromSequence", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method != null)
        {
            return (string)method.Invoke(wordValidator, new object[] { sequence });
        }
        return "";
    }
}
