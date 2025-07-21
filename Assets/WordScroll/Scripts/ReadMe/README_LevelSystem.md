# Simplified Level-Based System

## Overview
A streamlined level progression system where players advance through levels linearly without the ability to go back to previous levels. The system is integrated with the existing game over screen for a unified UI experience.

## Key Features

### ⭐ Star Rating System (Percentage-Based)
- **1 Star**: 50% of target score (configurable)
- **2 Stars**: 75% of target score (configurable)
- **3 Stars**: 100% of target score (configurable)
- **Level Completion**: When moves are exhausted (target score is for stars only)
- **No Level Failure**: Players always complete levels when moves run out

### 🎯 Linear Progression
- Players must complete levels in order
- No going back to previous levels
- Auto-advance to next level on completion
- Integrated with existing game over screen

### 📊 Level Configuration
Each level can be configured with:
- Target score (for 3-star rating, not completion)
- Maximum moves allowed (level ends when exhausted)
- Grid size
- Score multipliers
- Special tile availability
- Custom letter sets
- Percentage-based star thresholds

## Core Components

### 1. LevelData.cs
ScriptableObject defining individual level parameters:
```csharp
- Level identity (number, name, description)
- Score thresholds (1/2/3 stars)
- Move limits
- Grid configuration
- Difficulty modifiers
```

### 2. LevelManager.cs  
Central management system:
```csharp
- Level progression tracking
- Move and score monitoring
- Star rating calculation
- Auto-unlock next level
- Save/load progress
```

### 3. GameOverUIController.cs (Integrated Level Complete UI)
**UPDATED**: Now handles both traditional game over and move-based level completion:
```csharp
- Traditional game over for non-level play
- Level completion when moves are exhausted
- Star rating display (0-3 stars based on score percentage)
- Score percentage display relative to target
- Move count display (X/Y moves used)
- Auto-advance to next level (3 second delay)
- Manual navigation buttons (Next Level, Retry, Home)
- Unified UI experience
- No "level failed" state - always completion
```

### 4. LevelConfiguration.cs
Tool for generating sample levels:
```csharp
- Creates progressive difficulty
- Configurable scaling factors
- Automatic level descriptions
```

## Integration with GameManager

The existing GameManager seamlessly integrates:
- Move counting automatically notifies LevelManager
- Score increases update level progress in real-time
- Level completion triggers when moves are exhausted
- GameOverUIController detects level system and shows appropriate UI
- Backward compatible with original gameplay
- No level failure state - always progression

## Simplified User Flow

1. **Level Start**: Game automatically loads next level
2. **Gameplay**: Standard word puzzle mechanics
3. **Progress**: Real-time tracking of score vs target
4. **Completion**: 
   - Level completes when all moves are used
   - Game over panel shows final score, percentage, and stars earned
   - Auto-advance to next level (always unlocked)
5. **Progression**: Linear advancement through all levels (no failure state)

## Benefits

✅ **Player-Friendly**: No frustrating failure states  
✅ **Move-Based**: Clear completion condition (moves exhausted)  
✅ **Simple**: No complex UI or navigation  
✅ **Unified**: Single game over screen handles all scenarios  
✅ **Linear**: Clear progression path  
✅ **Motivating**: Star ratings provide replay value goals  
✅ **Configurable**: Easy to adjust difficulty per level  
✅ **Integrated**: Works with existing game systems  
✅ **Scalable**: Easy to add more levels  
✅ **Percentage-Based**: Flexible star rating system  

## Setup Instructions

1. Create LevelData assets for each level using the menu: `Create > Word Scroll > Level Data`
2. Add LevelManager to scene and assign level references
3. Ensure GameOverUIController is on the game over panel
4. Set GameManager.useLevelSystem = true
5. Configure the GameOverUIController with level system UI elements:
   - levelSystemPanel (panel shown for level system)
   - starIcons (array of 3 star GameObjects)
   - nextLevelButton, retryButton, homeButton
6. Players automatically progress through levels!

## UI Configuration

The GameOverUIController now supports two modes:

### Traditional Mode (useLevelSystem = false)
- Shows winLossMessageText ("You Win!" / "You Lose!")
- Shows finalScoreText
- Uses existing UI elements

### Level System Mode (useLevelSystem = true)  
- Shows levelSystemPanel
- Displays level completion title (always "Complete!")
- Shows star rating (0-3 stars based on score percentage)
- Shows final score as percentage of target
- Shows moves used (X/Y format)
- Shows target score with "(for 3⭐)" clarification
- Auto-advances to next level (always unlocked)
- No failure state - levels always complete when moves exhausted
