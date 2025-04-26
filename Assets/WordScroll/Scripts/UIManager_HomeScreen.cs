using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement; // Required for loading scenes
using System.Collections.Generic; // Required for Lists

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
    [SerializeField] private string gameSceneName = "WordScrollGame"; // <<< SET YOUR GAME SCENE NAME HERE

    void Start()
    {
        // --- Validate References ---
        if (coinText == null || heartText == null || settingsButton == null || playButton == null || tabButtons.Count == 0 || contentPanels.Count == 0 || tabButtons.Count != contentPanels.Count)
        {
            Debug.LogError("UIManager_HomeScreen: References not set correctly in Inspector!");
            this.enabled = false; // Disable script if setup is wrong
            return;
        }
        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogError("UIManager_HomeScreen: Game Scene Name is not set in Inspector!");
            this.enabled = false;
            return;
        }


        // --- Add Listeners ---
        settingsButton.onClick.AddListener(OnSettingsClicked);
        playButton.onClick.AddListener(OnPlayClicked);

        // Add listeners for each tab button
        for (int i = 0; i < tabButtons.Count; i++)
        {
            int index = i; // Required to capture the correct index in the lambda
            tabButtons[i].onClick.AddListener(() => SelectTab(index));
        }

        // --- Initial State ---
        UpdateResourceUI();
        SelectTab(0); // Select the first tab (Home) by default
    }

    void OnDestroy()
    {
        // --- Remove Listeners ---
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettingsClicked);
        if (playButton != null) playButton.onClick.RemoveListener(OnPlayClicked);

        // Remove tab button listeners
        for (int i = 0; i < tabButtons.Count; i++)
        {
            if (tabButtons[i] != null) tabButtons[i].onClick.RemoveAllListeners(); // Simple removal
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

        // Deactivate all content panels
        for (int i = 0; i < contentPanels.Count; i++)
        {
            if (contentPanels[i] != null)
                contentPanels[i].SetActive(false);
        }

        // Activate the selected one
        if (contentPanels[tabIndex] != null)
            contentPanels[tabIndex].SetActive(true);

        // Optional: Add visual feedback to buttons (e.g., change color of selected tab)
        for (int i = 0; i < tabButtons.Count; i++)
        {
            // Example: Change interactable state or color tint
            var colors = tabButtons[i].colors;
            colors.colorMultiplier = (i == tabIndex) ? 1f : 0.8f; // Make selected slightly brighter
            tabButtons[i].colors = colors;
        }
    }

    // --- Button Click Handlers ---

    void OnSettingsClicked()
    {
        Debug.Log("Settings Button Clicked!");
        // TODO: Implement settings panel logic
    }

    void OnPlayClicked()
    {
        Debug.Log($"Play Button Clicked! Loading scene: {gameSceneName}");
        // Load the main game scene
        // Ensure the scene is added to Build Settings!
        SceneManager.LoadScene(gameSceneName);
    }
}