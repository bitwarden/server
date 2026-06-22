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