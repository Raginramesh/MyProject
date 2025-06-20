using UnityEngine;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using WordScroll.Modifiers;

/// <summary>
/// Enhanced debug logging system for scoring with formatted console output
/// </summary>
public static class ScoringDebugLogger
{
    private static bool verboseLogging = true;
    private static bool useRichText = true;
    
    public static bool VerboseLogging 
    { 
        get => verboseLogging; 
        set => verboseLogging = value; 
    }
    
    public static bool UseRichText 
    { 
        get => useRichText; 
        set => useRichText = value; 
    }
    
    /// <summary>
    /// Log comprehensive scoring breakdown with enhanced formatting
    /// </summary>
    public static void LogScoringBreakdown(NumericalScoringData scoringData)
    {
        var log = new StringBuilder();
        
        // Header
        log.AppendLine(FormatHeader("SCORING BREAKDOWN ANALYSIS"));
        log.AppendLine();
        
        // Words summary
        log.AppendLine(FormatSection("WORDS FORMED"));
        foreach (var word in scoringData.words)
        {
            log.AppendLine($"  • {FormatWord(word.Word)} ({word.Coordinates.Count} letters)");
            if (verboseLogging)
            {
                log.AppendLine($"    Positions: {string.Join(", ", word.Coordinates.Select(c => $"({c.x},{c.y})"))}");
            }
        }
        log.AppendLine();
        
        // Scoring steps
        log.AppendLine(FormatSection("SCORING STEPS"));
        int runningTotal = 0;
        int stepNumber = 1;
        
        foreach (var step in scoringData.steps)
        {
            string stepDescription = GetDetailedStepDescription(step);
            runningTotal += (step.stepType != ScoreStep.StepType.Final) ? step.scoreValue : 0;
            
            log.AppendLine($"  {FormatStepNumber(stepNumber)}: {stepDescription}");
            
            if (step.stepType != ScoreStep.StepType.Final)
            {
                log.AppendLine($"    {FormatRunningTotal(runningTotal)}");
            }
            
            if (verboseLogging && step.gridPositions.Count > 0)
            {
                log.AppendLine($"    Grid positions: {string.Join(", ", step.gridPositions.Select(p => $"({p.x},{p.y})"))}");
                log.AppendLine($"    Animation delay: {step.animationDelay}s");
            }
            
            log.AppendLine();
            stepNumber++;
        }
        
        // Summary
        log.AppendLine(FormatSection("SUMMARY"));
        log.AppendLine($"  Intersection Score: {FormatScore(scoringData.intersectionScore)}");
        log.AppendLine($"  Base Word Score: {FormatScore(scoringData.baseWordScore)}");
        log.AppendLine($"  Final Score: {FormatFinalScore(scoringData.finalScore)}");
        log.AppendLine($"  Total Steps: {scoringData.steps.Count}");
        log.AppendLine($"  Animation Duration: {GetTotalAnimationDuration(scoringData.steps):F1}s");
        
        log.AppendLine();
        log.AppendLine(FormatFooter());
        
        Debug.Log(log.ToString());
    }
    
    /// <summary>
    /// Log letter-by-letter scoring breakdown
    /// </summary>
    public static void LogLetterBreakdown(List<FoundWordData> words, GameManager gameManager)
    {
        if (!verboseLogging) return;
        
        var log = new StringBuilder();
        
        log.AppendLine(FormatHeader("LETTER-BY-LETTER SCORING"));
        log.AppendLine();
        
        int totalLetters = 0;
        int totalScore = 0;
        
        foreach (var word in words)
        {
            log.AppendLine(FormatSubsection($"Word: {FormatWord(word.Word)}"));
            
            int wordScore = 0;
            for (int i = 0; i < word.Coordinates.Count; i++)
            {
                var coord = word.Coordinates[i];
                char letter = gameManager.GetLetterAtPosition(coord);
                int letterScore = gameManager.GetPointsForActualScoring(letter);
                
                wordScore += letterScore;
                totalScore += letterScore;
                totalLetters++;
                
                log.AppendLine($"  {i + 1:D2}. '{FormatLetter(letter)}' at ({coord.x},{coord.y}) = {FormatScore(letterScore)}");
            }
            
            log.AppendLine($"      Word Total: {FormatScore(wordScore)}");
            log.AppendLine();
        }
        
        log.AppendLine(FormatSection("LETTER SUMMARY"));
        log.AppendLine($"  Total Letters: {totalLetters}");
        log.AppendLine($"  Total Score: {FormatScore(totalScore)}");
        log.AppendLine($"  Average per Letter: {FormatScore(totalLetters > 0 ? totalScore / totalLetters : 0)}");
        
        log.AppendLine();
        log.AppendLine(FormatFooter());
        
        Debug.Log(log.ToString());
    }
    
    /// <summary>
    /// Log modifier information with detailed effects
    /// </summary>
    public static void LogModifierInfo(List<ModifierCardData> modifiers)
    {
        var log = new StringBuilder();
        
        log.AppendLine(FormatHeader($"ACTIVE MODIFIERS ({modifiers.Count})"));
        log.AppendLine();
        
        if (modifiers.Count == 0)
        {
            log.AppendLine("  No active modifiers");
        }
        else
        {
            int index = 1;
            foreach (var modifier in modifiers)
            {
                log.AppendLine(FormatSubsection($"{index}. {FormatModifierName(modifier.cardName)}"));
                log.AppendLine($"     Type: {modifier.effectType}");
                log.AppendLine($"     Card Type: {modifier.cardType}");
                
                // Detailed effect breakdown
                LogModifierEffects(log, modifier);
                
                log.AppendLine();
                index++;
            }
        }
        
        log.AppendLine();
        log.AppendLine(FormatFooter());
        
        Debug.Log(log.ToString());
    }
    
    private static void LogModifierEffects(StringBuilder log, ModifierCardData modifier)
    {
        switch (modifier.effectType)
        {
            case ModifierEffectType.GeneralScoreBonusAndMoveReduction:
                if (modifier.generalScoreMultiplier > 1f)
                    log.AppendLine($"     • Score Multiplier: ×{modifier.generalScoreMultiplier:F1}");
                if (modifier.moveReductionPercentage > 0)
                    log.AppendLine($"     • Move Reduction: {modifier.moveReductionPercentage:F1}%");
                break;
                
            case ModifierEffectType.SpecificWordLengthScoreBonus:
                log.AppendLine($"     • Target Word Length: {modifier.targetWordLength}");
                if (modifier.wordLengthScoreMultiplier > 1f)
                    log.AppendLine($"     • Score Multiplier: ×{modifier.wordLengthScoreMultiplier:F1}");
                break;
                
            case ModifierEffectType.VowelCountBonus:
                log.AppendLine($"     • Min Vowel Count: {modifier.minVowelCount}");
                log.AppendLine($"     • Bonus Points: +{modifier.vowelBonusPoints}");
                break;
        }
    }
    
    private static string GetDetailedStepDescription(ScoreStep step)
    {
        return step.stepType switch
        {
            ScoreStep.StepType.IntersectingLetters => 
                $"Intersecting Letters: {FormatScore(step.scoreValue)} points",
            ScoreStep.StepType.WordBase => 
                $"Base Word Score: {FormatScoreChange($"+{step.scoreValue}")}",
            ScoreStep.StepType.Multiplier => 
                $"Multiplier Applied: {FormatMultiplier(step.displayText)} ({FormatScoreChange($"+{step.scoreValue}")})",
            ScoreStep.StepType.AdditiveBonus => 
                $"Additive Bonus: {FormatBonus(step.displayText)}",
            ScoreStep.StepType.Final => 
                $"Final Score: {FormatFinalScore(step.scoreValue)}",
            _ => 
                $"Unknown Step: {FormatScore(step.scoreValue)}"
        };
    }
    
    private static float GetTotalAnimationDuration(List<ScoreStep> steps)
    {
        if (steps.Count == 0) return 0f;
        return steps.Max(s => s.animationDelay) + 1.5f; // Base animation time
    }
    
    // Formatting methods
    private static string FormatHeader(string text)
    {
        if (!useRichText) return $"=== {text} ===";
        return $"<color=yellow><b>=== {text} ===</b></color>";
    }
    
    private static string FormatSection(string text)
    {
        if (!useRichText) return $"--- {text} ---";
        return $"<color=cyan><b>--- {text} ---</b></color>";
    }
    
    private static string FormatSubsection(string text)
    {
        if (!useRichText) return text;
        return $"<color=white><b>{text}</b></color>";
    }
    
    private static string FormatWord(string word)
    {
        if (!useRichText) return $"'{word}'";
        return $"<color=yellow><b>'{word}'</b></color>";
    }
    
    private static string FormatLetter(char letter)
    {
        if (!useRichText) return letter.ToString();
        return $"<color=orange><b>{letter}</b></color>";
    }
    
    private static string FormatScore(int score)
    {
        if (!useRichText) return score.ToString();
        return $"<color=green><b>{score}</b></color>";
    }
    
    private static string FormatScoreChange(string change)
    {
        if (!useRichText) return change;
        return $"<color=lightblue><b>{change}</b></color>";
    }
    
    private static string FormatFinalScore(int score)
    {
        if (!useRichText) return score.ToString();
        return $"<color=gold><size=14><b>{score}</b></size></color>";
    }
    
    private static string FormatMultiplier(string multiplier)
    {
        if (!useRichText) return multiplier;
        return $"<color=magenta><b>{multiplier}</b></color>";
    }
    
    private static string FormatBonus(string bonus)
    {
        if (!useRichText) return bonus;
        return $"<color=green><b>{bonus}</b></color>";
    }
    
    private static string FormatModifierName(string name)
    {
        if (!useRichText) return name;
        return $"<color=purple><b>{name}</b></color>";
    }
    
    private static string FormatStepNumber(int number)
    {
        if (!useRichText) return $"Step {number}";
        return $"<color=white><b>Step {number}</b></color>";
    }
    
    private static string FormatRunningTotal(int total)
    {
        if (!useRichText) return $"Running Total: {total}";
        return $"<color=yellow>Running Total: <b>{total}</b></color>";
    }
    
    private static string FormatFooter()
    {
        if (!useRichText) return "================================";
        return "<color=gray>================================</color>";
    }
    
    /// <summary>
    /// Log an error with enhanced formatting
    /// </summary>
    public static void LogError(string title, string details)
    {
        var log = new StringBuilder();
        
        log.AppendLine(FormatHeader($"ERROR: {title}"));
        log.AppendLine();
        log.AppendLine(details);
        log.AppendLine();
        log.AppendLine(FormatFooter());
        
        Debug.LogError(log.ToString());
    }
}
