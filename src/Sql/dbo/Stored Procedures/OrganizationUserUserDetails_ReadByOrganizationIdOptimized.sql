CREATE PROCEDURE [dbo].[OrganizationUserUserDetails_ReadByOrganizationIdOptimized]
    @OrganizationId UNIQUEIDENTIFIER,
    @IncludeGroups BIT,
    @IncludeCollections BIT
AS
BEGIN
    SET NOCOUNT ON

    -- First result set: User details
    SELECT * FROM [dbo].[OrganizationUserUserDetailsView]
    WHERE [OrganizationId] = @OrganizationId

    -- Second result set: Group associations (if requested)
    IF @IncludeGroups = 1
    BEGIN
        SELECT gu.*
        FROM [dbo].[GroupUser] gu
        INNER JOIN [dbo].[OrganizationUser] ou ON gu.OrganizationUserId = ou.Id
        WHERE ou.OrganizationId = @OrganizationId
    END

    -- Third result set: Collection associations (if requested)  
    IF @IncludeCollections = 1
    BEGIN
        SELECT cu.*
        FROM [dbo].[CollectionUser] cu
        INNER JOIN [dbo].[OrganizationUser] ou ON cu.OrganizationUserId = ou.Id
        WHERE ou.OrganizationId = @OrganizationId
    END
END
