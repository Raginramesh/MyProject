using UnityEngine;
using UnityEngine.Audio; // For AudioMixerGroup (though Feel's SO might handle this more)
using MoreMountains.Tools;
using Lofelt.NiceVibrations; // Correct namespace for HapticPatterns

public class AudioAndHapticsManager : MonoBehaviour
{
    public static AudioAndHapticsManager Instance { get; private set; }

    [Header("Music Clips")]
    [SerializeField] private AudioClip homeScreenMusic;
    [SerializeField] private AudioClip gameSceneMusic;

    [Header("Sound Effect Clips")]
    [SerializeField] private AudioClip genericButtonClickSFX;
    [SerializeField] private AudioClip cellScrollSFX;
    [SerializeField] private AudioClip cellSnapToOriginalPositionSFX;
    [SerializeField] private AudioClip cellSnapToNewPositionSFX;
    [SerializeField] private AudioClip wordScoreSFX;
    [SerializeField] private AudioClip multiplierIncreaseSFX;
    [SerializeField] private AudioClip letterReplacementSFX;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // --- Music Methods ---
    public void PlayHomeScreenMusic()
    {
        PlayMusic(homeScreenMusic);
    }

    public void PlayGameSceneMusic()
    {
        PlayMusic(gameSceneMusic);
    }

    private void PlayMusic(AudioClip musicClip)
    {
        if (musicClip == null)
        {
            Debug.LogWarning("AudioAndHapticsManager: Music clip is null.");
            return;
        }

        if (MMSoundManager.Instance == null)
        {
            Debug.LogError("AudioAndHapticsManager: MMSoundManager.Instance is null. Cannot play music.");
            return;
        }

        // Stop any currently playing music on the Music track first
        MMSoundManager.Instance.StopTrack(MMSoundManager.MMSoundManagerTracks.Music);

        MMSoundManagerPlayOptions options = MMSoundManagerPlayOptions.Default;
        options.Loop = true;
        options.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.Music;
        MMSoundManager.Instance.PlaySound(musicClip, options);
    }

    public void StopMusic()
    {
        if (MMSoundManager.Instance != null)
        {
            MMSoundManager.Instance.StopTrack(MMSoundManager.MMSoundManagerTracks.Music);
        }
    }

    // --- Sound Effect & Haptic Methods ---

    public void PlayButtonClick()
    {
        PlaySFX(genericButtonClickSFX, MMSoundManager.MMSoundManagerTracks.UI);
        HapticPatterns.PlayPreset(HapticPatterns.PresetType.LightImpact);
    }

    public void PlayCellScrollStep()
    {
        PlaySFX(cellScrollSFX, MMSoundManager.MMSoundManagerTracks.Sfx, 0.8f, 1.2f);
        HapticPatterns.PlayPreset(HapticPatterns.PresetType.Selection);
    }

    public void PlayCellSnapToOriginalPosition()
    {
        PlaySFX(cellSnapToOriginalPositionSFX, MMSoundManager.MMSoundManagerTracks.Sfx);
        HapticPatterns.PlayPreset(HapticPatterns.PresetType.MediumImpact);
    }

    public void PlayCellSnapToNewPosition()
    {
        PlaySFX(cellSnapToNewPositionSFX, MMSoundManager.MMSoundManagerTracks.Sfx);
        HapticPatterns.PlayPreset(HapticPatterns.PresetType.LightImpact);
    }

    public void PlayWordScoreSound()
    {
        PlaySFX(wordScoreSFX, MMSoundManager.MMSoundManagerTracks.Sfx);
        HapticPatterns.PlayPreset(HapticPatterns.PresetType.Success);
    }

    public void PlayMultiplierIncreaseSound()
    {
        PlaySFX(multiplierIncreaseSFX, MMSoundManager.MMSoundManagerTracks.Sfx);
        HapticPatterns.PlayPreset(HapticPatterns.PresetType.Warning);
    }

    public void PlayLetterReplacementSound()
    {
        PlaySFX(letterReplacementSFX, MMSoundManager.MMSoundManagerTracks.Sfx, 0.9f, 1.1f);
    }

    // Helper to Play SFX using MMSoundManager
    private void PlaySFX(AudioClip clip, MMSoundManager.MMSoundManagerTracks track, float minPitch = 1.0f, float maxPitch = 1.0f, float volume = 1.0f)
    {
        if (clip == null)
        {
            return;
        }

        if (MMSoundManager.Instance == null)
        {
            Debug.LogWarning("AudioAndHapticsManager: MMSoundManager.Instance is null. Cannot play SFX.");
            return;
        }

        MMSoundManagerPlayOptions options = MMSoundManagerPlayOptions.Default;
        options.MmSoundManagerTrack = track;
        options.Volume = volume;
        options.Pitch = Random.Range(minPitch, maxPitch);

        MMSoundManager.Instance.PlaySound(clip, options);
    }
}