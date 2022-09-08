using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Bit.Setup;

public static class Helpers
{
    public static string SecureRandomString(int length, bool alpha = true, bool upper = true, bool lower = true,
        bool numeric = true, bool special = false)
    {
        return SecureRandomString(length, RandomStringCharacters(alpha, upper, lower, numeric, special));
    }

    // ref https://stackoverflow.com/a/8996788/1090359 with modifications
    public static string SecureRandomString(int length, string characters)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "length cannot be less than zero.");
        }

        if ((characters?.Length ?? 0) == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(characters), "characters invalid.");
        }

        const int byteSize = 0x100;
        if (byteSize < characters.Length)
        {
            throw new ArgumentException(
                string.Format("{0} may contain no more than {1} characters.", nameof(characters), byteSize),
                nameof(characters));
        }

        var outOfRangeStart = byteSize - (byteSize % characters.Length);
        using (var rng = RandomNumberGenerator.Create())
        {
            var sb = new StringBuilder();
            var buffer = new byte[128];
            while (sb.Length < length)
            {
                rng.GetBytes(buffer);
                for (var i = 0; i < buffer.Length && sb.Length < length; ++i)
                {
                    // Divide the byte into charSet-sized groups. If the random value falls into the last group and the
                    // last group is too small to choose from the entire allowedCharSet, ignore the value in order to
                    // avoid biasing the result.
                    if (outOfRangeStart <= buffer[i])
                    {
                        continue;
                    }

                    sb.Append(characters[buffer[i] % characters.Length]);
                }
            }

            return sb.ToString();
        }
    }

    private static string RandomStringCharacters(bool alpha, bool upper, bool lower, bool numeric, bool special)
    {
        var characters = string.Empty;
        if (alpha)
        {
            if (upper)
            {
                characters += "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            }

            if (lower)
            {
                characters += "abcdefghijklmnopqrstuvwxyz";
            }
        }

        if (numeric)
        {
            characters += "0123456789";
        }

        if (special)
        {
            characters += "!@#$%^*&";
        }

        return characters;
    }

    public static string GetValueFromEnvFile(string envFile, string key)
    {
        if (!File.Exists($"/bitwarden/env/{envFile}.override.env"))
        {
            return null;
        }

        var lines = File.ReadAllLines($"/bitwarden/env/{envFile}.override.env");
        foreach (var line in lines)
        {
            if (line.StartsWith($"{key}="))
            {
                return line.Split(new char[] { '=' }, 2)[1].Trim('"').Replace("\\\"", "\"");
            }
        }

        return null;
    }

    public static string Exec(string cmd, bool returnStdout = false)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var escapedArgs = cmd.Replace("\"", "\\\"");
            process.StartInfo.FileName = "/bin/bash";
            process.StartInfo.Arguments = $"-c \"{escapedArgs}\"";
        }
        else
        {
            process.StartInfo.FileName = "powershell";
            process.StartInfo.Arguments = cmd;
        }

        process.Start();
        var result = returnStdout ? process.StandardOutput.ReadToEnd() : null;
        process.WaitForExit();
        return result;
    }

    public static string ReadInput(string prompt)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("(!) ");
        Console.ResetColor();
        Console.Write(prompt);
        if (prompt.EndsWith("?"))
        {
            Console.Write(" (y/n)");
        }
        Console.Write(": ");
        var input = Console.ReadLine();
        Console.WriteLine();
        return input;
    }

    public static bool ReadQuestion(string prompt)
    {
        var input = ReadInput(prompt).ToLowerInvariant().Trim();
        return input == "y" || input == "yes";
    }

    public static void ShowBanner(Context context, string title, string message, ConsoleColor? color = null)
    {
        if (!context.PrintToScreen())
        {
            return;
        }
        if (color != null)
        {
            Console.ForegroundColor = color.Value;
        }
        Console.WriteLine($"!!!!!!!!!! {title} !!!!!!!!!!");
        Console.WriteLine(message);
        Console.WriteLine();
        Console.ResetColor();
    }

    public static HandlebarsDotNet.HandlebarsTemplate<object, object> ReadTemplate(string templateName)
    {
        var assembly = typeof(Helpers).GetTypeInfo().Assembly;
        var fullTemplateName = $"Bit.Setup.Templates.{templateName}.hbs";
        if (!assembly.GetManifestResourceNames().Any(f => f == fullTemplateName))
        {
            return null;
        }
        using (var s = assembly.GetManifestResourceStream(fullTemplateName))
        using (var sr = new StreamReader(s))
        {
            var templateText = sr.ReadToEnd();
            return HandlebarsDotNet.Handlebars.Compile(templateText);
        }
    }

    public static void WriteLine(Context context, string format = null, object arg0 = null, object arg1 = null,
        object arg2 = null)
    {
        if (!context.PrintToScreen())
        {
            return;
        }
        if (format != null && arg0 != null && arg1 != null && arg2 != null)
        {
            Console.WriteLine(format, arg0, arg1, arg2);
        }
        else if (format != null && arg0 != null && arg1 != null)
        {
            Console.WriteLine(format, arg0, arg1);
        }
        else if (format != null && arg0 != null)
        {
            Console.WriteLine(format, arg0);
        }
        else if (format != null)
        {
            Console.WriteLine(format);
        }
        else
        {
            Console.WriteLine();
        }
    }
}
