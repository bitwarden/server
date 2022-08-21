CREATE PROCEDURE [dbo].[Group_ReadWithCollectionsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    SELECT
        G.*,
        [CollectionId] [Id],
        [ReadOnly],
        [HidePasswords]
    FROM
        [dbo].[GroupView] G
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON G.Id = CG.GroupId
    WHERE
        [OrganizationId] = @OrganizationId
END