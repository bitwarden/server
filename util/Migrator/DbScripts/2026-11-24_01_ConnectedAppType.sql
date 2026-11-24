-- Add a Type discriminator to ConnectedApp so the "connected app" storage can host other integration
-- types later. Agent access (1) is the only type today and is the default.

IF COL_LENGTH('[dbo].[ConnectedApp]', 'Type') IS NULL
BEGIN
    ALTER TABLE [dbo].[ConnectedApp]
        ADD [Type] TINYINT NOT NULL CONSTRAINT [DF_ConnectedApp_Type] DEFAULT (1);
END
GO

-- Refresh the view and read procs so their SELECT * surfaces the new column.
EXECUTE sp_refreshview N'[dbo].[ConnectedAppView]'
GO

IF OBJECT_ID('[dbo].[ConnectedApp_ReadById]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[ConnectedApp_ReadById]'
END
GO

IF OBJECT_ID('[dbo].[ConnectedApp_ReadByUserId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[ConnectedApp_ReadByUserId]'
END
GO

CREATE OR ALTER PROCEDURE [dbo].[ConnectedApp_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @PartnerClientId NVARCHAR(200),
    @Name NVARCHAR(200),
    @Key VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @RevokedDate DATETIME2(7) = NULL,
    @Type TINYINT = 1
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[ConnectedApp]
    (
        [Id],
        [UserId],
        [PartnerClientId],
        [Name],
        [Key],
        [CreationDate],
        [RevisionDate],
        [RevokedDate],
        [Type]
    )
    VALUES
    (
        @Id,
        @UserId,
        @PartnerClientId,
        @Name,
        @Key,
        @CreationDate,
        @RevisionDate,
        @RevokedDate,
        @Type
    )

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[ConnectedApp_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @PartnerClientId NVARCHAR(200),
    @Name NVARCHAR(200),
    @Key VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @RevokedDate DATETIME2(7) = NULL,
    @Type TINYINT = 1
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[ConnectedApp]
    SET
        [UserId] = @UserId,
        [PartnerClientId] = @PartnerClientId,
        [Name] = @Name,
        [Key] = @Key,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [RevokedDate] = @RevokedDate,
        [Type] = @Type
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END
GO
