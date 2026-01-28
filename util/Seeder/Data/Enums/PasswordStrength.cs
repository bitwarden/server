namespace Bit.Seeder.Data.Enums;

/// <summary>
/// Password strength levels aligned with zxcvbn scoring (0-4).
/// </summary>
public enum PasswordStrength
{
    /// <summary>Score 0: Too guessable (&lt; 10³ guesses)</summary>
    VeryWeak = 0,

    /// <summary>Score 1: Very guessable (&lt; 10⁶ guesses)</summary>
    Weak = 1,

    /// <summary>Score 2: Somewhat guessable (&lt; 10⁸ guesses)</summary>
    Fair = 2,

    /// <summary>Score 3: Safely unguessable (&lt; 10¹⁰ guesses)</summary>
    Strong = 3,

    /// <summary>Score 4: Very unguessable (≥ 10¹⁰ guesses)</summary>
    VeryStrong = 4,

    /// <summary>Realistic distribution based on breach data statistics.</summary>
    Realistic = 99
}
