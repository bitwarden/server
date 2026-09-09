CREATE OR ALTER PROCEDURE [dbo].[Send_ReadIdsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Get the IDs of all users in an org --
    DECLARE @OrgUserIds AS [GuidIdArray];
    INSERT INTO @OrgUserIds
    SELECT DISTINCT
        [UserId]
    FROM
        [dbo].[OrganizationUserView]
    WHERE
        [OrganizationId] = @OrganizationId
        AND [UserId] IS NOT NULL

    -- Get the IDs of all Sends associated with those users --
    SELECT
        [Id]
    FROM
        [dbo].[SendView]
    WHERE
        [UserId] IN (SELECT [Id] FROM @OrgUserIds)
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Send_UpdateDisabledByIds]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @Disabled BIT,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserIds [dbo].[GuidIdArray]

    -- Set field
    UPDATE
        [dbo].[Send]
    SET
        [Disabled] = @Disabled,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] IN (SELECT * FROM @Ids)
    
    INSERT INTO @UserIds
    SELECT DISTINCT
        [UserId]
    FROM
        [dbo].[Send]
    WHERE
        [Id] IN (SELECT * FROM @Ids)
        AND [UserId] IS NOT NULL

    -- Bump account revision dates
    EXEC [dbo].[User_BumpManyAccountRevisionDates] @UserIds
END
GO