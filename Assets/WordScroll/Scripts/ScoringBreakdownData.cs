using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using WordScroll.Modifiers;
    /// <summary>
    /// Represents a single step in the scoring breakdown animation
    /// </summary>
    [System.Serializable]
    public class ScoringStep
    {
        public enum StepType
        {
            SharedLetter,
            WordBase,
            ModifierApplication,
            Total
        }

        public StepType stepType;
        public string displayText;
        public int scoreValue;
        public Color highlightColor = Color.white;
        public List<Vector2Int> gridPositions = new List<Vector2Int>(); // For highlighting grid cells
        public float animationDelay = 0f;
        public string modifierName = ""; // For modifier steps
        public string wordText = ""; // For word steps
        
        public ScoringStep(StepType type, string text, int score)
        {
            stepType = type;
            displayText = text;
            scoreValue = score;
        }
    }

    /// <summary>
    /// Contains all data needed for a complete scoring breakdown animation
    /// </summary>
    [System.Serializable]
    public class ScoringBreakdownData
    {
        public List<FoundWordData> words = new List<FoundWordData>();
        public List<ScoringStep> steps = new List<ScoringStep>();
        public Dictionary<Vector2Int, int> sharedLetterPositions = new Dictionary<Vector2Int, int>();
        public int totalBaseScore = 0;
        public int finalScore = 0;
        public float totalAnimationDuration = 0f;
        
        /// <summary>
        /// Generates scoring breakdown data from a list of words formed in a single move
        /// </summary>
        public static ScoringBreakdownData GenerateFromWords(List<FoundWordData> foundWords, GameManager gameManager)
        {
            var breakdownData = new ScoringBreakdownData();
            breakdownData.words = foundWords;
            
            // Find shared letters
            breakdownData.AnalyzeSharedLetters(foundWords);
            
            // Generate steps
            breakdownData.GenerateScoringSteps(gameManager);
            
            return breakdownData;
        }
        
        private void AnalyzeSharedLetters(List<FoundWordData> foundWords)
        {
            Dictionary<Vector2Int, int> positionCount = new Dictionary<Vector2Int, int>();
            
            // Count how many words use each position
            foreach (var word in foundWords)
            {
                foreach (var coord in word.Coordinates)
                {
                    if (positionCount.ContainsKey(coord))
                        positionCount[coord]++;
                    else
                        positionCount[coord] = 1;
                }
            }
            
            // Store shared positions (used by more than one word)
            sharedLetterPositions = positionCount.Where(kvp => kvp.Value > 1).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        
        private void GenerateScoringSteps(GameManager gameManager)
        {
            steps.Clear();
            float currentDelay = 0f;
            
            // Step 1: Show shared letters if any
            if (sharedLetterPositions.Count > 0)
            {
                foreach (var sharedPos in sharedLetterPositions)
                {
                    char letter = gameManager.GetLetterAtPosition(sharedPos.Key);
                    int letterScore = gameManager.GetPointsForActualScoring(letter);
                    
                    var step = new ScoringStep(ScoringStep.StepType.SharedLetter, 
                        $"Shared '{letter}' = {letterScore}", letterScore)
                    {
                        gridPositions = new List<Vector2Int> { sharedPos.Key },
                        highlightColor = Color.cyan,
                        animationDelay = currentDelay
                    };
                    
                    steps.Add(step);
                    currentDelay += 0.5f;
                }
            }
            
            // Step 2: Show each word's base score
            foreach (var word in words)
            {
                int wordBaseScore = CalculateWordBaseScore(word, gameManager);
                var step = new ScoringStep(ScoringStep.StepType.WordBase, 
                    $"{word.Word} = {wordBaseScore}", wordBaseScore)
                {
                    gridPositions = new List<Vector2Int>(word.Coordinates),
                    highlightColor = GetWordColor(word),
                    animationDelay = currentDelay,
                    wordText = word.Word
                };
                
                steps.Add(step);
                totalBaseScore += wordBaseScore;
                currentDelay += 0.8f;
            }
            
            // Step 3: Show modifier applications
            var modifierSteps = GenerateModifierSteps(gameManager, currentDelay);
            steps.AddRange(modifierSteps);
            currentDelay += modifierSteps.Count * 0.6f;
            
            // Step 4: Show final total
            var totalStep = new ScoringStep(ScoringStep.StepType.Total, 
                $"TOTAL: {finalScore}", finalScore)
            {
                animationDelay = currentDelay,
                highlightColor = Color.gold
            };
            steps.Add(totalStep);
            
            totalAnimationDuration = currentDelay + 1.5f;
        }
        
        private List<ScoringStep> GenerateModifierSteps(GameManager gameManager, float startDelay)
        {
            var modifierSteps = new List<ScoringStep>();
            float currentDelay = startDelay;
            int runningTotal = totalBaseScore;
            
            // Get active modifiers and apply them
            var modifierManager = Object.FindFirstObjectByType<ModifierManager>();
            if (modifierManager != null)
            {
                var activeModifiers = modifierManager.GetAllActiveModifiers();
                
                foreach (var modifier in activeModifiers)
                {
                    int scoreChange = CalculateModifierEffect(modifier, words, runningTotal);
                    if (scoreChange != 0)
                    {
                        string operation = scoreChange > 0 ? "+" : "";
                        if (modifier.effectType == ModifierEffectType.GeneralScoreBonusAndMoveReduction)
                        {
                            // Multiplier
                            int newTotal = Mathf.RoundToInt(runningTotal * modifier.generalScoreMultiplier);
                            scoreChange = newTotal - runningTotal;
                            operation = $"×{modifier.generalScoreMultiplier}";
                        }
                        
                        var step = new ScoringStep(ScoringStep.StepType.ModifierApplication, 
                            $"{modifier.cardName}: {operation}{scoreChange}", scoreChange)
                        {
                            animationDelay = currentDelay,
                            modifierName = modifier.cardName,
                            highlightColor = GetModifierColor(modifier)
                        };
                        
                        modifierSteps.Add(step);
                        runningTotal += scoreChange;
                        currentDelay += 0.6f;
                    }
                }
            }
            
            finalScore = runningTotal;
            return modifierSteps;
        }
        
        private int CalculateWordBaseScore(FoundWordData word, GameManager gameManager)
        {
            int score = 0;
            foreach (var coord in word.Coordinates)
            {
                char letter = gameManager.GetLetterAtPosition(coord);
                score += gameManager.GetPointsForActualScoring(letter);
            }
            return score;
        }
        
        private int CalculateModifierEffect(ModifierCardData modifier, List<FoundWordData> words, int baseScore)
        {
            switch (modifier.effectType)
            {
                case ModifierEffectType.SpecificWordLengthScoreBonus:
                    // Check if any word matches the target length
                    if (words.Any(w => w.Word.Length == modifier.targetWordLength))
                    {
                        return Mathf.RoundToInt(baseScore * (modifier.wordLengthScoreMultiplier - 1));
                    }
                    break;
                    
                case ModifierEffectType.VowelCountBonus:
                    int vowelBonus = 0;
                    foreach (var word in words)
                    {
                        int vowelCount = word.Word.Count(c => "AEIOUaeiou".Contains(c));
                        if (vowelCount >= modifier.minVowelCount)
                        {
                            vowelBonus += modifier.vowelBonusPoints;
                        }
                    }
                    return vowelBonus;
                    
                case ModifierEffectType.GeneralScoreBonusAndMoveReduction:
                    return Mathf.RoundToInt(baseScore * (modifier.generalScoreMultiplier - 1));
            }
            
            return 0;
        }
        
        // Cache for WordGridManager reference
        private static WordGridManager cachedGridManager;
        
        private Color GetWordColor(FoundWordData word)
        {
            // Use the new unified color system from WordGridManager
            if (cachedGridManager == null)
                cachedGridManager = Object.FindFirstObjectByType<WordGridManager>();
                
            if (cachedGridManager != null)
            {
                return cachedGridManager.GetValidWordColor();
            }
            
            // Fallback to yellow if WordGridManager not found
            return Color.yellow;
        }
        
        private Color GetModifierColor(ModifierCardData modifier)
        {
            return modifier.rarity switch
            {
                RarityTag.Common => Color.white,
                RarityTag.Uncommon => Color.blue,
                RarityTag.Rare => Color.magenta,
                _ => Color.white
            };
        }
    }
