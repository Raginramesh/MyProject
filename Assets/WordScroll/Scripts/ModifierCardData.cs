using UnityEngine;

namespace WordScroll.Modifiers
{
    public enum CardTypeTag
    {
        Modifier,
        Gift,
        Upgrade
    }

    public enum RarityTag
    {
        Common,
        Uncommon,
        Rare
        // Add more rarities here later if needed, e.g., Epic, Legendary
    }

    // New enum to define the primary mechanical effect of the modifier
    public enum ModifierEffectType
    {
        None, // Default or for cards with no direct gameplay mechanic (e.g., purely cosmetic if you had such)
        SpecificWordLengthScoreBonus, // "5-letter words earn double points"
        GeneralScoreBonusAndMoveReduction, // "Double points for all words, but moves reduced by 20%"
        VowelCountBonus // "+5 points if the words has 2 vowels"
        // Add more effect types here as you design them
    }

    [CreateAssetMenu(fileName = "NewModifierCard", menuName = "Word Scroll/Modifier Card Data")]
    public class ModifierCardData : ScriptableObject
    {
        [Header("Card Identity")]
        public string cardName = "New Modifier";
        public CardTypeTag cardType = CardTypeTag.Modifier;
        public RarityTag rarity = RarityTag.Common;
        public Sprite icon;

        [Header("Card Details")]
        [TextArea(3, 5)]
        public string effectDescription = "Describes the temporary effect.";
        [TextArea(2, 4)]
        public string strategicUseCase = "Describes what kind of level or playstyle it supports.";
        [TextArea(2, 4)]
        public string synergyNotes = "Optional tips on combinations.";

        [Header("Core Effect Definition")]
        public ModifierEffectType effectType = ModifierEffectType.None;

        // --- Parameters for SpecificWordLengthScoreBonus ---
        [Header("Specific Word Length Score Bonus Params")]
        [Tooltip("The length of the word that triggers this bonus (e.g., 5 for 5-letter words). Only active if effectType is SpecificWordLengthScoreBonus.")]
        public int targetWordLength = 5;
        [Tooltip("The multiplier for scores of words matching targetWordLength (e.g., 2.0 for double points). Only active if effectType is SpecificWordLengthScoreBonus.")]
        public float wordLengthScoreMultiplier = 2.0f;

        // --- Parameters for GeneralScoreBonusAndMoveReduction ---
        [Header("General Score Bonus & Move Reduction Params")]
        [Tooltip("The multiplier for all words (e.g., 2.0 for double points). Only active if effectType is GeneralScoreBonusAndMoveReduction.")]
        public float generalScoreMultiplier = 2.0f;
        [Tooltip("Percentage by which moves are reduced (e.g., 0.20 for 20% reduction). Only active if effectType is GeneralScoreBonusAndMoveReduction.")]
        public float moveReductionPercentage = 0.20f;

        // --- Parameters for VowelCountBonus ---
        [Header("Vowel Count Bonus Params")]
        [Tooltip("Minimum number of vowels a word must have to get this bonus (e.g., 2). Only active if effectType is VowelCountBonus.")]
        public int minVowelCount = 2;
        [Tooltip("Flat points added if vowel condition is met (e.g., 5). Only active if effectType is VowelCountBonus.")]
        public int vowelBonusPoints = 5;

        // Note: When adding new ModifierEffectType, remember to add corresponding parameter sections here.
        // For more complex effects, you might consider dedicated classes/structs for parameters.
    }
}
