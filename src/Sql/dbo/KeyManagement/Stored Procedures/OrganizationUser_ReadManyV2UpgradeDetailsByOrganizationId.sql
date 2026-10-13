CREATE PROCEDURE [dbo].[OrganizationUser_ReadManyV2UpgradeDetailsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @ExcludedType TINYINT,
    @AfterId UNIQUEIDENTIFIER = NULL,
    @MaxCount INT
AS
BEGIN
    SET NOCOUNT ON

    -- A row without a user key id cannot be upgraded. The server has nothing to validate the admin's
    -- re-wrapped key against, so the row is not returned.
    SELECT TOP (@MaxCount)
        OU.[Id] AS [OrganizationUserId],
        U.[UserKeyId],
        OU.[ResetPasswordKey] AS [AccountRecoveryKey],
        OU.[V2UpgradeToken]
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        [dbo].[User] U ON U.[Id] = OU.[UserId]
    WHERE
        OU.[OrganizationId] = @OrganizationId
        AND OU.[V2UpgradeToken] IS NOT NULL
        AND OU.[ResetPasswordKey] IS NOT NULL
        AND U.[UserKeyId] IS NOT NULL
        AND (@ExcludedType IS NULL OR OU.[Type] <> @ExcludedType)
        AND (@AfterId IS NULL OR OU.[Id] > @AfterId)
    ORDER BY
        OU.[Id]
END
