CREATE PROCEDURE [dbo].[GroupUser_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        GU.*
    FROM
        [dbo].[GroupUser] GU
    INNER JOIN
        [dbo].[Group] G ON G.[Id] = GU.[GroupId]
    WHERE
        G.[OrganizationId] = @OrganizationId
END