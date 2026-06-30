IF COL_LENGTH('[dbo].[AccessRule]', 'DeletedDate') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessRule]
        ADD [DeletedDate] DATETIME2(7) NULL;
END
GO

IF COL_LENGTH('[dbo].[AccessRule]', 'DeletedBy') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessRule]
        ADD [DeletedBy] UNIQUEIDENTIFIER NULL;
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
    @LastEditedBy UNIQUEIDENTIFIER = NULL,
    @DeletedDate DATETIME2(7) = NULL,
    @DeletedBy UNIQUEIDENTIFIER = NULL
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
        [LastEditedBy],
        [DeletedDate],
        [DeletedBy]
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
        @LastEditedBy,
        @DeletedDate,
        @DeletedBy
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
    @LastEditedBy UNIQUEIDENTIFIER = NULL,
    @DeletedDate DATETIME2(7) = NULL,
    @DeletedBy UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- @DeletedDate / @DeletedBy are accepted to match the entity shape but deliberately not written here; soft-delete
    -- state is owned solely by AccessRule_DeleteById, so updating a rule can never revive a deleted one.
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
