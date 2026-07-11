-- The generic Repository<T, TId>.GetByIdAsync convention calls [dbo].[{Table}_ReadById] -- [dbo].[ApiKey] never had
-- one (only ApiKey_ReadByServiceAccountId and the ServiceAccount-joined ApiKeyDetails_ReadById), because every prior
-- caller looked ApiKey up by ServiceAccountId or ApiKeyDetails. PAM's rotation-daemon credential is a bare ApiKey row
-- (ServiceAccountId NULL, owner link inverted via PamDaemon.ApiKeyId -- see Bit.Pam.Entities.PamDaemon), and
-- PamDaemonClientProvider (src/Identity/IdentityServer/ClientProviders/PamDaemonClientProvider.cs) resolves the
-- daemon's OAuth client via IApiKeyRepository.GetByIdAsync(apiKeyId), so this sproc is required for daemon token
-- issuance to work at all.

IF OBJECT_ID('[dbo].[ApiKey_ReadById]') IS NULL
BEGIN
    EXECUTE ('
        CREATE PROCEDURE [dbo].[ApiKey_ReadById]
            @Id UNIQUEIDENTIFIER
        AS
        BEGIN
            SET NOCOUNT ON

            SELECT
                *
            FROM
                [dbo].[ApiKeyView]
            WHERE
                [Id] = @Id
        END
    ')
END
GO
