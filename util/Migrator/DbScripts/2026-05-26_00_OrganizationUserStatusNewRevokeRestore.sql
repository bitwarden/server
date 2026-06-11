-- Create OrganizationUser_UpdateManyRevoke to snapshot pre-revoke Status into StatusNew
-- and skip already-revoked rows so existing StatusNew / RevocationReason are preserved.
-- Replaces OrganizationUser_RevokeMany (renamed to follow the {Entity}_{Action} convention).
CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_UpdateManyRevoke]
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
GO

-- Create OrganizationUser_UpdateManyRestore to clear StatusNew alongside RevocationReason.
-- Replaces OrganizationUser_RestoreMany (renamed to follow the {Entity}_{Action} convention).
CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_UpdateManyRestore]
    @OrganizationUserIds AS [dbo].[GuidIdArray] READONLY,
    @Status SMALLINT
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        OU
    SET
        OU.[Status] = @Status,
        OU.[StatusNew] = NULL,
        OU.[RevocationReason] = NULL
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        @OrganizationUserIds OUI ON OUI.[Id] = OU.[Id]
    WHERE
        OU.[Status] = -1 -- Only restore currently revoked users

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserIds] @OrganizationUserIds
END
GO
