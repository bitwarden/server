CREATE OR ALTER PROCEDURE [dbo].[Send_UpdateDisabledByIds]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @Disabled BIT
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserIds [dbo].[GuidIdarray]

    -- Set field
    UPDATE
        [dbo].[Send]
    SET
        [Disabled] = @Disabled,
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Id] IN (SELECT * FROM @Ids)
    
    INSERT INTO @UserIds
    SELECT DISTINCT
        UserId
    FROM
        [dbo].[Send]
    WHERE
        [Id] IN (SELECT * FROM @Ids)
        AND [UserId] IS NOT NULL

    -- Bump account revision dates
    EXEC [dbo].[User_BumpManyAccountRevisionDates] @UserIds
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Send_ReadByIds]
    @Ids AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SendView]
    WHERE
        [Id] IN (SELECT * FROM @Ids)
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Send_ReadIdsByOrgId]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Get the IDs of all users in an org --
    DECLARE @OrgUserIds AS [GuidIdArray];
    INSERT INTO @OrgUserIds
    SELECT DISTINCT
        UserId
    FROM
        [dbo].[OrganizationUserView]
    WHERE
        OrganizationId = @Id

    -- Get the IDs of all Sends associated with those users --
    SELECT
        Id
    FROM
        [dbo].[SendView]
    WHERE
        UserId IN (SELECT [Id] FROM @OrgUserIds)
END
GO