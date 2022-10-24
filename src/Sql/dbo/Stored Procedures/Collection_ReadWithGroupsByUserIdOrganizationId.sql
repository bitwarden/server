CREATE PROCEDURE [dbo].[Collection_ReadWithGroupsByUserIdOrganizationId]
	@UserId UNIQUEIDENTIFIER,
	@OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
	SET NOCOUNT ON
	
	DECLARE @TempUserCollections TABLE(Id UNIQUEIDENTIFIER, OrganizationId UNIQUEIDENTIFIER, Name VARCHAR(MAX), CreationDate DATETIME2(7), RevisionDate DATETIME2(7), ExternalId NVARCHAR(300), ReadOnly BIT, HidePasswords BIT)

	INSERT INTO @TempUserCollections EXEC [dbo].[Collection_ReadByUserId] @UserId
	 
	SELECT
		*
	FROM
	 	 @TempUserCollections C
	WHERE
		C.[OrganizationId] = @OrganizationId
	 	 
	SELECT
		CG.*
	FROM
	 	[dbo].[CollectionGroup] CG
	INNER JOIN
	    @TempUserCollections C ON C.[Id] = CG.[CollectionId]
	WHERE
	    C.[OrganizationId] = @OrganizationId

END