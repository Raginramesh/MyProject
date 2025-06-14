using UnityEngine;
using UnityEngine.UI;
using TMPro; // If using TextMeshPro
using System;
using WordScroll.Modifiers; // Added this line

namespace WordScroll.UI
{
    public class ModifierCardDisplayItem : MonoBehaviour
    {
        [Header("UI References")]
        public TextMeshProUGUI cardNameText; // Or public Text cardNameText;
        public TextMeshProUGUI effectDescriptionText; // Or public Text effectDescriptionText;
        public Image cardIconImage;
        public Button selectButton;
        public Image selectionIndicator; // Added: Assign an Image UI element (e.g., a border or checkmark) in the prefab

        private ModifierCardData _cardData;
        public ModifierCardData CardData => _cardData; // Public getter for _cardData
        private Action<ModifierCardData> _onCardSelectedCallback;

        /// <summary>
        /// Sets up the display item with the given card data and selection callback.
        /// </summary>
        public void Initialize(ModifierCardData cardData, Action<ModifierCardData> onCardSelectedCallback)
        {
            _cardData = cardData;
            _onCardSelectedCallback = onCardSelectedCallback;

            if (_cardData == null)
            {
                Debug.LogError("ModifierCardDisplayItem: CardData is null.");
                // Optionally disable the game object or show an error state
                gameObject.SetActive(false);
                return;
            }

            if (cardNameText != null) cardNameText.text = _cardData.cardName;
            if (effectDescriptionText != null) effectDescriptionText.text = _cardData.effectDescription;
            if (cardIconImage != null)
            {
                cardIconImage.sprite = _cardData.icon;
                cardIconImage.enabled = _cardData.icon != null;
            }

            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners(); // Clear previous listeners
                selectButton.onClick.AddListener(OnSelectButtonClicked);
            }
            else
            {
                Debug.LogWarning("ModifierCardDisplayItem: Select Button is not assigned.");
            }

            // Initialize selection indicator (e.g., hide it by default)
            if (selectionIndicator != null) selectionIndicator.enabled = false;
        }

        private void OnSelectButtonClicked()
        {
            if (_cardData != null && _onCardSelectedCallback != null)
            {
                _onCardSelectedCallback(_cardData);
            }
            else
            {
                Debug.LogError("ModifierCardDisplayItem: CardData or selection callback is null on button click.");
            }
        }

        /// <summary>
        /// Sets the visual state of the card to indicate whether it is selected.
        /// </summary>
        /// <param name="isSelected">True if the card should appear selected, false otherwise.</param>
        public void SetSelected(bool isSelected)
        {
            if (selectionIndicator != null)
            {
                selectionIndicator.enabled = isSelected;
            }
            // Optional: Add other visual changes, e.g., change button text, background color, etc.
            // if (isSelected)
            // {
            //     // Example: Change background color
            //     GetComponent<Image>().color = Color.yellow; 
            // }
            // else
            // {
            //     // Example: Reset background color
            //     GetComponent<Image>().color = Color.white; 
            // }
        }

        // Optional: Add methods for hover effects, highlighting, etc.
    }
}
