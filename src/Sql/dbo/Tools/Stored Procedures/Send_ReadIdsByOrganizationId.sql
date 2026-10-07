CREATE PROCEDURE [dbo].[Send_ReadIdsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Get the IDs of all active users in an org --
    DECLARE @OrgUserIds AS [GuidIdArray];
    INSERT INTO @OrgUserIds
    SELECT DISTINCT
        [UserId]
    FROM
        [dbo].[OrganizationUserView]
    WHERE
        [OrganizationId] = @OrganizationId
        AND [UserId] IS NOT NULL
        AND [Status] IN (1, 2) -- 1 = Accepted, 2 = Confirmed

    -- Get the IDs of all Sends associated with those users --
    SELECT
        [Id]
    FROM
        [dbo].[SendView]
    WHERE
        [UserId] IN (SELECT [Id] FROM @OrgUserIds)
END