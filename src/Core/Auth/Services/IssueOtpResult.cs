namespace Bit.Core.Auth.Services;

/// <summary>
/// The outcome of a request to issue an OTP.
/// </summary>
public abstract record IssueOtpResult
{
    private IssueOtpResult() { }

    /// <summary>
    /// An OTP was generated and replaces any OTP already pending.
    /// </summary>
    /// <param name="Otp">The generated OTP</param>
    public sealed record Issued(string Otp) : IssueOtpResult;

    /// <summary>
    /// No OTP was generated, because the user has been issued the maximum number of OTPs allowed in the current
    /// request window.
    /// </summary>
    public sealed record RequestLimitReached : IssueOtpResult;
}
