-- Create PlayData table
IF OBJECT_ID('dbo.PlayData') IS NULL
BEGIN
    CREATE TABLE [dbo].[PlayData] (
        [Id]             UNIQUEIDENTIFIER NOT NULL,
        [PlayId]         NVARCHAR (256)    NOT NULL,
        [UserId]         UNIQUEIDENTIFIER NULL,
        [OrganizationId] UNIQUEIDENTIFIER NULL,
        [CreationDate]   DATETIME2 (7)    NOT NULL,
        CONSTRAINT [PK_PlayData] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_PlayData_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]),
        CONSTRAINT [FK_PlayData_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
        CONSTRAINT [CK_PlayData_UserOrOrganization] CHECK (([UserId] IS NOT NULL AND [OrganizationId] IS NULL) OR ([UserId] IS NULL AND [OrganizationId] IS NOT NULL))
    );

    CREATE NONCLUSTERED INDEX [IX_PlayData_PlayId]
        ON [dbo].[PlayData]([PlayId] ASC);

    CREATE NONCLUSTERED INDEX [IX_PlayData_UserId]
        ON [dbo].[PlayData]([UserId] ASC);

    CREATE NONCLUSTERED INDEX [IX_PlayData_OrganizationId]
        ON [dbo].[PlayData]([OrganizationId] ASC);
END
GO

-- Create PlayData_Create stored procedure
CREATE OR ALTER PROCEDURE [dbo].[PlayData_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @PlayId NVARCHAR(256),
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[PlayData]
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

-- Create PlayData_ReadByPlayId stored procedure
CREATE OR ALTER PROCEDURE [dbo].[PlayData_ReadByPlayId]
    @PlayId NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[PlayData]
    WHERE
        [PlayId] = @PlayId
END
GO

-- Create PlayData_DeleteByPlayId stored procedure
CREATE OR ALTER PROCEDURE [dbo].[PlayData_DeleteByPlayId]
    @PlayId NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[PlayData]
    WHERE
        [PlayId] = @PlayId
END
GO
