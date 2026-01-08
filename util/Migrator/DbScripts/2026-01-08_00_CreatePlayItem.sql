-- Create PlayItem table
IF OBJECT_ID('dbo.PlayItem') IS NULL
BEGIN
    CREATE TABLE [dbo].[PlayItem] (
        [Id]             UNIQUEIDENTIFIER NOT NULL,
        [PlayId]         NVARCHAR (256)    NOT NULL,
        [UserId]         UNIQUEIDENTIFIER NULL,
        [OrganizationId] UNIQUEIDENTIFIER NULL,
        [CreationDate]   DATETIME2 (7)    NOT NULL,
        CONSTRAINT [PK_PlayItem] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PlayItem_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PlayItem_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [CK_PlayItem_UserOrOrganization] CHECK (([UserId] IS NOT NULL AND [OrganizationId] IS NULL) OR ([UserId] IS NULL AND [OrganizationId] IS NOT NULL))
    );

    CREATE NONCLUSTERED INDEX [IX_PlayItem_PlayId]
        ON [dbo].[PlayItem]([PlayId] ASC);

    CREATE NONCLUSTERED INDEX [IX_PlayItem_UserId]
        ON [dbo].[PlayItem]([UserId] ASC);

    CREATE NONCLUSTERED INDEX [IX_PlayItem_OrganizationId]
        ON [dbo].[PlayItem]([OrganizationId] ASC);
END
GO

-- Create PlayItem_Create stored procedure
CREATE OR ALTER PROCEDURE [dbo].[PlayItem_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @PlayId NVARCHAR(256),
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[PlayItem]
    (
        [Id],
        [PlayId],
        [UserId],
        [OrganizationId],
        [CreationDate]
    )
    VALUES
    (
        @Id,
        @PlayId,
        @UserId,
        @OrganizationId,
        @CreationDate
    )
END
GO

-- Create PlayItem_ReadByPlayId stored procedure
CREATE OR ALTER PROCEDURE [dbo].[PlayItem_ReadByPlayId]
    @PlayId NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Id],
        [PlayId],
        [UserId],
        [OrganizationId],
        [CreationDate]
    FROM
        [dbo].[PlayItem]
    WHERE
        [PlayId] = @PlayId
END
GO

-- Create PlayItem_DeleteByPlayId stored procedure
CREATE OR ALTER PROCEDURE [dbo].[PlayItem_DeleteByPlayId]
    @PlayId NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[PlayItem]
    WHERE
        [PlayId] = @PlayId
END
GO
