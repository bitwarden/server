CREATE PROCEDURE [dbo].[OrganizationUser_SetStatusToAccepted]
    @OrganizationUserIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UtcNow DATETIME2(7) = GETUTCDATE()

    UPDATE OU
    SET OU.[Status] = 1, -- Accepted
        OU.[Key] = NULL,
        OU.[RevisionDate] = @UtcNow
    FROM [dbo].[OrganizationUser] OU
    INNER JOIN @OrganizationUserIds OUI ON OUI.[Id] = OU.[Id]

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserIds] @OrganizationUserIds
END
GO
