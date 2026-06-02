-- Read an access rule along with the IDs of the collections it governs (second result set)
CREATE OR ALTER PROCEDURE [dbo].[AccessRule_ReadDetailsById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[AccessRule]
    WHERE [Id] = @Id

    SELECT [Id]
    FROM [dbo].[Collection]
    WHERE [AccessRuleId] = @Id
END
GO

-- Read all access rules in an organization (result set 1) along with their governed collections (result set 2)
CREATE OR ALTER PROCEDURE [dbo].[AccessRule_ReadDetailsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[AccessRule]
    WHERE [OrganizationId] = @OrganizationId

    SELECT
        [AccessRuleId],
        [Id] AS [CollectionId]
    FROM [dbo].[Collection]
    WHERE [OrganizationId] = @OrganizationId
        AND [AccessRuleId] IS NOT NULL
END
GO

-- Replace an access rule's collection associations: clear the rule from @ToClear and point @ToAssign at it.
-- Both sets are scoped to @OrganizationId.
CREATE OR ALTER PROCEDURE [dbo].[Collection_SetAccessRuleAssociations]
    @AccessRuleId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ToAssign AS [dbo].[GuidIdArray] READONLY,
    @ToClear AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @RevisionDate DATETIME2(7) = SYSUTCDATETIME()

    BEGIN TRANSACTION

    UPDATE
        C
    SET
        C.[AccessRuleId] = NULL,
        C.[RevisionDate] = @RevisionDate
    FROM
        [dbo].[Collection] C
    INNER JOIN
        @ToClear T ON T.[Id] = C.[Id]
    WHERE
        C.[OrganizationId] = @OrganizationId
        AND C.[AccessRuleId] = @AccessRuleId

    UPDATE
        C
    SET
        C.[AccessRuleId] = @AccessRuleId,
        C.[RevisionDate] = @RevisionDate
    FROM
        [dbo].[Collection] C
    INNER JOIN
        @ToAssign T ON T.[Id] = C.[Id]
    WHERE
        C.[OrganizationId] = @OrganizationId

    COMMIT TRANSACTION

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
GO

-- Deleting an access rule first clears it from any collections it governs (FK is NO ACTION), then deletes the rule.
CREATE OR ALTER PROCEDURE [dbo].[AccessRule_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @RevisionDate DATETIME2(7) = SYSUTCDATETIME()
    DECLARE @OrganizationId UNIQUEIDENTIFIER

    SELECT @OrganizationId = [OrganizationId]
    FROM [dbo].[AccessRule]
    WHERE [Id] = @Id

    BEGIN TRANSACTION

    UPDATE
        [dbo].[Collection]
    SET
        [AccessRuleId] = NULL,
        [RevisionDate] = @RevisionDate
    WHERE
        [AccessRuleId] = @Id

    DELETE FROM [dbo].[AccessRule] WHERE [Id] = @Id

    COMMIT TRANSACTION

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END
END
GO
