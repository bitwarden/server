IF COL_LENGTH('[dbo].[Installation]', 'LastActivityDate') IS NULL
BEGIN
  ALTER TABLE
    [dbo].[Installation]
  ADD
    [LastActivityDate] DATETIME2 (7) NULL
END
GO

CREATE OR ALTER VIEW [dbo].[InstallationView]
AS
    SELECT
        *
    FROM
        [dbo].[Installation]
GO

CREATE OR ALTER PROCEDURE [dbo].[Installation_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @Email NVARCHAR(256),
    @Key VARCHAR(150),
    @Enabled BIT,
    @CreationDate DATETIME2(7),
    @LastActivityDate DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Installation]
    (
        [Id],
        [Email],
        [Key],
        [Enabled],
        [CreationDate],
        [LastActivityDate]
    )
    VALUES
    (
        @Id,
        @Email,
        @Key,
        @Enabled,
        @CreationDate,
        @LastActivityDate
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Installation_Update]
    @Id UNIQUEIDENTIFIER,
    @Email NVARCHAR(256),
    @Key VARCHAR(150),
    @Enabled BIT,
    @CreationDate DATETIME2(7),
    @LastActivityDate DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Installation]
    SET
        [Email] = @Email,
        [Key] = @Key,
        [Enabled] = @Enabled,
        [CreationDate] = @CreationDate,
        [LastActivityDate] = @LastActivityDate
    WHERE
        [Id] = @Id
END
GO
