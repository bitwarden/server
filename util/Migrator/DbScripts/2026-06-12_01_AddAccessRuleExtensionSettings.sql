-- PAM Credential Leasing: per-rule extension settings.
--
-- AccessRule gains [AllowsExtensions] (BIT NOT NULL, default 0) and [MaxExtensions] (INT NULL). When
-- AllowsExtensions is true, a member holding an active lease under the rule may extend it (always auto-approved),
-- up to MaxExtensions times. AccessRule_Create/_Update gain matching parameters (defaulting so the procs stay
-- backward compatible). The Read procs use SELECT * and pick the new columns up automatically.
--
-- PAM is an unshipped POC behind the pm-37044-pam-v-0 flag with no production data; server + migration deploy
-- together, so the affected procs are altered in place rather than versioned.

IF COL_LENGTH('[dbo].[AccessRule]', 'AllowsExtensions') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessRule]
        ADD [AllowsExtensions] BIT NOT NULL CONSTRAINT [DF_AccessRule_AllowsExtensions] DEFAULT (0)
END
GO

IF COL_LENGTH('[dbo].[AccessRule]', 'MaxExtensions') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessRule]
        ADD [MaxExtensions] INT NULL
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
    @MaxExtensions INT = NULL,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
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
        [MaxExtensions],
        [CreationDate],
        [RevisionDate]
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
        @MaxExtensions,
        @CreationDate,
        @RevisionDate
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
    @MaxExtensions INT = NULL,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
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
        [MaxExtensions] = @MaxExtensions,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO
