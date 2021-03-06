-- Check email column is 50 characters long (100 bytes)
IF COL_LENGTH('[dbo].[Installation]', 'Email') = 100
BEGIN
	ALTER TABLE [dbo].[Installation] 
	ALTER COLUMN 
		Email NVARCHAR(256) NOT NULL
END
GO

-- Recreate procedure Installation_Create
IF OBJECT_ID('[dbo].[Installation_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Installation_Create]
END
GO

CREATE PROCEDURE [dbo].[Installation_Create]
    @Id UNIQUEIDENTIFIER,
    @Email NVARCHAR(256),
    @Key VARCHAR(150),
    @Enabled BIT,
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Installation]
    (
        [Id],
        [Email],
        [Key],
        [Enabled],
        [CreationDate]
    )
    VALUES
    (
        @Id,
        @Email,
        @Key,
        @Enabled,
        @CreationDate
    )
END
GO

-- Recreate procedure Installation_Update
IF OBJECT_ID('[dbo].[Installation_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Installation_Update]
END
GO

CREATE PROCEDURE [dbo].[Installation_Update]
    @Id UNIQUEIDENTIFIER,
    @Email NVARCHAR(256),
    @Key VARCHAR(150),
    @Enabled BIT,
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Installation]
    SET
        [Email] = @Email,
        [Key] = @Key,
        [Enabled] = @Enabled,
        [CreationDate] = @CreationDate
    WHERE
        [Id] = @Id
END