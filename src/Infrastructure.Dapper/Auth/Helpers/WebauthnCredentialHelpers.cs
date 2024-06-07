using System.Data;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Data;
using Dapper;

namespace Bit.Infrastructure.Dapper.Auth.Helpers;

public static class WebauthnCredentialHelpers
{
    public static DataTable ToTvp(this IEnumerable<WebauthnRotateKeyData> webauthnCredentials)
    {
        var table = new DataTable();
        table.SetTypeName("[dbo].[WebauthnCredentialRotationDataType]");

        var columnData = new List<(string name, Type type, Func<WebauthnRotateKeyData, object> getter)>
        {
            (nameof(WebAuthnCredential.Id), typeof(Guid), wc => wc.Id),
            (nameof(WebAuthnCredential.EncryptedPublicKey), typeof(string), wc => wc.EncryptedPublicKey),
            (nameof(WebAuthnCredential.EncryptedUserKey), typeof(string), wc => wc.EncryptedUserKey),
        };

        return webauthnCredentials.BuildTable(table, columnData);
    }
}
