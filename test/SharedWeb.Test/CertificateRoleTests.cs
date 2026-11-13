using Bit.Core.Enums;
using Bit.SharedWeb.Utilities;

namespace Bit.SharedWeb.Test;

public class CertificateRoleTests
{
    [Theory]
    [InlineData(CertificateRole.IdentitySigning, "Identity signing")]
    [InlineData(CertificateRole.SsoSigning, "SSO signing")]
    [InlineData(CertificateRole.SsoEncryption, "SSO encryption")]
    public void GetDisplayName_ReturnsTheRoleText(CertificateRole role, string expected)
    {
        Assert.Equal(expected, role.GetDisplayName());
    }
}
