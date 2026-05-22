CREATE PROCEDURE [dbo].[OrganizationUser_RevokeMany]
    @OrganizationUserIds AS [dbo].[GuidIdArray] READONLY,
    @RevocationReason TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        OU
    SET
        OU.[StatusNew] = OU.[Status], -- Snapshot the pre-update Status before overwriting
        OU.[Status] = -1, -- Revoked
        OU.[RevocationReason] = @RevocationReason
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        @OrganizationUserIds OUI ON OUI.[Id] = OU.[Id]
    WHERE
        OU.[Status] != -1 -- Skip already-revoked rows so existing StatusNew / RevocationReason are preserved

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserIds] @OrganizationUserIds
END
