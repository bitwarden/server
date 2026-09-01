#nullable enable

namespace Bit.Core.Tools.Models.Data;

/// <summary>
/// A discriminated union for send authentication.
/// </summary>
/// <example>
/// const method : SendAuthenticationMethod;
/// // other variable definitions omitted
///
/// var token = method switch
/// {
///     NotAuthenticated => issueTokenFor(sendId),
///     ResourcePassword(var expected) => tryIssueTokenFor(sendId, expected, actual),
///     EmailOtp(_) => tryIssueTokenFor(sendId, email, actualOtp),
///     _ => throw new Exception()
/// };
/// </example>
public abstract record SendAuthenticationMethod;

/// <summary>
/// Create a send claim automatically.
/// </summary>
public record NotAuthenticated : SendAuthenticationMethod;

/// <summary>
/// Create a send claim by requesting a password confirmation hash.
/// </summary>
/// <param name="Hash">
/// A base64 encoded hash that permits access to the send.
/// </param>
public record ResourcePassword(string Hash) : SendAuthenticationMethod;

/// <summary>
/// Create a send claim by requesting a one time password (OTP) confirmation code.
/// </summary>
/// <param name="emails">
/// The list of email addresses permitted access to the send.
/// </param>
public record EmailOtp(string[] emails) : SendAuthenticationMethod;

/// <summary>
/// The send cannot be accessed: it exists but is inaccessible (expired, disabled, max access exceeded,
/// or past deletion date), or no send matches the given id.
/// </summary>
public record SendInaccessible : SendAuthenticationMethod;
