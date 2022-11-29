CREATE PROCEDURE [dbo].[CollectionUser_ReadByOrganizationId]
	@OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    
    SELECT
        CU.*
    FROM
        [dbo].[CollectionUser] CU
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = CU.[OrganizationUserId]
    WHERE
        OU.[OrganizationId] = @OrganizationId
    
END