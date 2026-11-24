-- Return whether each user is the sole owner of an organization other than the recovering one
CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_ReadManyAccountRecoveryDetailsByOrganizationUserIds]
    @OrganizationId UNIQUEIDENTIFIER,
    @OrganizationUserIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        OU.[Id] AS OrganizationUserId,
        U.[Kdf],
        U.[KdfIterations],
        U.[KdfMemory],
        U.[KdfParallelism],
        U.[MasterPasswordSalt],
        OU.[ResetPasswordKey],
        O.[PrivateKey] AS EncryptedPrivateKey,
        CAST(CASE WHEN EXISTS (
            SELECT 1
            FROM [dbo].[OrganizationUser] OtherOU
            WHERE OtherOU.[UserId] = OU.[UserId]
                AND OtherOU.[OrganizationId] <> @OrganizationId
                AND NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[OrganizationUser] OwnerOU
                    WHERE OwnerOU.[OrganizationId] = OtherOU.[OrganizationId]
                        AND OwnerOU.[Id] <> OtherOU.[Id]
                        AND OwnerOU.[Type] = 0 -- Owner
                        AND OwnerOU.[Status] = 2 -- Confirmed
                )
                AND NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[ProviderOrganization] PO
                    INNER JOIN [dbo].[ProviderUser] PU ON PU.[ProviderId] = PO.[ProviderId]
                    WHERE PO.[OrganizationId] = OtherOU.[OrganizationId]
                        AND PU.[Status] = 2 -- Confirmed
                )
        ) THEN 1 ELSE 0 END AS BIT) AS IsSoleOwnerOfOtherOrganization
    FROM
        @OrganizationUserIds AS OUIDs
    INNER JOIN
        [dbo].[OrganizationUser] AS OU ON OUIDs.[Id] = OU.[Id]
    INNER JOIN
        [dbo].[Organization] AS O ON OU.[OrganizationId] = O.[Id]
    INNER JOIN
        [dbo].[User] U ON U.[Id] = OU.[UserId]
    WHERE
        OU.[OrganizationId] = @OrganizationId
END
GO
