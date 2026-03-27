-- Create UserPreferences table
IF OBJECT_ID('dbo.UserPreferences') IS NULL
BEGIN
    CREATE TABLE [dbo].[UserPreferences] (
        [Id]           UNIQUEIDENTIFIER NOT NULL,
        [UserId]       UNIQUEIDENTIFIER NOT NULL,
        [Data]         VARCHAR (MAX)    NOT NULL,
        [CreationDate] DATETIME2 (7)    NOT NULL,
        [RevisionDate] DATETIME2 (7)    NOT NULL,
        CONSTRAINT [PK_UserPreferences] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_UserPreferences_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_UserPreferences_UserId]
        ON [dbo].[UserPreferences]([UserId] ASC);
END
GO

-- Create UserPreferences_Create stored procedure
CREATE OR ALTER PROCEDURE [dbo].[UserPreferences_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @Data VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[UserPreferences]
    (
        [Id],
        [UserId],
        [Data],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @Data,
        @CreationDate,
        @RevisionDate
    )
END
GO

-- Create UserPreferences_Update stored procedure
CREATE OR ALTER PROCEDURE [dbo].[UserPreferences_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Data VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[UserPreferences]
    SET
        [UserId] = @UserId,
        [Data] = @Data,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

-- Create UserPreferences_ReadByUserId stored procedure
CREATE OR ALTER PROCEDURE [dbo].[UserPreferences_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserPreferences]
    WHERE
        [UserId] = @UserId
END
GO

-- Create UserPreferences_DeleteByUserId stored procedure
CREATE OR ALTER PROCEDURE [dbo].[UserPreferences_DeleteByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[UserPreferences]
    WHERE
        [UserId] = @UserId
END
GO
