namespace Bit.Api.Tools.Utilities;

using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;

public class SendUtilities
{
    public static AuthType InferAuthType(Send send)
    {
        if (!string.IsNullOrWhiteSpace(send.Password))
        {
            return AuthType.Password;
        }

        if (!string.IsNullOrWhiteSpace(send.Emails))
        {
            return AuthType.Email;
        }

        return AuthType.None;
    }
}

