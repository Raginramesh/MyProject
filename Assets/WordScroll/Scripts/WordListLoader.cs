using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class WordListLoader : MonoBehaviour
{
    public HashSet<string> validWords = new HashSet<string>();
    public string wordListFileName = "wordlist.txt";

    public void LoadWordList()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, wordListFileName); // Best for build compatibility

        if (File.Exists(filePath))
        {
            string[] words = File.ReadAllLines(filePath);
            foreach (string word in words)
            {
                validWords.Add(word.ToUpper()); // Convert to uppercase for case-insensitive checking
            }
            Debug.Log($"Loaded {validWords.Count} words.");
        }
        else
        {
            Debug.LogError($"Word list file not found at: {filePath}");
        }
    }

    // Example of how to use it:
    public bool IsValidWord(string wordToCheck)
    {
        return validWords.Contains(wordToCheck.ToUpper());
    }

    private void Awake()
    {
        LoadWordList(); // Load the word list when the game starts
    }
}