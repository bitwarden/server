CREATE PROCEDURE [dbo].[CollectionGroup_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
        
    SELECT
        CG.*
    FROM
        [dbo].[CollectionGroup] CG
    INNER JOIN
        [dbo].[Group] G ON G.[Id] = CG.[GroupId]
    WHERE
        G.[OrganizationId] = @OrganizationId
END