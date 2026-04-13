-- Create OrganizationUser_RevokeMany
CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_RevokeMany]
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
GO

-- Create OrganizationUser_RestoreMany
CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_RestoreMany]
    @OrganizationUserIds AS [dbo].[GuidIdArray] READONLY,
    @Status SMALLINT
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        OU
    SET
        OU.[Status] = @Status,
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

-- Drop legacy sprocs replaced by the bulk versions above
DROP PROCEDURE IF EXISTS [dbo].[OrganizationUser_Deactivate];
GO

DROP PROCEDURE IF EXISTS [dbo].[OrganizationUser_Activate];
GO

DROP PROCEDURE IF EXISTS [dbo].[OrganizationUser_SetStatusForUsersByGuidIdArray];
GO
