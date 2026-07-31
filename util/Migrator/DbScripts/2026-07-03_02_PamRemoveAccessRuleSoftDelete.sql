-- Remove the AccessRule soft-delete. Hard-delete rules (clearing Collection.AccessRuleId links first, since the FK is
-- ON DELETE NO ACTION), drop DeletedDate/DeletedBy, and revert the name unique index from filtered to plain. The
-- written audit log no longer needs the row to survive -- the RuleDeleted event carries the rule name. Dapper/MSSQL
-- only (PAM EF is deferred; the EF model drops the columns to stay in sync with its snapshot).

IF EXISTS (SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_AccessRule_OrganizationId_Name' AND object_id = OBJECT_ID('[dbo].[AccessRule]'))
BEGIN
    DROP INDEX [IX_AccessRule_OrganizationId_Name] ON [dbo].[AccessRule];
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
    WHERE [name] = 'IX_AccessRule_OrganizationId_Name' AND object_id = OBJECT_ID('[dbo].[AccessRule]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_AccessRule_OrganizationId_Name]
        ON [dbo].[AccessRule] ([OrganizationId] ASC, [Name] ASC);
END
GO

IF COL_LENGTH('[dbo].[AccessRule]', 'DeletedDate') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[AccessRule] DROP COLUMN [DeletedDate];
END
GO

IF COL_LENGTH('[dbo].[AccessRule]', 'DeletedBy') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[AccessRule] DROP COLUMN [DeletedBy];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRule_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrganizationId UNIQUEIDENTIFIER

    SELECT @OrganizationId = [OrganizationId]
    FROM [dbo].[AccessRule]
    WHERE [Id] = @Id

    IF @OrganizationId IS NULL
    BEGIN
        RETURN
    END

    -- Clear the collection links first (FK is ON DELETE NO ACTION), then remove the rule.
    UPDATE [dbo].[Collection]
    SET [AccessRuleId] = NULL,
        [RevisionDate] = SYSUTCDATETIME()
    WHERE [AccessRuleId] = @Id

    DELETE FROM [dbo].[AccessRule]
    WHERE [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRule_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(256),
    @Description NVARCHAR(MAX) = NULL,
    @Conditions NVARCHAR(MAX),
    @SingleActiveLease BIT = 0,
    @DefaultLeaseDurationSeconds INT = NULL,
    @MaxLeaseDurationSeconds INT = NULL,
    @Enabled BIT = 1,
    @AllowsExtensions BIT = 0,
    @MaxExtensionDurationSeconds INT = NULL,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @LastEditedBy UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[AccessRule]
    (
        [Id],
        [OrganizationId],
        [Name],
        [Description],
        [Conditions],
        [SingleActiveLease],
        [DefaultLeaseDurationSeconds],
        [MaxLeaseDurationSeconds],
        [Enabled],
        [AllowsExtensions],
        [MaxExtensionDurationSeconds],
        [CreationDate],
        [RevisionDate],
        [LastEditedBy]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Name,
        @Description,
        @Conditions,
        @SingleActiveLease,
        @DefaultLeaseDurationSeconds,
        @MaxLeaseDurationSeconds,
        @Enabled,
        @AllowsExtensions,
        @MaxExtensionDurationSeconds,
        @CreationDate,
        @RevisionDate,
        @LastEditedBy
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRule_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(256),
    @Description NVARCHAR(MAX) = NULL,
    @Conditions NVARCHAR(MAX),
    @SingleActiveLease BIT = 0,
    @DefaultLeaseDurationSeconds INT = NULL,
    @MaxLeaseDurationSeconds INT = NULL,
    @Enabled BIT = 1,
    @AllowsExtensions BIT = 0,
    @MaxExtensionDurationSeconds INT = NULL,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @LastEditedBy UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[AccessRule]
    SET
        [OrganizationId] = @OrganizationId,
        [Name] = @Name,
        [Description] = @Description,
        [Conditions] = @Conditions,
        [SingleActiveLease] = @SingleActiveLease,
        [DefaultLeaseDurationSeconds] = @DefaultLeaseDurationSeconds,
        [MaxLeaseDurationSeconds] = @MaxLeaseDurationSeconds,
        [Enabled] = @Enabled,
        [AllowsExtensions] = @AllowsExtensions,
        [MaxExtensionDurationSeconds] = @MaxExtensionDurationSeconds,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [LastEditedBy] = @LastEditedBy
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRule_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[AccessRule]
    WHERE [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRule_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[AccessRule]
    WHERE [OrganizationId] = @OrganizationId
END
GO

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
