using UnityEngine;
using UnityEditor;

namespace WordScroll.Modifiers.Editor
{
    [CustomEditor(typeof(ModifierCardData))]
    [CanEditMultipleObjects]
    public class ModifierCardDataEditor : UnityEditor.Editor
    {
        SerializedProperty cardNameProp;
        SerializedProperty cardTypeProp;
        SerializedProperty rarityProp;
        SerializedProperty iconProp;
        SerializedProperty effectDescriptionProp;
        SerializedProperty strategicUseCaseProp;
        SerializedProperty synergyNotesProp;
        SerializedProperty effectTypeProp;

        // Parameters for SpecificWordLengthScoreBonus
        SerializedProperty targetWordLengthProp;
        SerializedProperty wordLengthScoreMultiplierProp;

        // Parameters for GeneralScoreBonusAndMoveReduction
        SerializedProperty generalScoreMultiplierProp;
        SerializedProperty moveReductionPercentageProp;

        // Parameters for VowelCountBonus
        SerializedProperty minVowelCountProp;
        SerializedProperty vowelBonusPointsProp;

        void OnEnable()
        {
            // Card Identity
            cardNameProp = serializedObject.FindProperty("cardName");
            cardTypeProp = serializedObject.FindProperty("cardType");
            rarityProp = serializedObject.FindProperty("rarity");
            iconProp = serializedObject.FindProperty("icon");

            // Card Details
            effectDescriptionProp = serializedObject.FindProperty("effectDescription");
            strategicUseCaseProp = serializedObject.FindProperty("strategicUseCase");
            synergyNotesProp = serializedObject.FindProperty("synergyNotes");

            // Core Effect Definition
            effectTypeProp = serializedObject.FindProperty("effectType");

            // Effect-specific parameters
            targetWordLengthProp = serializedObject.FindProperty("targetWordLength");
            wordLengthScoreMultiplierProp = serializedObject.FindProperty("wordLengthScoreMultiplier");

            generalScoreMultiplierProp = serializedObject.FindProperty("generalScoreMultiplier");
            moveReductionPercentageProp = serializedObject.FindProperty("moveReductionPercentage");

            minVowelCountProp = serializedObject.FindProperty("minVowelCount");
            vowelBonusPointsProp = serializedObject.FindProperty("vowelBonusPoints");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Card Identity", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(cardNameProp);
            EditorGUILayout.PropertyField(cardTypeProp);
            EditorGUILayout.PropertyField(rarityProp);
            EditorGUILayout.PropertyField(iconProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Card Details", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(effectDescriptionProp);
            EditorGUILayout.PropertyField(strategicUseCaseProp);
            EditorGUILayout.PropertyField(synergyNotesProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Core Effect Definition", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(effectTypeProp);

            // Cast the enum value to use in switch
            ModifierEffectType currentEffectType = (ModifierEffectType)effectTypeProp.enumValueIndex;

            switch (currentEffectType)
            {
                case ModifierEffectType.SpecificWordLengthScoreBonus:
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Specific Word Length Score Bonus Params", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(targetWordLengthProp);
                    EditorGUILayout.PropertyField(wordLengthScoreMultiplierProp);
                    break;

                case ModifierEffectType.GeneralScoreBonusAndMoveReduction:
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("General Score Bonus & Move Reduction Params", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(generalScoreMultiplierProp);
                    EditorGUILayout.PropertyField(moveReductionPercentageProp);
                    break;

                case ModifierEffectType.VowelCountBonus:
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Vowel Count Bonus Params", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(minVowelCountProp);
                    EditorGUILayout.PropertyField(vowelBonusPointsProp);
                    break;

                case ModifierEffectType.None:
                    // Optionally show a message or nothing
                    EditorGUILayout.HelpBox("No specific effect parameters for 'None' type.", MessageType.Info);
                    break;
                // Add cases for new ModifierEffectType values here
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
