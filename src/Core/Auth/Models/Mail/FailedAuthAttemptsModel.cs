﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Models.Mail;

namespace Bit.Core.Auth.Models.Mail;

public class FailedAuthAttemptsModel : NewDeviceLoggedInModel
{
    public string AffectedEmail { get; set; }
}
