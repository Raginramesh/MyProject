using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement; // Required for loading scenes
using System.Collections.Generic; // Required for Lists
using WordScroll.UI; // <<< ADDED for ModifierSelectionUI

public class UIManager_HomeScreen : MonoBehaviour
{
    [Header("Top HUD References")]
    [SerializeField] private TextMeshProUGUI coinText;
    [SerializeField] private TextMeshProUGUI heartText;
    [SerializeField] private Button settingsButton;

    [Header("Bottom Tab References")]
    [SerializeField] private List<Button> tabButtons; // Assign buttons in Inspector (HomeTabButton first)

    [Header("Content Panel References")]
    [SerializeField] private List<GameObject> contentPanels; // Assign panels in Inspector (HomeContentPanel first)

    [Header("Home Content References")]
    [SerializeField] private Button playButton; // Assign the Play button inside HomeContentPanel

    [Header("Scene To Load")]
    [SerializeField] private string gameSceneName = "WordScrollGame";

    [Header("Modifier Selection UI")] // <<< CORRECTED HEADER PLACEMENT
    [SerializeField] private ModifierSelectionUI modifierSelectionUI; // <<< ADDED FIELD

    void Start()
    {
        // --- Validate References ---
        if (coinText == null || heartText == null || settingsButton == null || playButton == null || tabButtons.Count == 0 || contentPanels.Count == 0 || tabButtons.Count != contentPanels.Count)
        {
            Debug.LogError("UIManager_HomeScreen: UI References not set correctly in Inspector!");
            this.enabled = false; 
            return;
        }
        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogError("UIManager_HomeScreen: Game Scene Name is not set in Inspector!");
            this.enabled = false;
            return;
        }
        if (modifierSelectionUI == null) 
        {
            Debug.LogWarning("UIManager_HomeScreen: ModifierSelectionUI is not set in Inspector! Modifier selection will not be available.");
            // Not returning or disabling, so the game can still function without modifiers if not set up.
        }


        // --- Add Listeners ---
        settingsButton.onClick.AddListener(OnSettingsClicked);
        playButton.onClick.AddListener(OnPlayClicked);

        for (int i = 0; i < tabButtons.Count; i++)
        {
            int index = i; 
            tabButtons[i].onClick.AddListener(() => SelectTab(index));
        }

        // --- Initial State ---
        UpdateResourceUI();
        SelectTab(0); 
    }

    void OnDestroy()
    {
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettingsClicked);
        if (playButton != null) playButton.onClick.RemoveListener(OnPlayClicked);

        for (int i = 0; i < tabButtons.Count; i++)
        {
            if (tabButtons[i] != null) tabButtons[i].onClick.RemoveAllListeners(); 
        }
    }

    void UpdateResourceUI()
    {
        if (coinText != null) coinText.text = PlayerData.Coins.ToString();
        if (heartText != null) heartText.text = PlayerData.Hearts.ToString();
    }

    void SelectTab(int tabIndex)
    {
        Debug.Log($"Selecting Tab: {tabIndex}");
        if (tabIndex < 0 || tabIndex >= contentPanels.Count)
        {
            Debug.LogError($"Invalid tab index: {tabIndex}");
            return;
        }

        for (int i = 0; i < contentPanels.Count; i++)
        {
            if (contentPanels[i] != null)
                contentPanels[i].SetActive(false);
        }

        if (contentPanels[tabIndex] != null)
            contentPanels[tabIndex].SetActive(true);

        for (int i = 0; i < tabButtons.Count; i++)
        {
            var colors = tabButtons[i].colors;
            colors.colorMultiplier = (i == tabIndex) ? 1f : 0.8f; 
            tabButtons[i].colors = colors;
        }
    }

    void OnSettingsClicked()
    {
        Debug.Log("Settings Button Clicked!");
    }

    void OnPlayClicked()
    {
        Debug.Log("UIManager_HomeScreen: OnPlayClicked() initiated."); // <<< ADDED DEBUG

        if (modifierSelectionUI != null)
        {
            Debug.Log("UIManager_HomeScreen: modifierSelectionUI reference is VALID. Attempting to show."); // <<< ADDED DEBUG
            modifierSelectionUI.Show();
        }
        else
        {
            Debug.LogError("UIManager_HomeScreen: OnPlayClicked - modifierSelectionUI reference is NULL!"); // <<< CHANGED TO LOGERROR
            // Fallback: Load game directly, or do nothing, or show an error to the player.
            Debug.LogWarning("Fallback: Loading game scene directly as ModifierSelectionUI is missing.");
            SceneManager.LoadScene(gameSceneName);
        }
    }

    /// <summary>
    /// Shows the main home content panel. Typically called when ModifierSelectionUI is closed.
    /// </summary>
    public void ShowHomeContent()
    {
        // Ensure the home tab (index 0) and its content are active.
        SelectTab(0); 
    }
}