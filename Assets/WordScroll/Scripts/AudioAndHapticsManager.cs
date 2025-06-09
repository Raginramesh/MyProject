using UnityEngine;
using UnityEngine.Audio; // Added for AudioMixerGroup
using MoreMountains.Tools; // For MMSoundManager features (if using Feel for audio)
using Lofelt.NiceVibrations; // Ensure this is the correct namespace for HapticPatterns

public class AudioAndHapticsManager : MonoBehaviour
{
    public static AudioAndHapticsManager Instance { get; private set; }

    [Header("Audio Sources & Mixers (Optional - Feel handles this)")]
    [SerializeField] private AudioMixerGroup musicMixerGroup;
    [SerializeField] private AudioMixerGroup sfxMixerGroup;

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

    private AudioSource _musicAudioSource;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _musicAudioSource = gameObject.GetComponent<AudioSource>();
            if (_musicAudioSource == null)
            {
                _musicAudioSource = gameObject.AddComponent<AudioSource>();
            }
            _musicAudioSource.loop = true;
            if (musicMixerGroup != null)
            {
                _musicAudioSource.outputAudioMixerGroup = musicMixerGroup;
            }
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

        if (_musicAudioSource.clip == musicClip && _musicAudioSource.isPlaying) return;
        _musicAudioSource.clip = musicClip;
        _musicAudioSource.Play();
    }

    public void StopMusic()
    {
        _musicAudioSource.Stop();
    }

    // --- Sound Effect & Haptic Methods ---

    // Generic Button Click
    public void PlayButtonClick()
    {
        PlaySFX(genericButtonClickSFX);
        HapticPatterns.PlayPreset(HapticPatterns.PresetType.LightImpact);
    }

    // Cell Scrolling (continuous or per step)
    public void PlayCellScrollStep()
    {
        PlaySFX(cellScrollSFX, 0.8f, 1.2f);
        HapticPatterns.PlayPreset(HapticPatterns.PresetType.Selection);
    }

    // Cell Snaps back to its original data position (full cycle scroll)
    public void PlayCellSnapToOriginalPosition()
    {
        PlaySFX(cellSnapToOriginalPositionSFX);
        HapticPatterns.PlayPreset(HapticPatterns.PresetType.MediumImpact);
    }

    // Cell Snaps to a new data position (after a partial scroll and release)
    public void PlayCellSnapToNewPosition()
    {
        PlaySFX(cellSnapToNewPositionSFX);
        HapticPatterns.PlayPreset(HapticPatterns.PresetType.LightImpact);
    }

    // Word Scored (base score part)
    public void PlayWordScoreSound()
    {
        PlaySFX(wordScoreSFX);
        HapticPatterns.PlayPreset(HapticPatterns.PresetType.Success);
    }

    // Multiplier Increase (e.g., "2X!")
    public void PlayMultiplierIncreaseSound()
    {
        PlaySFX(multiplierIncreaseSFX);
        HapticPatterns.PlayPreset(HapticPatterns.PresetType.Warning);
    }

    // Letters are replaced on the grid
    public void PlayLetterReplacementSound()
    {
        PlaySFX(letterReplacementSFX, 0.9f, 1.1f);
        // Optional: HapticPatterns.PlayPreset(HapticPatterns.PresetType.LightImpact);
    }

    // --- Helper to Play SFX ---
    private void PlaySFX(AudioClip clip, float minPitch = 1.0f, float maxPitch = 1.0f, float volume = 1.0f)
    {
        if (clip == null)
        {
            return;
        }

        MMSoundManagerPlayOptions options = MMSoundManagerPlayOptions.Default;
        options.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.Sfx;
        options.Volume = volume;
        options.Pitch = Random.Range(minPitch, maxPitch);

        if (MMSoundManager.Instance != null)
        {
            MMSoundManager.Instance.PlaySound(clip, options);
        }
        else
        {
            Debug.LogWarning("AudioAndHapticsManager: MMSoundManager.Instance is null. Cannot play SFX via Feel.");
        }
    }
}