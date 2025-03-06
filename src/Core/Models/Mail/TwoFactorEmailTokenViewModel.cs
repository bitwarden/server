﻿namespace Bit.Core.Models.Mail;

/// <summary>
/// This view model is used to set-up email two factor authentication, to log in with email two factor authentication,
/// and for new device verification.
/// </summary>
public class TwoFactorEmailTokenViewModel : BaseMailModel
{
    public string Token { get; set; }
    /// <summary>
    /// This view model is used to also set-up email two factor authentication. We use this property to communicate
    /// the purpose of the email, since it can be used for logging in and for setting up.
    /// </summary>
    public string EmailTotpAction { get; set; }
    /// <summary>
    /// When logging in with email two factor the account email may not be the same as the email used for two factor.
    /// we want to show the account email in the email, so the user knows which account they are logging into.
    /// </summary>
    public string AccountEmail { get; set; }
    public string TheDate { get; set; }
    public string TheTime { get; set; }
    public string TimeZone { get; set; }
    public string DeviceIp { get; set; }
    public string DeviceType { get; set; }
}
