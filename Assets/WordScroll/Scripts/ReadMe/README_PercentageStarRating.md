# Percentage-Based Star Rating System

## Overview
The level system now uses **percentage-based star ratings** instead of fixed score thresholds. This makes the system much more flexible and scalable across levels with different target scores.

## How It Works

### Star Rating Calculation
Instead of fixed scores, stars are awarded based on **percentage of target score achieved**:

- **1 Star**: Default 50% of target score
- **2 Stars**: Default 75% of target score  
- **3 Stars**: Default 100% of target score

### Example
For a level with target score of 200:
- **1 Star**: 100 points (50% of 200)
- **2 Stars**: 150 points (75% of 200)
- **3 Stars**: 200 points (100% of 200)

For a level with target score of 1000:
- **1 Star**: 500 points (50% of 1000)
- **2 Stars**: 750 points (75% of 1000)
- **3 Stars**: 1000 points (100% of 1000)

## LevelData Configuration

### New Fields
```csharp
[Header("Star Rating Thresholds (Percentage of Target Score)")]
[Range(0f, 100f)]
private float oneStarPercentage = 50f;   // 50% of target for 1 star
[Range(0f, 100f)]
private float twoStarPercentage = 75f;   // 75% of target for 2 stars  
[Range(0f, 100f)]
private float threeStarPercentage = 100f; // 100% of target for 3 stars
```

### Auto-Calculated Properties
The actual score thresholds are calculated automatically:
```csharp
public int OneStarScore => Mathf.RoundToInt(targetScore * oneStarPercentage / 100f);
public int TwoStarScore => Mathf.RoundToInt(targetScore * twoStarPercentage / 100f);
public int ThreeStarScore => Mathf.RoundToInt(targetScore * threeStarPercentage / 100f);
```

## New Methods

### GetStarRating(int achievedScore)
- **Purpose**: Get star rating based on achieved score
- **Returns**: 0-3 stars based on percentage thresholds

### GetStarRatingByPercentage(float percentage)
- **Purpose**: Get star rating directly from percentage
- **Returns**: 0-3 stars based on percentage value

### GetScorePercentage(int achievedScore)
- **Purpose**: Calculate what percentage of target was achieved
- **Returns**: Percentage (0-100+)

### GetStarThresholdInfo()
- **Purpose**: Get debug info about star thresholds
- **Returns**: Formatted string with all threshold info

## UI Integration

### Game Over Screen
The game over screen now shows percentage information:
```
Level 1 Complete! (85.5%)
Score: 171 (85.5%)
Target: 200 (100%)
```

### Debug Logging
Enhanced debug output shows star threshold details:
```
📊 Score: 171 (85.5%) | Star Thresholds: 1⭐100 (50%), 2⭐150 (75%), 3⭐200 (100%) of 200
```

## Benefits

### 1. **Scalability**
- Easy to create levels with any target score
- Star difficulty remains consistent across levels
- No need to manually calculate star thresholds

### 2. **Flexibility**
- Can adjust star difficulty by changing percentages
- Different levels can have different star requirements
- Easy to balance difficulty progression

### 3. **Consistency**
- Star requirements scale proportionally with target score
- Players know what to expect across different levels
- Fair difficulty regardless of level score range

### 4. **Designer Friendly**
- Sliders in inspector make it easy to adjust
- Auto-validation prevents invalid configurations
- Real-time preview of calculated thresholds

## Editor Features

### Validation
- Automatically ensures percentages are in correct order
- Clamps values to 0-100% range
- Prevents invalid configurations

### OnValidate
The system automatically corrects invalid percentage configurations:
```csharp
// Ensures 1-star ≤ 2-star ≤ 3-star percentages
if (oneStarPercentage > twoStarPercentage)
    twoStarPercentage = oneStarPercentage;
```

## Backward Compatibility
- Existing levels will work with default percentages (50%, 75%, 100%)
- Old star threshold properties still available as calculated values
- No breaking changes to existing code

## Usage Examples

### Easy Level (Generous Stars)
```csharp
oneStarPercentage = 30f;    // 1 star at 30%
twoStarPercentage = 60f;    // 2 stars at 60%  
threeStarPercentage = 90f;  // 3 stars at 90%
```

### Hard Level (Strict Stars)
```csharp
oneStarPercentage = 70f;    // 1 star at 70%
twoStarPercentage = 85f;    // 2 stars at 85%
threeStarPercentage = 100f; // 3 stars at 100%
```

### Tutorial Level (Very Easy)
```csharp
oneStarPercentage = 10f;    // 1 star at 10%
twoStarPercentage = 25f;    // 2 stars at 25%
threeStarPercentage = 50f;  // 3 stars at 50%
```

This percentage-based system provides much more flexibility and consistency for level design while maintaining all existing functionality.
