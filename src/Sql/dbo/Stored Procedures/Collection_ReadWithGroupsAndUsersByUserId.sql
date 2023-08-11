CREATE PROCEDURE [dbo].[Collection_ReadWithGroupsAndUsersByUserId]
	@UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
	
    DECLARE @TempUserCollections TABLE(
        Id UNIQUEIDENTIFIER, 
        OrganizationId UNIQUEIDENTIFIER, 
        Name VARCHAR(MAX), 
        CreationDate DATETIME2(7), 
        RevisionDate DATETIME2(7), 
        ExternalId NVARCHAR(300), 
        ReadOnly BIT, 
        HidePasswords BIT,
        Manage BIT)

    INSERT INTO @TempUserCollections EXEC [dbo].[Collection_ReadByUserId] @UserId
	 
    SELECT
        *
    FROM
        @TempUserCollections C
	 	 
    SELECT
        CG.*
    FROM
        [dbo].[CollectionGroup] CG
    INNER JOIN
        @TempUserCollections C ON C.[Id] = CG.[CollectionId]
	    
    SELECT
        CU.*
    FROM
        [dbo].[CollectionUser] CU
    INNER JOIN
        @TempUserCollections C ON C.[Id] = CU.[CollectionId]
		
END
