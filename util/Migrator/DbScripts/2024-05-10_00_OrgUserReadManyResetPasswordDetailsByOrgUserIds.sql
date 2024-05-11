CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_ReadManyResetPasswordDetailsByOrganizationUserIds]
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
        OU.[ResetPasswordKey],
        O.[PrivateKey]
    FROM @OrganizationUserIds AS OUIDs
    INNER JOIN [dbo].[OrganizationUser] AS OU
        ON OUIDs.[Id] = OU.[Id]
    INNER JOIN [dbo].[Organization] AS O
        ON OU.[OrganizationId] = O.[Id]
    INNER JOIN [dbo].[User] U
        ON U.[Id] = OU.[UserId]
    WHERE OU.[OrganizationId] = @OrganizationId
END
