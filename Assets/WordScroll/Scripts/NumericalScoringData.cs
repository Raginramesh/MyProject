using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using WordScroll.Modifiers;

/// <summary>
/// Represents a single numerical scoring step
/// </summary>
[System.Serializable]
public class ScoreStep
{
    public enum StepType
    {
        IntersectingLetters,    // Shared letter scores
        WordBase,               // Base word score
        Multiplier,             // Multiplier effects (×2, ×3)
        AdditiveBonus,          // Additive bonuses (+5, +10)
        Final                   // Final total
    }

    public StepType stepType;
    public int scoreValue;
    public string displayText;      // e.g., "2", "+6", "×2", "+5", "14"
    public float animationDelay;
    public Color highlightColor = Color.white;
    public List<Vector2Int> gridPositions = new List<Vector2Int>(); // For grid highlighting
    
    public ScoreStep(StepType type, int value, string text, float delay = 0f)
    {
        stepType = type;
        scoreValue = value;
        displayText = text;
        animationDelay = delay;
    }
}

/// <summary>
/// Contains all data for numerical score breakdown calculation and display
/// </summary>
[System.Serializable]
public class NumericalScoringData
{
    public List<FoundWordData> words = new List<FoundWordData>();
    public List<ScoreStep> steps = new List<ScoreStep>();
    public int intersectionScore = 0;
    public int baseWordScore = 0;
    public int finalScore = 0;
    
    /// <summary>
    /// Generates numerical scoring data from found words
    /// </summary>
    public static NumericalScoringData GenerateFromWords(List<FoundWordData> foundWords, GameManager gameManager)
    {
        var scoringData = new NumericalScoringData();
        scoringData.words = foundWords;
        
        // Enhanced console logging
        Debug.Log("╔══════════════════════════════════════════════════════════════════╗");
        Debug.Log("║                      SCORING BREAKDOWN DEBUG                    ║");
        Debug.Log("╚══════════════════════════════════════════════════════════════════╝");
        Debug.Log($"📝 Processing {foundWords.Count} word(s): {string.Join(", ", foundWords.Select(w => $"'{w.Word}'"))}");
        
        // Log letter-by-letter breakdown first
        LogLetterByLetterBreakdown(foundWords, gameManager);
        
        // Find shared letters
        scoringData.CalculateIntersectionScore(foundWords, gameManager);
        
        // Calculate base word scores
        scoringData.CalculateBaseWordScore(foundWords, gameManager);
        
        // Generate scoring steps
        scoringData.GenerateScoringSteps(gameManager);
        
        // Log comprehensive summary
        LogScoringSummary(scoringData);
        
        Debug.Log("╔══════════════════════════════════════════════════════════════════╗");
        Debug.Log($"║ 🎯 FINAL SCORE: {scoringData.finalScore.ToString().PadLeft(3)} POINTS {new string(' ', 37)}║");
        Debug.Log("╚══════════════════════════════════════════════════════════════════╝");
        
        // Send debug event for any listeners
        scoringData.SendDebugEvent();
        
        return scoringData;
    }
    
    /// <summary>
    /// Send debug event that can be caught by debug UI systems
    /// </summary>
    private void SendDebugEvent()
    {
        try
        {
            // Use a simple event system to avoid direct references
            GameObject.FindGameObjectWithTag("DebugSystem")?.SendMessage("OnScoringDebugEvent", this, SendMessageOptions.DontRequireReceiver);
        }
        catch (UnityException ex)
        {
            // Handle missing tag gracefully - no need to spam console since this is optional
            if (!ex.Message.Contains("Tag: DebugSystem is not defined"))
            {
                Debug.LogWarning($"[NumericalScoringData] Debug system error: {ex.Message}");
            }
        }
    }
    
    private void CalculateIntersectionScore(List<FoundWordData> foundWords, GameManager gameManager)
    {
        Dictionary<Vector2Int, int> positionCount = new Dictionary<Vector2Int, int>();
        
        Debug.Log("┌──────────────────────────────────────────────────────────────────┐");
        Debug.Log("│                  ANALYZING INTERSECTING LETTERS                 │");
        Debug.Log("└──────────────────────────────────────────────────────────────────┘");
        
        // Count letter position usage
        foreach (var word in foundWords)
        {
            Debug.Log($"🔍 Word '{word.Word}' uses positions: {string.Join(", ", word.Coordinates.Select(c => $"({c.x},{c.y})"))}");
            foreach (var coord in word.Coordinates)
            {
                if (positionCount.ContainsKey(coord))
                    positionCount[coord]++;
                else
                    positionCount[coord] = 1;
            }
        }
        
        // Calculate score for shared positions only
        intersectionScore = 0;
        var sharedPositions = positionCount.Where(kvp => kvp.Value > 1).ToList();
        
        if (sharedPositions.Count > 0)
        {
            Debug.Log("🔗 Found intersecting letters:");
            foreach (var kvp in sharedPositions)
            {
                char letter = gameManager.GetLetterAtPosition(kvp.Key);
                int letterScore = gameManager.GetPointsForActualScoring(letter);
                intersectionScore += letterScore;
                
                Debug.Log($"   • '{letter}' at ({kvp.Key.x},{kvp.Key.y}) shared by {kvp.Value} words = {letterScore} points");
            }
        }
        else
        {
            Debug.Log("❌ No intersecting letters found");
        }
        
        Debug.Log($"📊 Total Intersection Score: {intersectionScore} points");
        Debug.Log("");
    }
    
    private void CalculateBaseWordScore(List<FoundWordData> foundWords, GameManager gameManager)
    {
        Debug.Log("┌──────────────────────────────────────────────────────────────────┐");
        Debug.Log("│                   CALCULATING BASE WORD SCORES                  │");
        Debug.Log("└──────────────────────────────────────────────────────────────────┘");
        
        baseWordScore = 0;
        foreach (var word in foundWords)
        {
            int wordScore = 0;
            string letterBreakdown = "";
            
            foreach (var coord in word.Coordinates)
            {
                char letter = gameManager.GetLetterAtPosition(coord);
                int letterScore = gameManager.GetPointsForActualScoring(letter);
                baseWordScore += letterScore;
                wordScore += letterScore;
                
                letterBreakdown += $"{letter}({letterScore}) ";
            }
            
            Debug.Log($"📝 Word '{word.Word}': {letterBreakdown.Trim()} = {wordScore} points");
        }
        
        Debug.Log($"📊 Total Base Word Score: {baseWordScore} points");
        Debug.Log("");
    }
    
    private void GenerateScoringSteps(GameManager gameManager)
    {
        Debug.Log("┌──────────────────────────────────────────────────────────────────┐");
        Debug.Log("│                    GENERATING SCORING STEPS                     │");
        Debug.Log("└──────────────────────────────────────────────────────────────────┘");
        
        steps.Clear();
        float currentDelay = 0f;
        int runningScore = 0;
        
        // Step 1: Intersection score (if any)
        if (intersectionScore > 0)
        {
            var intersectionStep = new ScoreStep(ScoreStep.StepType.IntersectingLetters, 
                intersectionScore, intersectionScore.ToString(), currentDelay)
            {
                highlightColor = Color.cyan
            };
            
            // Add grid positions for shared letters
            intersectionStep.gridPositions = GetSharedLetterPositions();
            steps.Add(intersectionStep);
            runningScore += intersectionScore;
            currentDelay += 0.6f;
            
            Debug.Log($"✅ Added Intersection Step: {intersectionScore} points (Running total: {runningScore})");
        }
        else
        {
            Debug.Log("ℹ️  No intersection step needed (no shared letters)");
        }
        
        // Step 2: Base word score
        int displayWordScore = baseWordScore - intersectionScore; // Don't double-count intersection
        if (displayWordScore > 0)
        {
            var wordStep = new ScoreStep(ScoreStep.StepType.WordBase, 
                displayWordScore, $"+{displayWordScore}", currentDelay)
            {
                highlightColor = Color.yellow
            };
            
            // Add all word positions
            foreach (var word in words)
            {
                wordStep.gridPositions.AddRange(word.Coordinates);
            }
            
            steps.Add(wordStep);
            runningScore += displayWordScore;
            currentDelay += 0.6f;
            
            Debug.Log($"✅ Added Word Base Step: +{displayWordScore} points (Running total: {runningScore})");
        }
        
        // Step 3: Apply modifiers
        Debug.Log("🎛️  Checking for active modifiers...");
        ApplyModifiers(gameManager, ref runningScore, ref currentDelay);
        
        // Step 4: Final score
        finalScore = runningScore;
        var finalStep = new ScoreStep(ScoreStep.StepType.Final, 
            finalScore, finalScore.ToString(), currentDelay)
        {
            highlightColor = Color.gold
        };
        steps.Add(finalStep);
        
        Debug.Log($"🏁 Added Final Step: {finalScore} points");
        Debug.Log($"📊 Generated {steps.Count} scoring steps with total animation duration: {currentDelay + 1.5f:F1}s");
        Debug.Log("");
    }
    
    private void ApplyModifiers(GameManager gameManager, ref int runningScore, ref float currentDelay)
    {
        var modifierManager = ModifierManager.Instance;
        if (modifierManager == null) return;
        
        var activeModifiers = modifierManager.GetAllActiveModifiers();
        
        foreach (var modifier in activeModifiers)
        {
            // Handle multipliers first
            if (modifier.effectType == ModifierEffectType.GeneralScoreBonusAndMoveReduction ||
                modifier.effectType == ModifierEffectType.SpecificWordLengthScoreBonus)
            {
                float multiplier = GetMultiplierForModifier(modifier);
                if (multiplier > 1f)
                {
                    int oldScore = runningScore;
                    runningScore = Mathf.RoundToInt(runningScore * multiplier);
                    
                    var multiplierStep = new ScoreStep(ScoreStep.StepType.Multiplier, 
                        runningScore - oldScore, $"×{multiplier}", currentDelay)
                    {
                        highlightColor = Color.magenta
                    };
                    steps.Add(multiplierStep);
                    currentDelay += 0.6f;
                }
            }
            
            // Handle additive bonuses
            int additiveBonus = GetAdditiveBonusForModifier(modifier);
            if (additiveBonus > 0)
            {
                var bonusStep = new ScoreStep(ScoreStep.StepType.AdditiveBonus, 
                    additiveBonus, $"+{additiveBonus}", currentDelay)
                {
                    highlightColor = Color.green
                };
                steps.Add(bonusStep);
                runningScore += additiveBonus;
                currentDelay += 0.6f;
            }
        }
    }
    
    private List<Vector2Int> GetSharedLetterPositions()
    {
        Dictionary<Vector2Int, int> positionCount = new Dictionary<Vector2Int, int>();
        
        foreach (var word in words)
        {
            foreach (var coord in word.Coordinates)
            {
                if (positionCount.ContainsKey(coord))
                    positionCount[coord]++;
                else
                    positionCount[coord] = 1;
            }
        }
        
        return positionCount.Where(kvp => kvp.Value > 1).Select(kvp => kvp.Key).ToList();
    }
    
    private float GetMultiplierForModifier(ModifierCardData modifier)
    {
        switch (modifier.effectType)
        {
            case ModifierEffectType.GeneralScoreBonusAndMoveReduction:
                return modifier.generalScoreMultiplier;
                
            case ModifierEffectType.SpecificWordLengthScoreBonus:
                // Check if any word matches the target length
                if (words.Any(w => w.Word.Length == modifier.targetWordLength))
                    return modifier.wordLengthScoreMultiplier;
                break;
        }
        return 1f;
    }
    
    private int GetAdditiveBonusForModifier(ModifierCardData modifier)
    {
        switch (modifier.effectType)
        {
            case ModifierEffectType.VowelCountBonus:
                int totalBonus = 0;
                foreach (var word in words)
                {
                    int vowelCount = word.Word.Count(c => "AEIOUaeiou".Contains(c));
                    if (vowelCount >= modifier.minVowelCount)
                    {
                        totalBonus += modifier.vowelBonusPoints;
                    }
                }
                return totalBonus;
        }
        return 0;
    }
    
    /// <summary>
    /// Log detailed letter-by-letter scoring breakdown
    /// </summary>
    private static void LogLetterByLetterBreakdown(List<FoundWordData> words, GameManager gameManager)
    {
        Debug.Log("┌──────────────────────────────────────────────────────────────────┐");
        Debug.Log("│                    LETTER-BY-LETTER BREAKDOWN                    │");
        Debug.Log("└──────────────────────────────────────────────────────────────────┘");
        
        int totalLetters = 0;
        int totalScore = 0;
        
        foreach (var word in words)
        {
            Debug.Log($"📖 Word: '{word.Word}' ({word.Coordinates.Count} letters)");
            
            int wordScore = 0;
            for (int i = 0; i < word.Coordinates.Count; i++)
            {
                var coord = word.Coordinates[i];
                char letter = gameManager.GetLetterAtPosition(coord);
                int letterScore = gameManager.GetPointsForActualScoring(letter);
                
                wordScore += letterScore;
                totalScore += letterScore;
                totalLetters++;
                
                Debug.Log($"   {(i + 1).ToString().PadLeft(2)}. '{letter}' at ({coord.x},{coord.y}) = {letterScore.ToString().PadLeft(2)} points");
            }
            
            Debug.Log($"   ✅ Word Total: {wordScore} points");
            Debug.Log("");
        }
        
        Debug.Log($"📊 Letter Summary: {totalLetters} letters, {totalScore} total points, {(totalLetters > 0 ? (float)totalScore / totalLetters : 0):F1} avg per letter");
        Debug.Log("");
    }
    
    /// <summary>
    /// Log comprehensive scoring summary
    /// </summary>
    private static void LogScoringSummary(NumericalScoringData scoringData)
    {
        Debug.Log("┌──────────────────────────────────────────────────────────────────┐");
        Debug.Log("│                      SCORING STEP SUMMARY                       │");
        Debug.Log("└──────────────────────────────────────────────────────────────────┘");
        
        int stepNumber = 1;
        int runningTotal = 0;
        
        foreach (var step in scoringData.steps)
        {
            string stepType = step.stepType switch
            {
                ScoreStep.StepType.IntersectingLetters => "🔗 Intersecting Letters",
                ScoreStep.StepType.WordBase => "📝 Base Word Score",
                ScoreStep.StepType.Multiplier => "✖️  Multiplier Applied",
                ScoreStep.StepType.AdditiveBonus => "➕ Additive Bonus",
                ScoreStep.StepType.Final => "🏁 Final Score",
                _ => "❓ Unknown Step"
            };
            
            if (step.stepType != ScoreStep.StepType.Final)
            {
                runningTotal += step.scoreValue;
                Debug.Log($"Step {stepNumber}: {stepType}");
                Debug.Log($"   Value: {step.displayText}");
                Debug.Log($"   Points: {step.scoreValue}");
                Debug.Log($"   Running Total: {runningTotal}");
                
                if (step.gridPositions.Count > 0)
                {
                    Debug.Log($"   Grid Positions: {string.Join(", ", step.gridPositions.Select(p => $"({p.x},{p.y})"))}");
                }
                
                Debug.Log($"   Animation Delay: {step.animationDelay}s");
            }
            else
            {
                Debug.Log($"Step {stepNumber}: {stepType}");
                Debug.Log($"   Final Score: {step.scoreValue}");
            }
            
            Debug.Log("");
            stepNumber++;
        }
        
        // Log active modifiers if any
        LogActiveModifiers();
        
        Debug.Log($"📈 Scoring Breakdown:");
        Debug.Log($"   • Intersection Score: {scoringData.intersectionScore}");
        Debug.Log($"   • Base Word Score: {scoringData.baseWordScore}");
        Debug.Log($"   • Total Animation Steps: {scoringData.steps.Count}");
        Debug.Log($"   • Total Animation Duration: {(scoringData.steps.LastOrDefault()?.animationDelay ?? 0) + 1.5f:F1}s");
        Debug.Log("");
    }
    
    /// <summary>
    /// Log information about active modifiers
    /// </summary>
    private static void LogActiveModifiers()
    {
        var modifierManager = WordScroll.Modifiers.ModifierManager.Instance;
        if (modifierManager == null) return;
        
        var activeModifiers = modifierManager.GetAllActiveModifiers();
        
        if (activeModifiers.Count > 0)
        {
            Debug.Log("🎛️  Active Modifiers:");
            foreach (var modifier in activeModifiers)
            {
                Debug.Log($"   • {modifier.cardName} ({modifier.effectType})");
                
                switch (modifier.effectType)
                {
                    case WordScroll.Modifiers.ModifierEffectType.GeneralScoreBonusAndMoveReduction:
                        if (modifier.generalScoreMultiplier > 1f)
                            Debug.Log($"     - Score Multiplier: ×{modifier.generalScoreMultiplier:F1}");
                        if (modifier.moveReductionPercentage > 0)
                            Debug.Log($"     - Move Reduction: {modifier.moveReductionPercentage:F1}%");
                        break;
                        
                    case WordScroll.Modifiers.ModifierEffectType.SpecificWordLengthScoreBonus:
                        Debug.Log($"     - Target Word Length: {modifier.targetWordLength}");
                        if (modifier.wordLengthScoreMultiplier > 1f)
                            Debug.Log($"     - Score Multiplier: ×{modifier.wordLengthScoreMultiplier:F1}");
                        break;
                        
                    case WordScroll.Modifiers.ModifierEffectType.VowelCountBonus:
                        Debug.Log($"     - Min Vowel Count: {modifier.minVowelCount}");
                        Debug.Log($"     - Bonus Points: +{modifier.vowelBonusPoints}");
                        break;
                }
            }
            Debug.Log("");
        }
    }
}