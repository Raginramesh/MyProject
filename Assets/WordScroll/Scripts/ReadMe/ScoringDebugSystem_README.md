# Scoring Debug System Documentation

## Overview

The Scoring Debug System provides comprehensive debugging capabilities for the Unity word puzzle game's scoring system. It includes enhanced console logging, an in-game debug UI, and detailed breakdown analysis of all scoring calculations.

## Components

### 1. ScoringDebugUI.cs
**Purpose**: Main debug UI panel that displays scoring breakdowns in-game
**Features**:
- Real-time scoring breakdown display
- Expandable/collapsible entries
- Debug log export functionality
- Toggle for verbose mode
- Current score tracking

**Setup**:
1. Add `ScoringDebugUI` component to a GameObject
2. Set the GameObject's tag to "DebugSystem"
3. Assign UI references in the inspector
4. The component will auto-setup as a singleton with DontDestroyOnLoad

### 2. ScoringDebugEntryUI.cs
**Purpose**: Individual entry components for the debug UI
**Features**:
- Expandable details view
- Color-coded entry types
- Animation support
- Icon display based on entry type

### 3. ScoringDebugLogger.cs
**Purpose**: Enhanced console logging with rich formatting
**Features**:
- Colorized console output (when supported)
- Detailed step-by-step breakdowns
- Letter-by-letter scoring analysis
- Modifier effect explanations
- Configurable verbosity levels

**Usage**:
```csharp
// Enable/disable features
ScoringDebugLogger.VerboseLogging = true;
ScoringDebugLogger.UseRichText = true;

// Log scoring breakdown
ScoringDebugLogger.LogScoringBreakdown(scoringData);
ScoringDebugLogger.LogLetterBreakdown(words, gameManager);
ScoringDebugLogger.LogModifierInfo(modifiers);
```

### 4. DebugToggleButton.cs
**Purpose**: Simple button component to show/hide debug UI
**Features**:
- Keyboard shortcut support (F1 by default)
- Automatic hiding in release builds
- Easy integration into any scene

### 5. ScoringDebugManager.cs
**Purpose**: Central manager for debug system configuration
**Features**:
- Debug system initialization
- Test controls for development
- Runtime configuration changes
- Game state logging

## Debug Output Examples

### Console Output
```
=== SCORING BREAKDOWN ANALYSIS ===

--- WORDS FORMED ---
  • 'HELLO' (5 letters)
    Positions: (0,0), (1,0), (2,0), (3,0), (4,0)
  • 'WORLD' (5 letters)
    Positions: (0,1), (1,1), (2,1), (3,1), (4,1)

--- SCORING STEPS ---
  Step 1: Intersecting Letters: 0 points
    Running Total: 0

  Step 2: Base Word Score: +15 points
    Running Total: 15
    Grid positions: (0,0), (1,0), (2,0), (3,0), (4,0), (0,1), (1,1), (2,1), (3,1), (4,1)

  Step 3: Final Score: 15 points

--- SUMMARY ---
  Intersection Score: 0
  Base Word Score: 15
  Final Score: 15
  Total Steps: 3
  Animation Duration: 2.1s
```

### In-Game Debug UI
- Expandable panels for each scoring event
- Real-time score tracking
- Export functionality for logs
- Color-coded entries by type

## Integration

### Existing Scoring System Integration
The debug system is integrated into the existing scoring system via:

1. **NumericalScoringData.cs**: Sends debug events when scoring is calculated
2. **GameManager.cs**: Updates debug UI with current score
3. **ModifierManager.cs**: Logs modifier activations

### Event System
Uses Unity's SendMessage system for loose coupling:
- Tag-based component finding ("DebugSystem" tag)
- Optional integration (no errors if debug system not present)
- Event-driven updates

## Setup Instructions

### Basic Setup
1. Create a Canvas for the debug UI
2. Add the `ScoringDebugUI` prefab to the canvas
3. Tag the debug UI GameObject as "DebugSystem"
4. Add `DebugToggleButton` to any UI element
5. Optionally add `ScoringDebugManager` for centralized control

### Scene Setup
```
Canvas
├── DebugUI (tag: "DebugSystem")
│   ├── ScoringDebugUI (component)
│   ├── Debug Panel
│   │   ├── Scroll View
│   │   ├── Toggle Buttons
│   │   └── Export Controls
│   └── Entry Prefabs
└── Debug Toggle Button
    └── DebugToggleButton (component)
```

### Prefab Requirements
- **Debug Entry Prefab**: Must have `ScoringDebugEntryUI` component
- **UI Elements**: Text components for titles, details, timestamps
- **Controls**: Buttons for expand/collapse, export, clear

## Configuration Options

### Runtime Configuration
```csharp
// Toggle verbose logging
ScoringDebugLogger.VerboseLogging = true/false;

// Enable rich text console output
ScoringDebugLogger.UseRichText = true/false;

// Show/hide debug UI
debugUI.ToggleDebugPanel();

// Enable/disable debug system
debugUI.SetDebugEnabled(true/false);
```

### Build Configuration
- Debug UI automatically hides in release builds
- Console logging remains active (can be disabled via preprocessor directives)
- Export functionality works in development builds only

## Debug Data Structure

### ScoringDebugEntry
```csharp
public class ScoringDebugEntry
{
    public DateTime timestamp;
    public EntryType entryType;
    public string title;
    public string details;
    public List<string> words;
    public int intersectionScore;
    public int baseScore;
    public int finalScore;
    public List<ScoreStep> steps;
}
```

### Entry Types
- **ScoringBreakdown**: Complete scoring analysis
- **LetterBreakdown**: Letter-by-letter scoring
- **ModifierInfo**: Active modifier details
- **Error**: Error conditions
- **Warning**: Warning conditions
- **Info**: General information

## Performance Considerations

### Optimization Features
- Automatic entry limit (keeps last 100 entries)
- Optional verbose mode
- Lazy UI updates (only when visible)
- Memory-efficient string building
- Optional rich text formatting

### Production Build
- UI components inactive in release builds
- Minimal console logging overhead
- Export functionality disabled
- Debug events ignored if no listeners

## Troubleshooting

### Common Issues
1. **Debug UI not showing**: Check GameObject tag is "DebugSystem"
2. **No debug output**: Verify `SetDebugEnabled(true)` is called
3. **Console formatting issues**: Disable rich text if console doesn't support it
4. **Performance impact**: Disable verbose mode in production

### Debug Commands
```csharp
// Force show debug UI
GameObject.FindGameObjectWithTag("DebugSystem")?.SendMessage("ToggleDebugPanel");

// Clear debug log
GameObject.FindGameObjectWithTag("DebugSystem")?.SendMessage("ClearDebugLog");

// Export debug log
GameObject.FindGameObjectWithTag("DebugSystem")?.SendMessage("ExportDebugLog");
```

## Extension Points

### Adding New Debug Entry Types
1. Add new `EntryType` enum value
2. Implement logging method in `ScoringDebugUI`
3. Add formatting in `ScoringDebugLogger`
4. Update UI color coding

### Custom Debug Events
```csharp
// Send custom debug event
var debugSystem = GameObject.FindGameObjectWithTag("DebugSystem");
debugSystem?.SendMessage("OnCustomDebugEvent", debugData, SendMessageOptions.DontRequireReceiver);
```

### Additional Logging
```csharp
// Add custom logging methods to ScoringDebugLogger
public static void LogCustomEvent(CustomData data)
{
    // Implementation
}
```

## Best Practices

1. **Use verbose mode only in development**
2. **Export logs for bug reports**
3. **Disable debug UI in production builds**
4. **Use rich text formatting sparingly**
5. **Clear debug logs periodically**
6. **Monitor memory usage with many entries**

## Files Overview

| File | Purpose | Key Methods |
|------|---------|-------------|
| ScoringDebugUI.cs | Main debug UI | LogScoringBreakdown, ToggleDebugPanel |
| ScoringDebugEntryUI.cs | Individual entries | Setup, ToggleExpanded |
| ScoringDebugLogger.cs | Enhanced logging | LogScoringBreakdown, LogLetterBreakdown |
| DebugToggleButton.cs | UI toggle control | ToggleDebugUI |
| ScoringDebugManager.cs | System management | SetupDebugSystem, LogGameState |

## Version History

- **v1.0**: Initial implementation with basic debug UI and console logging
- **v1.1**: Added rich text formatting and export functionality  
- **v1.2**: Integrated with existing scoring system via events
- **v1.3**: Added modifier debugging and letter-by-letter analysis
