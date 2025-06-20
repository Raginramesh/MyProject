using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple debug toggle button that can be placed in any scene to show/hide the scoring debug UI
/// </summary>
public class DebugToggleButton : MonoBehaviour
{
    [Header("Debug Button")]
    [SerializeField] private Button debugButton;
    [SerializeField] private GameObject buttonParent;
    
    [Header("Settings")]
    [SerializeField] private bool showInRelease = false;
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;
    
    private void Start()
    {
        // Hide debug button in release builds unless explicitly enabled
        #if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        if (!showInRelease && buttonParent != null)
        {
            buttonParent.SetActive(false);
            return;
        }
        #endif
        
        // Setup button
        if (debugButton == null)
            debugButton = GetComponent<Button>();
            
        if (debugButton != null)
            debugButton.onClick.AddListener(ToggleDebugUI);
    }
    
    private void Update()
    {
        // Allow keyboard toggle
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleDebugUI();
        }
    }
    
    private void ToggleDebugUI()
    {
        var debugSystem = GameObject.FindGameObjectWithTag("DebugSystem");
        if (debugSystem != null)
        {
            debugSystem.SendMessage("ToggleDebugPanel", SendMessageOptions.DontRequireReceiver);
        }
        else
        {
            Debug.LogWarning("DebugToggleButton: No debug system found with tag 'DebugSystem'");
        }
    }
}
