using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var apiKey = app.Configuration["PasswordAnalyzer:ApiKey"];

app.MapPost("/analyze", (PasswordRequest request, HttpContext ctx) =>
{
    // Check API key
    if (!CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(ctx.Request.Headers["X-API-Key"].ToString()),
            System.Text.Encoding.UTF8.GetBytes(apiKey ?? string.Empty)))
        return Results.Unauthorized();
        return Results.Unauthorized();


    var score = 0;
    var feedback = new List<string>();

    // Length check
    if (request.Password.Length >= 8) score += 20;
    if (request.Password.Length >= 12) score += 10;
    if (request.Password.Length >= 16) score += 10;

    // Uppercase
    for (int i = 0; i < request.Password.Length; i++)
    {
        if (char.IsUpper(request.Password[i]))
        {
            score += 15;
            break;
        }
    }

    // Lowercase
    for (int i = 0; i < request.Password.Length; i++)
    {
        if (char.IsLower(request.Password[i]))
        {
            score += 15;
            break;
        }
    }

    // Numbers
    for (int i = 0; i < request.Password.Length; i++)
    {
        if (char.IsDigit(request.Password[i]))
        {
            score += 15;
            break;
        }
    }

    // Special chars
    if (Regex.IsMatch(request.Password, @"[!@#$%^&*]"))
        score += 15;

    // Common password check
    var common = new string[] { "password", "123456", "qwerty", "admin" };
    for (int i = 0; i < common.Length; i++)
    {
        if (request.Password.ToLower() == common[i])
        {
            score = 0;
            feedback.Add("Common password detected");
        }
    }

    // Determine strength
    string strength;
    if (score < 40)
        strength = "Weak";
    else if (score < 70)
        strength = "Medium";
    else
        strength = "Strong";

    return Results.Ok(new
    {
        score,
        strength,
        feedback,
        analyzedAt = DateTime.Now,
        passwordLength = request.Password.Length
    });
});

app.MapGet("/health", () => "OK");

app.Run();

record PasswordRequest(string Password);
