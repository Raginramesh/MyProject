using UnityEngine;
using Lofelt.NiceVibrations;

public class HapticManager : MonoBehaviour
{
    public static HapticManager Instance { get; private set; }
    
    [Header("Haptic Settings")]
    [SerializeField] private bool hapticsEnabled = true;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Initialize haptics (automatic in Nice Vibrations)
            Debug.Log("HapticManager initialized");
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public void PlayHaptic(HapticPatterns.PresetType hapticType)
    {
        if (hapticsEnabled)
        {
            HapticPatterns.PlayPreset(hapticType);
        }
    }
    
    public void SetHapticsEnabled(bool enabled)
    {
        hapticsEnabled = enabled;
        HapticController.hapticsEnabled = enabled;
    }
    
    public bool AreHapticsEnabled()
    {
        return hapticsEnabled;
    }
}
