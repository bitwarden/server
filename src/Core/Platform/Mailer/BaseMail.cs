namespace Bit.Core.Platform.Mailer;

#nullable enable

/// <summary>
/// BaseMail describes a model for emails. It contains metadata about the email such as recipients,
/// subject, and an optional category for processing at the upstream email delivery service.
///
/// Each BaseMail must have a view model that inherits from BaseMailView. The view model is used to
/// generate the text part and HTML body.
/// </summary>
public abstract class BaseMail<TView> where TView : BaseMailView
{
    /// <summary>
    /// Email recipients.
    /// </summary>
    public required IEnumerable<string> ToEmails { get; set; }

    /// <summary>
    /// The subject of the email.
    /// </summary>
    public abstract string Subject { get; }

    /// <summary>
    /// An optional category for processing at the upstream email delivery service.
    /// </summary>
    public string? Category { get; }

    /// <summary>
    /// Allows you to override ignore the suppression list for this email.
    ///
    /// Warning: This should be used with caution, valid reasons are primarily account recovery, email OTP.
    /// </summary>
    public virtual bool IgnoreSuppressList { get; } = false;

    /// <summary>
    /// View model for the email body.
    /// </summary>
    public required TView View { get; set; }
}

/// <summary>
/// Each MailView consists of two body parts: a text part and an HTML part and the filename must be
/// relative to the viewmodel and match the following pattern:
/// - `{ClassName}.html.hbs` for the HTML part
/// - `{ClassName}.txt.hbs` for the text part
/// </summary>
public abstract class BaseMailView
{
    /// <summary>
    /// Current year.
    /// </summary>
    public string CurrentYear => DateTime.UtcNow.Year.ToString();
}
