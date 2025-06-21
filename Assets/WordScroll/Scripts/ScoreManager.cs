using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using WordScroll.Modifiers; // Required for ModifierManager and ModifierCardData

namespace WordScroll.Managers
{
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        [Header("Manager References")]
        private ModifierManager _modifierManager;

        [Header("Scoring State")]
        public int PlayerScore { get; private set; }

        // Optional: Event for UI updates
        public static event System.Action<int> OnScoreChanged;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // DontDestroyOnLoad(gameObject); // Optional: if ScoreManager needs to persist across scenes
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            PlayerScore = 0;
        }

        void Start()
        {
            // Use the ModifierManager singleton instance
            _modifierManager = ModifierManager.Instance;
            if (_modifierManager == null)
            {
                Debug.LogError("ScoreManager: ModifierManager instance not found! Modifier effects on score will not be applied.");
            }
        }

        /// <summary>
        /// Calculates the score for a given word, applying any active modifier effects.
        /// </summary>
        /// <param name="word">The word that was formed.</param>
        /// <param name="lettersUsed">The list of characters/tiles used to form the word (for potential individual letter scores).</param>
        /// <returns>The final score for the word.</returns>
        public int CalculateWordScore(string word, List<char> lettersUsed)
        {
            if (string.IsNullOrEmpty(word))
            {
                return 0;
            }

            // --- 1. Calculate Base Score ---
            // Basic scoring: 1 point per letter, for example.
            // You can expand this: e.g., rarer letters give more points.
            int baseScore = 0;
            foreach (char letter in lettersUsed) // Or word.Length if not using individual letter scores
            {
                baseScore += GetScoreForLetter(letter); // Example: 1 point per letter
            }
            
            int finalScore = baseScore;
            Debug.Log($"ScoreManager: Word '{word}', Base Score: {baseScore}");

            // --- 2. Apply Modifier Effects ---
            if (_modifierManager != null)
            {
                // Modifier: FiveLetterWordDoublePoints
                ModifierCardData fiveLetterBonusMod = _modifierManager.GetActiveModifierByType(ModifierEffectType.SpecificWordLengthScoreBonus);
                if (fiveLetterBonusMod != null && word.Length == 5) // Assuming targetWordLength is 5 for this effect as per its name
                {
                    // Use fiveLetterBonusMod.wordLengthScoreMultiplier instead of hardcoding 2
                    finalScore = Mathf.RoundToInt(finalScore * fiveLetterBonusMod.wordLengthScoreMultiplier);
                    Debug.Log($"ScoreManager: Applied '{fiveLetterBonusMod.cardName}'. Score is now {finalScore}");
                }

                // Modifier: GeneralScoreBonusAndMoveReduction (Score part)
                ModifierCardData doublePointsMod = _modifierManager.GetActiveModifierByType(ModifierEffectType.GeneralScoreBonusAndMoveReduction);
                if (doublePointsMod != null)
                {
                    // Use doublePointsMod.generalScoreMultiplier instead of hardcoding 2
                    finalScore = Mathf.RoundToInt(finalScore * doublePointsMod.generalScoreMultiplier);
                    Debug.Log($"ScoreManager: Applied '{doublePointsMod.cardName}' (score part). Score is now {finalScore}");
                }

                // Modifier: VowelBonus
                ModifierCardData vowelBonusMod = _modifierManager.GetActiveModifierByType(ModifierEffectType.VowelCountBonus);
                if (vowelBonusMod != null)
                {
                    int vowelCount = word.Count(c => "AEIOUaeiou".Contains(c));
                    // Check against minVowelCount from ModifierCardData
                    if (vowelCount >= vowelBonusMod.minVowelCount) 
                    {
                        // Use vowelBonusMod.vowelBonusPointsPerVowel (assuming this name, adjust if different in ModifierCardData)
                        // It was vowelBonusPoints in ModifierCardData, let's assume it was meant to be points per vowel or a flat bonus.
                        // For now, using vowelBonusPoints as a flat bonus if minVowelCount is met.
                        // If it's per vowel, the logic should be: int bonusFromVowels = vowelCount * vowelBonusMod.vowelBonusPointsPerVowel;
                        int bonusFromVowels = vowelBonusMod.vowelBonusPoints; // Using the flat bonus as defined
                        finalScore += bonusFromVowels;
                        Debug.Log($"ScoreManager: Applied '{vowelBonusMod.cardName}'. Added {bonusFromVowels} ({vowelCount} vowels). Score is now {finalScore}");
                    }
                }
            }
            else
            {
                Debug.LogWarning("ScoreManager: ModifierManager reference is null. Cannot apply score modifiers.");
            }
            
            AddScore(finalScore); // Add the calculated word score to the player's total score
            return finalScore;
        }

        /// <summary>
        /// Gets the base score for an individual letter.
        /// Placeholder - customize this based on your game's scoring rules.
        /// </summary>
        private int GetScoreForLetter(char letter)
        {
            // Use GameManager's scoring system to respect Scrabble values and scoring mode
            if (GameManager.instance != null)
            {
                return GameManager.instance.GetPointsForActualScoring(letter);
            }
            
            // Fallback if GameManager is not available
            return 1; 
        }

        /// <summary>
        /// Adds the given amount to the player's total score and invokes the OnScoreChanged event.
        /// </summary>
        /// <param name="amount">The amount to add to the score.</param>
        public void AddScore(int amount)
        {
            if (amount <= 0 && !(amount < 0)) // only add positive score, unless it's a penalty
            {
                // if amount is 0, do nothing, unless it's a penalty that reduces score to 0
                // this check is a bit convoluted, if you want to allow 0 score to be added, remove this
            }
            PlayerScore += amount;
            Debug.Log($"ScoreManager: Score updated. New Total Score: {PlayerScore}");
            OnScoreChanged?.Invoke(PlayerScore);
        }

        /// <summary>
        /// Resets the player's score to zero.
        /// </summary>
        public void ResetScore()
        {
            PlayerScore = 0;
            Debug.Log("ScoreManager: Player score reset.");
            OnScoreChanged?.Invoke(PlayerScore);
        }
    }
}
