-- PAM Credential Leasing: per-rule lease durations.
--
-- AccessRule gains [DefaultLeaseDurationSeconds] and [MaxLeaseDurationSeconds] (both nullable INT seconds).
-- The default is used to pre-fill a request opened under the rule; null means the backend default applies.
-- The max is a hard ceiling on any single lease granted under the rule; null means no per-rule cap. The
-- client already sends and reads both fields, but the server had nowhere to persist them, so they were
-- dropped on every save. AccessRule_Create/_Update gain matching parameters (defaulting to NULL so the
-- procs stay backward compatible). The Read procs use SELECT * and pick the new columns up automatically.
--
-- PAM is an unshipped POC behind the pm-37044-pam-v-0 flag with no production data; server + migration deploy
-- together, so the affected procs are altered in place rather than versioned.

IF COL_LENGTH('[dbo].[AccessRule]', 'DefaultLeaseDurationSeconds') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessRule]
        ADD [DefaultLeaseDurationSeconds] INT NULL
END
GO

IF COL_LENGTH('[dbo].[AccessRule]', 'MaxLeaseDurationSeconds') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessRule]
        ADD [MaxLeaseDurationSeconds] INT NULL
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
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO
