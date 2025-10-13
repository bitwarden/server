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
/// Never issue a send claim.
/// </summary>
/// <remarks>
/// This claim is issued when a send does not exist or when a send
/// has exceeded its max access attempts.
/// </remarks>
public record NeverAuthenticate : SendAuthenticationMethod;

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
/// <param name="Emails">
/// The list of email addresses permitted access to the send.
/// </param>
public record EmailOtp(string[] Emails) : SendAuthenticationMethod;
