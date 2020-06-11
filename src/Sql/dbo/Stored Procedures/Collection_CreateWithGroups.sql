CREATE PROCEDURE [dbo].[Collection_CreateWithGroups]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Groups AS [dbo].[SelectionReadOnlyArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_Create] @Id, @OrganizationId, @Name, @ExternalId, @CreationDate, @RevisionDate

    ;WITH [AvailableGroupsCTE] AS(
        SELECT
            [Id]
        FROM
            [dbo].[Group]
        WHERE
            [OrganizationId] = @OrganizationId
    )
    INSERT INTO [dbo].[CollectionGroup]
    (
        [CollectionId],
        [GroupId],
        [ReadOnly],
        [HidePasswords]
    )
    SELECT
        @Id,
        [Id],
        [ReadOnly],
        [HidePasswords]
    FROM
        @Groups
    WHERE
        [Id] IN (SELECT [Id] FROM [AvailableGroupsCTE])

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END