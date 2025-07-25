CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CollectionView]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadSharedCollectionsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CollectionView]
    WHERE
        [OrganizationId] = @OrganizationId AND
        [Type] = 0 -- DefaultUserCollection only
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUserUserDetails_ReadByOrganizationId_V2]
    @OrganizationId UNIQUEIDENTIFIER,
    @IncludeGroups BIT = 0,
    @IncludeCollections BIT = 0
AS
BEGIN
    SET NOCOUNT ON

    -- Result Set 1: User Details (always returned)
    SELECT * 
    FROM [dbo].[OrganizationUserUserDetailsView] 
    WHERE OrganizationId = @OrganizationId

    -- Result Set 2: Group associations (if requested)
    IF @IncludeGroups = 1
    BEGIN
        SELECT gu.*
        FROM [dbo].[GroupUser] gu
        INNER JOIN [dbo].[OrganizationUser] ou ON gu.OrganizationUserId = ou.Id
        WHERE ou.OrganizationId = @OrganizationId
    END

    -- Result Set 3: Collection associations (if requested)  
    IF @IncludeCollections = 1
    BEGIN
        SELECT cu.*
        FROM [dbo].[CollectionUser] cu
        INNER JOIN [dbo].[OrganizationUser] ou ON cu.OrganizationUserId = ou.Id
        INNER JOIN [dbo].[Collection] c ON cu.CollectionId = c.Id
        WHERE ou.OrganizationId = @OrganizationId 
            AND c.Type = 0 -- SharedCollections only
    END
END
GO
