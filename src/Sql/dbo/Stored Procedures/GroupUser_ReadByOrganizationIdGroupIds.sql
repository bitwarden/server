CREATE PROCEDURE [dbo].[GroupUser_ReadByOrganizationIdGroupIds]
    @OrganizationId UNIQUEIDENTIFIER,
    @GroupIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT GU.*
    FROM [dbo].[GroupUser] GU
    INNER JOIN @GroupIds IDs ON IDs.[Id] = GU.[GroupId]
    INNER JOIN [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    INNER JOIN [dbo].[OrganizationUser] OU ON OU.[Id] = GU.[OrganizationUserId]
    WHERE G.[OrganizationId] = @OrganizationId
        AND OU.[OrganizationId] = @OrganizationId
END
