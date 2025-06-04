namespace Bit.Core.Platform.Services;

#nullable enable

/// <summary>
/// IMail describes a view model for emails. Any propery in the view model are available for usage
/// in the email templates.
///
/// Each Mail consists of two body parts: a text part and an HTML part and the filename must be
/// relative to the viewmodel and match the following pattern:
/// - `{ClassName}.html.hbs` for the HTML part
/// - `{ClassName}.txt.hbs` for the text part
/// </summary>
public abstract class BaseMailModel2
{
    /// <summary>
    /// The subject of the email.
    /// </summary>
    public abstract string Subject { get; set; }

    /// <summary>
    /// An optional category for processing at the upstream email delivery service.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Current year.
    /// </summary>
    public string CurrentYear => DateTime.UtcNow.Year.ToString();
}
