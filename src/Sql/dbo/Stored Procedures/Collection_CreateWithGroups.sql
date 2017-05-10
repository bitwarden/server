CREATE PROCEDURE [dbo].[Collection_CreateWithGroups]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @GroupIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_Create] @Id, @OrganizationId, @Name, @CreationDate, @RevisionDate

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
        [ReadOnly]
    )
    SELECT
        @Id,
        [Id],
        0
    FROM
        @GroupIds
    WHERE
        [Id] IN (SELECT [Id] FROM [AvailableGroupsCTE])
END