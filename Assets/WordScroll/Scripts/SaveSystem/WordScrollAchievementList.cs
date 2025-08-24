using UnityEngine;
using MoreMountains.Tools;

namespace WordScroll.SaveSystem
{
    /// <summary>
    /// Achievement definitions for WordScroll game
    /// Integrates with Feel framework's achievement system
    /// </summary>
    [CreateAssetMenu(fileName = "WordScrollAchievements", menuName = "Word Scroll/Achievement List")]
    public class WordScrollAchievementList : MMAchievementList
    {
        [Header("WordScroll Achievements")]
        [SerializeField] private MMAchievement[] wordScrollAchievements = new MMAchievement[]
        {
            // Basic Progress Achievements
            new MMAchievement
            {
                AchievementID = "first_steps",
                AchievementType = AchievementTypes.Simple,
                Title = "First Steps",
                Description = "Complete your first level",
                Points = 10,
                HiddenAchievement = false
            },
            
            new MMAchievement
            {
                AchievementID = "getting_started",
                AchievementType = AchievementTypes.Progress,
                Title = "Getting Started",
                Description = "Complete 5 levels",
                Points = 25,
                ProgressTarget = 5,
                HiddenAchievement = false
            },
            
            new MMAchievement
            {
                AchievementID = "word_explorer",
                AchievementType = AchievementTypes.Progress,
                Title = "Word Explorer",
                Description = "Complete 25 levels",
                Points = 100,
                ProgressTarget = 25,
                HiddenAchievement = false
            },
            
            new MMAchievement
            {
                AchievementID = "completionist",
                AchievementType = AchievementTypes.Progress,
                Title = "Completionist",
                Description = "Complete all levels",
                Points = 500,
                ProgressTarget = 100, // Adjust based on your total level count
                HiddenAchievement = false
            },
            
            // Performance Achievements
            new MMAchievement
            {
                AchievementID = "perfect_score",
                AchievementType = AchievementTypes.Simple,
                Title = "Perfect Score",
                Description = "Get 3 stars on a level",
                Points = 50,
                HiddenAchievement = false
            },
            
            new MMAchievement
            {
                AchievementID = "perfect_levels",
                AchievementType = AchievementTypes.Progress,
                Title = "Perfectionist",
                Description = "Get 3 stars on 10 levels",
                Points = 200,
                ProgressTarget = 10,
                HiddenAchievement = false
            },
            
            new MMAchievement
            {
                AchievementID = "speed_demon",
                AchievementType = AchievementTypes.Simple,
                Title = "Speed Demon",
                Description = "Complete a timed level in under 2 minutes",
                Points = 75,
                HiddenAchievement = false
            },
            
            new MMAchievement
            {
                AchievementID = "efficiency_expert",
                AchievementType = AchievementTypes.Simple,
                Title = "Efficiency Expert",
                Description = "Complete a level using less than 50% of available moves",
                Points = 100,
                HiddenAchievement = false
            },
            
            // Word Discovery Achievements
            new MMAchievement
            {
                AchievementID = "word_finder",
                AchievementType = AchievementTypes.Progress,
                Title = "Word Finder",
                Description = "Find 50 words total",
                Points = 50,
                ProgressTarget = 50,
                HiddenAchievement = false
            },
            
            new MMAchievement
            {
                AchievementID = "word_master",
                AchievementType = AchievementTypes.Progress,
                Title = "Word Master",
                Description = "Find 500 words total",
                Points = 250,
                ProgressTarget = 500,
                HiddenAchievement = false
            },
            
            new MMAchievement
            {
                AchievementID = "vocabulary_expert",
                AchievementType = AchievementTypes.Progress,
                Title = "Vocabulary Expert",
                Description = "Find 1000 words total",
                Points = 500,
                ProgressTarget = 1000,
                HiddenAchievement = false
            },
            
            new MMAchievement
            {
                AchievementID = "long_word_specialist",
                AchievementType = AchievementTypes.Simple,
                Title = "Long Word Specialist",
                Description = "Find a word with 8 or more letters",
                Points = 100,
                HiddenAchievement = false
            },
            
            // Game Mode Specific Achievements
            new MMAchievement
            {
                AchievementID = "scrabble_master",
                AchievementType = AchievementTypes.Progress,
                Title = "Scrabble Master",
                Description = "Complete 20 Scrabble-style levels",
                Points = 150,
                ProgressTarget = 20,
                HiddenAchievement = false
            },
            
            new MMAchievement
            {
                AchievementID = "wordle_wizard",
                AchievementType = AchievementTypes.Progress,
                Title = "Wordle Wizard",
                Description = "Complete 20 Wordle-style levels",
                Points = 150,
                ProgressTarget = 20,
                HiddenAchievement = false
            },
            
            // Special/Hidden Achievements
            new MMAchievement
            {
                AchievementID = "persistent_player",
                AchievementType = AchievementTypes.Progress,
                Title = "Persistent Player",
                Description = "Play the game for 5 different days",
                Points = 100,
                ProgressTarget = 5,
                HiddenAchievement = true
            },
            
            new MMAchievement
            {
                AchievementID = "marathon_session",
                AchievementType = AchievementTypes.Simple,
                Title = "Marathon Session",
                Description = "Play for more than 1 hour in a single session",
                Points = 200,
                HiddenAchievement = true
            },
            
            new MMAchievement
            {
                AchievementID = "comeback_kid",
                AchievementType = AchievementTypes.Simple,
                Title = "Comeback Kid",
                Description = "Complete a level after failing it 3 times",
                Points = 75,
                HiddenAchievement = true
            }
        };
        
        private void OnValidate()
        {
            // Ensure the base class has our achievements
            if (Achievements == null || Achievements.Count != wordScrollAchievements.Length)
            {
                Achievements = new System.Collections.Generic.List<MMAchievement>(wordScrollAchievements);
            }
        }
    }
}
