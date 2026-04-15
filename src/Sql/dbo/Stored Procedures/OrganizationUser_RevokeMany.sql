CREATE PROCEDURE [dbo].[OrganizationUser_RevokeMany]
    @OrganizationUserIds AS [dbo].[GuidIdArray] READONLY,
    @RevocationReason TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        OU
    SET
        OU.[Status] = -1, -- Revoked
        OU.[RevocationReason] = @RevocationReason
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        @OrganizationUserIds OUI ON OUI.[Id] = OU.[Id]

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserIds] @OrganizationUserIds
END
