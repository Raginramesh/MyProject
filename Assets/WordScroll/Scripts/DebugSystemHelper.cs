using UnityEngine;

/// <summary>
/// Utility script to help set up debug system tags and provide fallback logging
/// </summary>
public static class DebugSystemHelper
{
    private static bool tagExistsChecked = false;
    private static bool tagExists = false;
    
    /// <summary>
    /// Safely find a GameObject with the DebugSystem tag, handling missing tag gracefully
    /// </summary>
    public static GameObject FindDebugSystem()
    {
        if (!tagExistsChecked)
        {
            CheckIfTagExists();
        }
        
        if (!tagExists)
        {
            return null;
        }
        
        try
        {
            return GameObject.FindGameObjectWithTag("DebugSystem");
        }
        catch (UnityException)
        {
            tagExists = false;
            return null;
        }
    }
    
    /// <summary>
    /// Safely send a message to the debug system
    /// </summary>
    public static void SendMessageToDebugSystem(string methodName, object parameter = null)
    {
        var debugSystem = FindDebugSystem();
        if (debugSystem != null)
        {
            if (parameter != null)
            {
                debugSystem.SendMessage(methodName, parameter, SendMessageOptions.DontRequireReceiver);
            }
            else
            {
                debugSystem.SendMessage(methodName, SendMessageOptions.DontRequireReceiver);
            }
        }
    }
    
    /// <summary>
    /// Check if the DebugSystem tag exists
    /// </summary>
    private static void CheckIfTagExists()
    {
        try
        {
            // Try to find with the tag - this will throw an exception if tag doesn't exist
            GameObject.FindGameObjectWithTag("DebugSystem");
            tagExists = true;
        }
        catch (UnityException ex)
        {
            if (ex.Message.Contains("Tag: DebugSystem is not defined"))
            {
                tagExists = false;
                LogTagSetupInstructions();
            }
            else
            {
                tagExists = false;
            }
        }
        
        tagExistsChecked = true;
    }
    
    /// <summary>
    /// Log instructions for setting up the debug system
    /// </summary>
    private static void LogTagSetupInstructions()
    {
        Debug.Log("╔══════════════════════════════════════════════════════════════════╗");
        Debug.Log("║                     DEBUG SYSTEM SETUP                          ║");
        Debug.Log("╚══════════════════════════════════════════════════════════════════╝");
        Debug.Log("🔧 To enable the debug system, please add the 'DebugSystem' tag:");
        Debug.Log("   1. Go to Edit → Project Settings");
        Debug.Log("   2. Select 'Tags and Layers'");
        Debug.Log("   3. Click '+' under Tags section");
        Debug.Log("   4. Add tag: 'DebugSystem'");
        Debug.Log("");
        Debug.Log("ℹ️  The scoring debug system will work without this tag,");
        Debug.Log("   but advanced debug UI features will be disabled.");
        Debug.Log("══════════════════════════════════════════════════════════════════");
    }
    
    /// <summary>
    /// Check if debug system is available
    /// </summary>
    public static bool IsDebugSystemAvailable()
    {
        if (!tagExistsChecked)
        {
            CheckIfTagExists();
        }
        
        return tagExists && FindDebugSystem() != null;
    }
}
