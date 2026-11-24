IF OBJECT_ID('[dbo].[ConnectedApp]') IS NULL
BEGIN
    CREATE TABLE [dbo].[ConnectedApp] (
        [Id]              UNIQUEIDENTIFIER NOT NULL,
        [UserId]          UNIQUEIDENTIFIER NOT NULL,
        [PartnerClientId] NVARCHAR(200)    NOT NULL,
        [Name]            NVARCHAR(200)    NOT NULL,
        [Key]             VARCHAR(MAX)     NOT NULL,
        [CreationDate]    DATETIME2(7)     NOT NULL,
        [RevisionDate]    DATETIME2(7)     NOT NULL,
        [RevokedDate]     DATETIME2(7)     NULL,
        CONSTRAINT [PK_ConnectedApp] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ConnectedApp_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_ConnectedApp_UserId]
        ON [dbo].[ConnectedApp]([UserId] ASC);
END
GO

CREATE OR ALTER VIEW [dbo].[ConnectedAppView]
AS
SELECT
    *
FROM
    [dbo].[ConnectedApp]
GO

CREATE OR ALTER PROCEDURE [dbo].[ConnectedApp_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @PartnerClientId NVARCHAR(200),
    @Name NVARCHAR(200),
    @Key VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @RevokedDate DATETIME2(7) = NULL
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
        [RevokedDate]
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
        @RevokedDate
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
    @RevokedDate DATETIME2(7) = NULL
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
        [RevokedDate] = @RevokedDate
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[ConnectedApp_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ConnectedAppView]
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[ConnectedApp_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ConnectedAppView]
    WHERE
        [UserId] = @UserId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[ConnectedApp_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserId UNIQUEIDENTIFIER

    SELECT TOP 1
        @UserId = [UserId]
    FROM
        [dbo].[ConnectedApp]
    WHERE
        [Id] = @Id

    DELETE
    FROM
        [dbo].[ConnectedApp]
    WHERE
        [Id] = @Id

    IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
END
GO
