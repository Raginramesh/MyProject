using UnityEngine;
using UnityEngine.UI;

public class AudioTestUI : MonoBehaviour
{
    [Header("Test Buttons")]
    [SerializeField] private Button testMusicButton;
    [SerializeField] private Button testButtonClickButton;
    [SerializeField] private Button testWordScoreButton;
    [SerializeField] private Button testCellScrollButton;

    void Start()
    {
        SetupTestButtons();
    }

    private void SetupTestButtons()
    {
        if (testMusicButton != null)
            testMusicButton.onClick.AddListener(() => {
                if (AudioAndHapticsManager.Instance != null)
                    AudioAndHapticsManager.Instance.PlayGameSceneMusic();
            });

        if (testButtonClickButton != null)
            testButtonClickButton.onClick.AddListener(() => {
                if (AudioAndHapticsManager.Instance != null)
                    AudioAndHapticsManager.Instance.PlayButtonClick();
            });

        if (testWordScoreButton != null)
            testWordScoreButton.onClick.AddListener(() => {
                if (AudioAndHapticsManager.Instance != null)
                    AudioAndHapticsManager.Instance.PlayWordScoreSound();
            });

        if (testCellScrollButton != null)
            testCellScrollButton.onClick.AddListener(() => {
                if (AudioAndHapticsManager.Instance != null)
                    AudioAndHapticsManager.Instance.PlayCellScrollStep();
            });
    }
}
