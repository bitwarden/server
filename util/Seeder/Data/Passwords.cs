using Bit.Seeder.Data.Enums;

namespace Bit.Seeder.Data;

/// <summary>
/// Password collections by zxcvbn strength level (0-4) for realistic test data.
/// </summary>
internal static class Passwords
{
    /// <summary>
    /// Score 0 - Too guessable: keyboard walks, simple sequences, single words.
    /// </summary>
    public static readonly string[] VeryWeak =
    [
        "password",
        "123456",
        "qwerty",
        "abc123",
        "letmein",
        "admin",
        "welcome",
        "monkey",
        "dragon",
        "master",
        "111111",
        "baseball",
        "iloveyou",
        "trustno1",
        "sunshine",
        "princess",
        "football",
        "shadow",
        "superman",
        "michael",
        "password1",
        "123456789",
        "12345678",
        "1234567",
        "12345",
        "qwerty123",
        "1q2w3e4r",
        "123123",
        "000000",
        "654321"
    ];

    /// <summary>
    /// Score 1 - Very guessable: common patterns with minor complexity.
    /// </summary>
    public static readonly string[] Weak =
    [
        "Password1",
        "Qwerty123",
        "Welcome1",
        "Admin123",
        "Letmein1",
        "Dragon123",
        "Master123",
        "Shadow123",
        "Michael1",
        "Jennifer1",
        "abc123!",
        "pass123!",
        "test1234",
        "hello123",
        "love1234",
        "money123",
        "secret1",
        "access1",
        "login123",
        "super123",
        "changeme",
        "temp1234",
        "guest123",
        "user1234",
        "pass1234",
        "default1",
        "sample12",
        "demo1234",
        "trial123",
        "secure1"
    ];

    /// <summary>
    /// Score 2 - Somewhat guessable: meets basic complexity but predictable patterns.
    /// </summary>
    public static readonly string[] Fair =
    [
        "Summer2024!",
        "Winter2023#",
        "Spring2024@",
        "Autumn2023$",
        "January2024!",
        "Welcome123!",
        "Company2024#",
        "Secure123!",
        "Access2024@",
        "Login2024!",
        "Michael123!",
        "Jennifer2024@",
        "Robert456#",
        "Sarah789!",
        "David2024!",
        "Password123!",
        "Security2024@",
        "Admin2024!",
        "User2024#",
        "Guest123!",
        "Football123!",
        "Baseball2024@",
        "Soccer456#",
        "Hockey789!",
        "Tennis2024!",
        "NewYork2024!",
        "Chicago123@",
        "Boston2024#",
        "Seattle789!",
        "Denver2024$"
    ];

    /// <summary>
    /// Score 3 - Safely unguessable: good entropy, mixed character types.
    /// </summary>
    public static readonly string[] Strong =
    [
        "k#9Lm$vQ2@xR7nP!",
        "Yx8&mK3$pL5#wQ9@",
        "Nv4%jH7!bT2@sF6#",
        "Rm9#cX5$gW1@zK8!",
        "Qp3@hY6#nL9$tB2!",
        "Wz7!mF4@kS8#xC1$",
        "Jd2#pR9!vN5@bG7$",
        "Ht6@wL3#yK8!mQ4$",
        "Bf8$cM2@zT5#rX9!",
        "Lg1!nV7@sH4#pY6$",
        "Xk5#tW8@jR2$mN9!",
        "Cv3@yB6#pF1$qL4!",
        "correct-horse-battery",
        "purple-monkey-dishwasher",
        "quantum-bicycle-elephant",
        "velvet-thunder-crystal",
        "neon-wizard-cosmic",
        "amber-phoenix-digital",
        "Brave.Tiger.Runs.42",
        "Blue.Ocean.Deep.17",
        "Swift.Eagle.Soars.93",
        "maple#stream#winter",
        "ember@cloud@silent",
        "frost$dawn$valley"
    ];

    /// <summary>
    /// Score 4 - Very unguessable: high entropy, long passphrases, random strings.
    /// </summary>
    public static readonly string[] VeryStrong =
    [
        "Kx9#mL4$pQ7@wR2!vN5hT8",
        "Yz3@hT8#bF1$cS6!nM9wK4",
        "Wv5!rK2@jG9#tX4$mL7nB3",
        "Qn7$sB3@yH6#pC1!zF8kW2",
        "Tm2@xD5#kW9$vL4!rJ7gN1",
        "Pf4!nC8@bR3#yL6$hS9mV2",
        "correct-horse-battery-staple",
        "purple-monkey-dishwasher-lamp",
        "quantum-bicycle-elephant-storm",
        "velvet-thunder-crystal-forge",
        "neon-wizard-cosmic-river",
        "amber-phoenix-digital-maze",
        "silver-falcon-ancient-code",
        "lunar-garden-frozen-spark",
        "echo-prism-wandering-light",
        "Brave.Tiger.Runs.Fast.42!",
        "Blue.Ocean.Deep.Wave.17@",
        "Swift.Eagle.Soars.High.93#",
        "maple#stream#winter#glow#dawn",
        "ember@cloud@silent@peak@mist",
        "frost$dawn$valley$mist$glow",
        "7hK$mN2@pL9#xR4!wQ8vB5&jF",
        "3yT@nC7#bS1$kW6!mH9rL2%xD",
        "9pF!vK4@jR8#tN3$yB7mL1&wS"
    ];

    /// <summary>All passwords combined for mixed/random selection.</summary>
    public static readonly string[] All = [.. VeryWeak, .. Weak, .. Fair, .. Strong, .. VeryStrong];

    /// <summary>
    /// Realistic distribution based on breach data and security research.
    /// Sources: NordPass annual reports, Have I Been Pwned analysis, academic studies.
    /// Distribution: 25% VeryWeak, 30% Weak, 25% Fair, 15% Strong, 5% VeryStrong
    /// </summary>
    private static readonly (PasswordStrength Strength, int CumulativePercent)[] RealisticDistribution =
    [
        (PasswordStrength.VeryWeak, 25),    // 25% - most common breached passwords
        (PasswordStrength.Weak, 55),         // 30% - simple patterns with numbers
        (PasswordStrength.Fair, 80),         // 25% - meets basic requirements
        (PasswordStrength.Strong, 95),       // 15% - good passwords
        (PasswordStrength.VeryStrong, 100)   // 5%  - password manager users
    ];

    public static string[] GetByStrength(PasswordStrength strength) => strength switch
    {
        PasswordStrength.VeryWeak => VeryWeak,
        PasswordStrength.Weak => Weak,
        PasswordStrength.Fair => Fair,
        PasswordStrength.Strong => Strong,
        PasswordStrength.VeryStrong => VeryStrong,
        PasswordStrength.Realistic => All, // For direct array access, use All
        _ => Strong
    };

    /// <summary>
    /// Gets a password with realistic strength distribution.
    /// Uses deterministic selection based on index for reproducible test data.
    /// </summary>
    public static string GetRealisticPassword(int index)
    {
        var strength = GetRealisticStrength(index);
        var passwords = GetByStrength(strength);
        return passwords[index % passwords.Length];
    }

    /// <summary>
    /// Gets a password strength following realistic distribution.
    /// Deterministic based on index for reproducible results.
    /// </summary>
    public static PasswordStrength GetRealisticStrength(int index)
    {
        // Use modulo 100 for percentage-based bucket selection
        var bucket = index % 100;

        foreach (var (strength, cumulativePercent) in RealisticDistribution)
        {
            if (bucket < cumulativePercent)
            {
                return strength;
            }
        }

        return PasswordStrength.Strong; // Fallback
    }

    public static string GetPassword(PasswordStrength strength, int index)
    {
        if (strength == PasswordStrength.Realistic)
        {
            return GetRealisticPassword(index);
        }

        var passwords = GetByStrength(strength);
        return passwords[index % passwords.Length];
    }
}
