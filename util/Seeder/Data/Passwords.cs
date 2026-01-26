using Bit.Seeder.Data.Enums;

namespace Bit.Seeder.Data;

/// <summary>
/// Password collections by strength level for realistic test data.
/// </summary>
internal static class Passwords
{
    /// <summary>
    /// Top breached passwords - use for security testing scenarios.
    /// </summary>
    public static readonly string[] Weak =
    [
        "password", "123456", "qwerty", "abc123", "letmein", "welcome", "admin", "dragon", "sunshine", "princess",
        "football", "master", "shadow", "superman", "trustno1", "iloveyou", "passw0rd", "p@ssw0rd", "welcome1", "Password1",
        "qwerty123", "123qwe", "1q2w3e", "password123", "12345678", "111111", "1234567890", "monkey", "baseball", "access"
    ];

    /// <summary>
    /// Meets basic complexity requirements but follows predictable patterns (season+year, name+numbers).
    /// </summary>
    public static readonly string[] Medium =
    [
        "Summer2024!", "Winter2023#", "Spring2024@", "Autumn2023$", "January2024!", "December2023#",
        "Welcome123!", "Company2024#", "Secure123!", "Access2024@", "Login123!", "Portal2024#",
        "Michael123!", "Jennifer2024@", "Robert456#", "Sarah789!",
        "Qwerty123!", "Asdfgh456@", "Zxcvbn789#",
        "Password123!", "Security2024@", "Admin123!", "User2024#", "Guest123!", "Test2024@",
        "Football123!", "Baseball2024@", "Soccer456#", "Hockey789!"
    ];

    /// <summary>
    /// High-entropy passwords: random strings (password manager style) and diceware passphrases.
    /// </summary>
    public static readonly string[] Strong =
    [
        "k#9Lm$vQ2@xR7nP!", "Yx8&mK3$pL5#wQ9@", "Nv4%jH7!bT2@sF6#", "Rm9#cX5$gW1@zK8!", "Qp3@hY6#nL9$tB2!",
        "Wz7!mF4@kS8#xC1$", "Jd2#pR9!vN5@bG7$", "Ht6@wL3#yK8!mQ4$", "Bf8$cM2@zT5#rX9!", "Lg1!nV7@sH4#pY6$",
        "Kx9#mL4$pQ7@wR2!vN5", "Yz3@hT8#bF1$cS6!nM9", "Wv5!rK2@jG9#tX4$mL7", "Qn7$sB3@yH6#pC1!zF8", "Tm2@xD5#kW9$vL4!rJ7",
        "correct-horse-battery-staple", "purple-monkey-dishwasher-lamp", "quantum-bicycle-elephant-storm",
        "velvet-thunder-crystal-forge", "neon-wizard-cosmic-river", "amber-phoenix-digital-maze",
        "silver-falcon-ancient-code", "lunar-garden-frozen-spark", "echo-prism-wandering-light", "rust-vapor-hidden-gate",
        "Brave.Tiger.Runs.Fast.42", "Blue.Ocean.Deep.Wave.17", "Swift.Eagle.Soars.High.93",
        "Calm.Forest.Green.Path.28", "Warm.Summer.Golden.Sun.61",
        "maple#stream#winter#glow", "ember@cloud@silent@peak", "frost$dawn$valley$mist", "coral!reef!azure!tide", "stone&moss&ancient&oak",
        "Kx9mL4pQ7wR2vN5hT8bF", "Yz3hT8bF1cS6nM9wK4pL", "Wv5rK2jG9tX4mL7nB3sH", "Qn7sB3yH6pC1zF8kW2xD", "Tm2xD5kW9vL4rJ7gN1cY"
    ];

    /// <remarks>Must be declared after strength arrays (S3263).</remarks>
    public static readonly string[] All = [.. Weak, .. Medium, .. Strong];

    public static string[] GetByStrength(PasswordStrength strength) => strength switch
    {
        PasswordStrength.Weak => Weak,
        PasswordStrength.Medium => Medium,
        PasswordStrength.Strong => Strong,
        PasswordStrength.Mixed => All,
        _ => Strong
    };

    public static string GetPassword(PasswordStrength strength, int index)
    {
        var passwords = GetByStrength(strength);
        return passwords[index % passwords.Length];
    }
}
