// Simple static class to hold player data for UI demonstration.
// Replace with a real save/load system later!
public static class PlayerData
{
    public static int Coins { get; set; } = 100; // Initial value
    public static int Hearts { get; set; } = 5;   // Initial value

    // Add methods to modify these if needed, e.g.:
    public static void AddCoins(int amount)
    {
        Coins += amount;
        // Add clamping or validation if necessary
    }

    public static bool SpendHearts(int amount)
    {
        if (Hearts >= amount)
        {
            Hearts -= amount;
            return true;
        }
        return false;
    }
}