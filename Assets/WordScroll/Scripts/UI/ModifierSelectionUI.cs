using UnityEngine;
using UnityEngine.UI; // Required for Button
using System.Collections.Generic;
using WordScroll.Modifiers; // Required for ModifierManager and ModifierCardData
using TMPro; // If using TextMeshPro for any text elements in this UI
using UnityEngine.SceneManagement; // Required for loading scenes

namespace WordScroll.UI
{
    public class ModifierSelectionUI : MonoBehaviour
    {
        [Header("Panel To Control")] 
        [Tooltip("Assign the main panel GameObject for this UI that should be shown/hidden.")]
        public GameObject modifierSelectionPanelObject; 

        [Header("Manager References")]
        public ModifierManager modifierManager; 

        [Header("Scene Configuration")]
        public string gameSceneName = "WordScroll"; 

        [Header("UI Prefab & Layout")]
        public GameObject modifierCardDisplayItemPrefab; 
        public Transform cardDisplayParent; 

        [Header("UI Elements")]
        public Button rerollButton; 
        public TextMeshProUGUI rerollCostText; 
        public Button playGameButton; 
        public Button closeButton; 

        [Header("Reroll Configuration")]
        public int baseRerollCost = 100; // Example cost
        public int rerollCostIncrease = 50; // Example cost increase per reroll
        private int _currentRerollCost;

        private List<GameObject> _instantiatedCardItems = new List<GameObject>();
        private ModifierCardData _selectedModifierForGame = null;
        private UIManager_HomeScreen _homeScreenManager; 
        private ModifierCardDisplayItem _currentlySelectedCardDisplay = null; // Added to track the selected card's display item

        void Awake()
        {
            // Use FindFirstObjectByType instead of FindObjectOfType
            _homeScreenManager = FindFirstObjectByType<UIManager_HomeScreen>(); 
            
            if (modifierSelectionPanelObject != null)
            {
                modifierSelectionPanelObject.SetActive(false);
            }
            else
            {
                Debug.LogWarning("ModifierSelectionUI: modifierSelectionPanelObject is not assigned in Awake. The UI might not hide as expected initially. Ensure it's assigned in the Inspector.");
            }
        }

        void Start()
        {
            if (modifierSelectionPanelObject == null) 
            {
                Debug.LogError("ModifierSelectionUI: ModifierSelectionPanelObject is not assigned in the Inspector! UI cannot be shown or hidden correctly. Please assign it.");
                return; 
            }

            if (modifierManager == null)
            {
                Debug.LogError("ModifierSelectionUI: ModifierManager not assigned!");
                modifierSelectionPanelObject.SetActive(false); // Hide panel if critical component is missing
                return;
            }
            if (modifierCardDisplayItemPrefab == null)
            {
                Debug.LogError("ModifierSelectionUI: ModifierCardDisplayItem Prefab not assigned!");
                modifierSelectionPanelObject.SetActive(false); 
                return;
            }
            if (cardDisplayParent == null)
            {
                Debug.LogError("ModifierSelectionUI: CardDisplayParent not assigned!");
                modifierSelectionPanelObject.SetActive(false); 
                return;
            }
            if (playGameButton == null)
            {
                Debug.LogWarning("ModifierSelectionUI: Play Game Button not assigned! Proceeding without it.");
            }
            else
            {
                playGameButton.onClick.AddListener(OnPlayGameClicked);
                playGameButton.interactable = false; 
            }

            if (rerollButton != null)
            {
                rerollButton.onClick.AddListener(OnRerollClicked);
            }
            // Initialize reroll cost
            _currentRerollCost = baseRerollCost;
            UpdateRerollCostDisplay();

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(OnClosePanelClicked);
            }
        }

        public void Show()
        {
            if (modifierSelectionPanelObject == null)
            {
                Debug.LogError("ModifierSelectionUI: Cannot Show - modifierSelectionPanelObject is not assigned!");
                return; 
            }
            modifierSelectionPanelObject.SetActive(true);
            _selectedModifierForGame = null; 
            if(playGameButton != null) playGameButton.interactable = false; 
            // Reset reroll cost if the panel is shown anew (e.g. after a game)
            // Or, if cost should persist across showings, this line can be removed.
            _currentRerollCost = baseRerollCost;
            UpdateRerollCostDisplay();
            PopulateModifierOffers();
            // Ensure no card appears selected initially when shown
            if (_currentlySelectedCardDisplay != null)
            {
                _currentlySelectedCardDisplay.SetSelected(false);
                _currentlySelectedCardDisplay = null;
            }
        }

        public void Hide()
        {
            if (modifierSelectionPanelObject == null)
            {
                Debug.LogWarning("ModifierSelectionUI: Cannot Hide - modifierSelectionPanelObject is not assigned.");
                return;
            }
            modifierSelectionPanelObject.SetActive(false);
        }

        void PopulateModifierOffers()
        {
            if (modifierSelectionPanelObject == null) return; // Should not happen if Start checks passed

            foreach (GameObject item in _instantiatedCardItems)
            {
                Destroy(item);
            }
            _instantiatedCardItems.Clear();

            if (modifierManager == null) return;

            List<ModifierCardData> offers = modifierManager.GetNewModifierOffer();

            if (offers.Count == 0)
            {
                Debug.LogWarning("ModifierSelectionUI: No modifier offers received from ModifierManager.");
                return;
            }

            foreach (ModifierCardData cardData in offers)
            {
                if (modifierCardDisplayItemPrefab == null) continue;

                GameObject cardInstance = Instantiate(modifierCardDisplayItemPrefab, cardDisplayParent);
                ModifierCardDisplayItem displayItem = cardInstance.GetComponent<ModifierCardDisplayItem>();

                if (displayItem != null)
                {
                    displayItem.Initialize(cardData, HandleModifierCardSelection);
                    _instantiatedCardItems.Add(cardInstance);
                }
                else
                {
                    Debug.LogError("ModifierSelectionUI: Instantiated card item is missing ModifierCardDisplayItem component.");
                    Destroy(cardInstance); // Clean up unusable instance
                }
            }
        }

        void HandleModifierCardSelection(ModifierCardData selectedCardData)
        {
            _selectedModifierForGame = selectedCardData;
            if(playGameButton != null) playGameButton.interactable = true;

            // Update visual selection state for all cards
            foreach (GameObject cardItemGO in _instantiatedCardItems)
            {
                ModifierCardDisplayItem displayItem = cardItemGO.GetComponent<ModifierCardDisplayItem>();
                if (displayItem != null)
                {
                    // bool isThisCardSelected = (displayItem._cardData == selectedCardData); // Accessing _cardData directly for comparison - This was causing an error
                    bool isThisCardSelected = (displayItem.CardData == selectedCardData); // Use the public CardData property
                    displayItem.SetSelected(isThisCardSelected);
                    if (isThisCardSelected)
                    {
                        _currentlySelectedCardDisplay = displayItem;
                    }
                }
            }

            Debug.Log($"ModifierSelectionUI: Player selected modifier: {selectedCardData.cardName}");
        }

        void OnPlayGameClicked()
        {
            if (_selectedModifierForGame == null)
            {
                Debug.LogWarning("ModifierSelectionUI: Play clicked but no modifier selected.");
                // Optionally show a message to the player to select a modifier first.
                // For now, we'll prevent proceeding without a selection.
                // You could add a TMPro text field to show messages like "Please select a modifier!"
                return; 
            }

            if (modifierManager != null && _selectedModifierForGame != null)
            {
                modifierManager.ActivateModifier(_selectedModifierForGame); 
                Debug.Log($"ModifierSelectionUI: Activating '{_selectedModifierForGame.cardName}' and loading game scene.");
            }
            
            if (string.IsNullOrEmpty(gameSceneName))
            {
                Debug.LogError("ModifierSelectionUI: Game Scene Name is not set!");
                return;
            }
            SceneManager.LoadScene(gameSceneName);
            // Hide(); // Panel will be destroyed with scene load, but good practice if it were persistent
        }

        void OnRerollClicked()
        {
            // Placeholder for checking player currency
            bool canAffordReroll = CheckPlayerCurrency(_currentRerollCost);

            if (canAffordReroll)
            {
                Debug.Log($"ModifierSelectionUI: Rerolling modifiers. Cost: {_currentRerollCost}");
                // Placeholder for deducting currency
                // DeductPlayerCurrency(_currentRerollCost);

                // Clear current selection and offers
                _selectedModifierForGame = null;
                if (_currentlySelectedCardDisplay != null)
                {
                    _currentlySelectedCardDisplay.SetSelected(false);
                    _currentlySelectedCardDisplay = null;
                }
                if(playGameButton != null) playGameButton.interactable = false;

                PopulateModifierOffers(); // This will clear old cards and create new ones

                // Increase cost for next reroll
                _currentRerollCost += rerollCostIncrease;
                UpdateRerollCostDisplay();
            }
            else
            {
                Debug.LogWarning("ModifierSelectionUI: Cannot reroll. Not enough currency (placeholder check).");
                // Optionally, provide UI feedback like disabling the button or showing a message
                // rerollButton.interactable = false; // Example: disable if cannot afford
            }
        }

        void OnClosePanelClicked()
        {
            Hide();
            if (_homeScreenManager != null)
            {
                _homeScreenManager.ShowHomeContent();
            }
            else
            {
                Debug.LogWarning("ModifierSelectionUI: HomeScreenManager not found, cannot explicitly re-show home content.");
            }
        }

        // --- Helper Methods ---

        private void UpdateRerollCostDisplay()
        {
            if (rerollCostText != null)
            {
                rerollCostText.text = $"Reroll ({_currentRerollCost})";
            }
        }

        // Placeholder for actual currency check logic
        private bool CheckPlayerCurrency(int amount)
        {
            // In a real game, this would check against a player inventory/currency manager
            Debug.Log($"Placeholder: Checking if player can afford {amount}. Assuming YES for now.");
            return true; // Always return true for now
        }

        // Placeholder for actual currency deduction logic
        // private void DeductPlayerCurrency(int amount)
        // {
        //     // In a real game, this would deduct from a player inventory/currency manager
        //     Debug.Log($"Placeholder: Deducting {amount} currency.");
        // }

    }
}
