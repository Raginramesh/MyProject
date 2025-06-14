using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace WordScroll.Modifiers
{
    public class ModifierManager : MonoBehaviour
    {
        [Header("Modifier Configuration")]
        [Tooltip("All possible ModifierCardData assets that can be drawn for selection. Populate this in the Inspector.")]
        public List<ModifierCardData> allAvailableModifierCards = new List<ModifierCardData>();

        [Tooltip("How many modifiers to offer to the player at selection time.")]
        public int numberOfModifiersToOffer = 3;

        [Header("Runtime State")]
        [Tooltip("The list of modifiers currently active in the game.")]
        private List<ModifierCardData> activeModifiers = new List<ModifierCardData>();

        [Tooltip("The current selection of modifiers offered to the player.")]
        private List<ModifierCardData> currentOfferedModifiers = new List<ModifierCardData>();

        // --- Public Methods ---

        /// <summary>
        /// Generates a new selection of modifiers to be offered to the player.
        /// These are typically of CardTypeTag.Modifier.
        /// </summary>
        /// <returns>A list of ModifierCardData to be offered.</returns>
        public List<ModifierCardData> GetNewModifierOffer()
        {
            currentOfferedModifiers.Clear();
            List<ModifierCardData> potentialModifiers = allAvailableModifierCards
                .Where(card => card.cardType == CardTypeTag.Modifier) // Only offer 'Modifier' type cards for now
                .ToList();

            if (potentialModifiers.Count == 0)
            {
                Debug.LogWarning("ModifierManager: No 'Modifier' type cards available in allAvailableModifierCards to offer.");
                return currentOfferedModifiers;
            }

            // Shuffle the potential modifiers
            System.Random rng = new System.Random();
            List<ModifierCardData> shuffledModifiers = potentialModifiers.OrderBy(a => rng.Next()).ToList();

            int countToOffer = Mathf.Min(numberOfModifiersToOffer, shuffledModifiers.Count);

            for (int i = 0; i < countToOffer; i++)
            {
                currentOfferedModifiers.Add(shuffledModifiers[i]);
            }

            Debug.Log($"Offered {currentOfferedModifiers.Count} modifiers.");
            return new List<ModifierCardData>(currentOfferedModifiers); // Return a copy
        }

        /// <summary>
        /// Activates a modifier chosen by the player from the current offer.
        /// </summary>
        /// <param name="chosenModifier">The ModifierCardData chosen by the player.</param>
        public void ActivateModifier(ModifierCardData chosenModifier)
        {
            if (chosenModifier == null)
            {
                Debug.LogWarning("ModifierManager: Tried to activate a null modifier.");
                return;
            }

            if (!currentOfferedModifiers.Contains(chosenModifier))
            {
                Debug.LogWarning($"ModifierManager: Tried to activate modifier '{chosenModifier.cardName}' which was not in the current offer.");
                // Optionally, still allow activation if this is desired behavior for other systems (e.g. gifts)
            }

            if (activeModifiers.Contains(chosenModifier) && chosenModifier.cardType == CardTypeTag.Modifier)
            {
                // For some modifier types, stacking might be allowed or they might refresh a timer.
                // For now, let's prevent duplicate 'Modifier' types unless designed to stack.
                Debug.Log($"ModifierManager: Modifier '{chosenModifier.cardName}' is already active. Behavior for re-activation not yet defined.");
                return; // Or handle re-activation/stacking logic here
            }

            activeModifiers.Add(chosenModifier);
            Debug.Log($"ModifierManager: Activated modifier '{chosenModifier.cardName}'. Type: {chosenModifier.cardType}");

            // TODO: Implement logic for temporary modifiers (e.g., start a timer to deactivate them)
            // TODO: Broadcast an event or call a method to notify other systems that a modifier has been activated.
        }

        /// <summary>
        /// Deactivates a specific modifier.
        /// </summary>
        /// <param name="modifierToDeactivate">The modifier to remove from the active list.</param>
        public void DeactivateModifier(ModifierCardData modifierToDeactivate)
        {
            if (modifierToDeactivate != null && activeModifiers.Contains(modifierToDeactivate))
            {
                activeModifiers.Remove(modifierToDeactivate);
                Debug.Log($"ModifierManager: Deactivated modifier '{modifierToDeactivate.cardName}'.");
                // TODO: Broadcast an event or call a method to notify other systems that a modifier has been deactivated.
            }
        }

        /// <summary>
        /// Clears all currently active modifiers. Useful for game end or level reset.
        /// </summary>
        public void ClearAllActiveModifiers()
        {
            if (activeModifiers.Count > 0)
            {
                Debug.Log("ModifierManager: Clearing all active modifiers.");
                // Potentially call DeactivateModifier for each if they have specific deactivation logic/events
                activeModifiers.Clear();
                // TODO: Broadcast an event or notify systems if needed.
            }
        }

        /// <summary>
        /// Checks if a modifier with a specific effect type is currently active.
        /// </summary>
        /// <param name="effectType">The effect type to check for.</param>
        /// <returns>True if an active modifier has this effect type, false otherwise.</returns>
        public bool IsModifierEffectActive(ModifierEffectType effectType)
        {
            return activeModifiers.Any(mod => mod.effectType == effectType);
        }

        /// <summary>
        /// Gets the first active modifier of a specific effect type.
        /// Useful if you expect only one of a certain type, or want its parameters.
        /// </summary>
        /// <param name="effectType">The effect type to retrieve.</param>
        /// <returns>The ModifierCardData if found, otherwise null.</returns>
        public ModifierCardData GetActiveModifierByType(ModifierEffectType effectType)
        {
            return activeModifiers.FirstOrDefault(mod => mod.effectType == effectType);
        }

        /// <summary>
        /// Gets all currently active modifiers.
        /// </summary>
        /// <returns>A new list containing all active ModifierCardData.</returns>
        public List<ModifierCardData> GetAllActiveModifiers()
        {
            return new List<ModifierCardData>(activeModifiers);
        }

        // --- TODO: Gift and Upgrade Card Handling ---
        // Methods for managing gift inventory (AddGift, UseGift, GetGiftCount)
        // Methods for managing equipped upgrades (EquipUpgrade, UnequipUpgrade, IsUpgradeEquipped)

    }
}
