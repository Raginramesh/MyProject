# Level Completion Mechanics

## Overview
The Word Scroll game uses a **move-based level completion system** where levels always complete when moves are exhausted, regardless of score achieved. The target score is used only for star rating calculation, not for level completion/failure determination.

## Key Concepts

### Level Completion Logic
- **Level ends when**: All moves are used up (or unlimited moves mode)
- **Level does NOT end when**: Target score is reached
- **No level failure**: Players cannot "fail" a level - they always complete it when moves run out

### Star Rating System
- **Target Score Purpose**: Benchmark for 3-star rating only
- **Star Calculation**: Based on percentage of target score achieved
  - 1 Star: 50% of target score (configurable)
  - 2 Stars: 75% of target score (configurable)  
  - 3 Stars: 100% of target score (configurable)
- **Star Thresholds**: Set as percentages in LevelData ScriptableObject

## Implementation Details

### LevelData.cs
```csharp
// Level completion based on moves
public bool IsLevelCompletedByMoves(int currentMoves)
{
    if (unlimitedMoves) return false;
    return currentMoves >= maxMoves;
}

// Star rating based on score percentage
public int GetStarRating(int achievedScore)
{
    if (achievedScore >= ThreeStarScore) return 3;
    if (achievedScore >= TwoStarScore) return 2;
    if (achievedScore >= OneStarScore) return 1;
    return 0;
}
```

### LevelManager.cs
```csharp
// Check completion when moves are used
public void AddMove()
{
    currentMoves++;
    if (!currentLevel.HasMovesRemaining(currentMoves))
    {
        CheckLevelCompletion(); // Always completes level
    }
}
```

### GameOverUIController.cs
```csharp
// Show completion with score percentage
levelCompleteTitle.text = $"{currentLevel.LevelName} Complete! ({scorePercentage:F1}%)";
targetScoreText.text = $"Target: {currentLevel.TargetScore:N0} (for 3⭐)";
```

## UI Display Guidelines

### Game Over Screen
- **Title**: Always shows "Level Complete!" (no failure state)
- **Score Display**: Shows achieved score and percentage of target
- **Target Display**: Shows target score with "(for 3⭐)" clarification
- **Moves Display**: Shows "X/Y moves used"
- **Stars**: Visual display of earned stars (0-3)

### In-Game UI
- **Moves Counter**: Shows remaining moves, counts down to 0
- **Score Display**: Shows current score, updates in real-time
- **Progress**: Optional progress bar showing score percentage

## Player Experience

### Gameplay Flow
1. Player starts level with X moves and 0 score
2. Player makes moves, score accumulates
3. When moves reach maximum, level automatically ends
4. Player sees results: final score, percentage, stars earned
5. Player proceeds to next level (always unlocked)

### Strategic Implications
- **Move Efficiency**: Players must balance word length vs. move count
- **Score Optimization**: Higher scores earn more stars, but moves are limited
- **No Pressure**: Players cannot "fail" - encourages experimentation
- **Progression**: Linear progression through levels based on completion, not performance

## Configuration

### Per-Level Settings (LevelData)
```csharp
[SerializeField] private int targetScore = 100;        // 3-star benchmark
[SerializeField] private int maxMoves = 10;            // Move limit
[SerializeField] private float oneStarPercentage = 50f;   // 1-star threshold
[SerializeField] private float twoStarPercentage = 75f;   // 2-star threshold  
[SerializeField] private float threeStarPercentage = 100f; // 3-star threshold
```

### Design Considerations
- **Target Score**: Should be achievable but challenging for 3 stars
- **Move Limit**: Balance between too easy (too many moves) and too hard (too few)
- **Star Percentages**: Allow for different difficulty curves per level

## Migration Notes

### From Score-Based Completion
If migrating from a system where reaching target score ended the level:

1. **Remove**: Score-based completion checks
2. **Update**: UI text to reflect move-based completion
3. **Clarify**: Target score is for stars only
4. **Test**: Ensure levels end only when moves are exhausted

### Legacy Method Cleanup
- `IsLevelCompleted(int score)` → Removed (confusing)
- `IsLevelCompletedByMoves(int moves)` → Primary completion check
- `OnLevelFailed` events → Can be removed (no failure state)

## Benefits

### Player-Friendly
- No frustrating "game over" scenarios
- Always feel progress and completion
- Encourages trying different strategies

### Design Flexibility
- Easy to balance difficulty through move counts
- Star system provides optional challenge
- Clear progression path for all players

### Technical Simplicity
- Single completion condition (moves)
- No complex success/failure logic
- Consistent player experience
