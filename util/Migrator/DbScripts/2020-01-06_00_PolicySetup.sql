IF OBJECT_ID('[dbo].[Policy]') IS NULL
BEGIN
    CREATE TABLE [dbo].[Policy] (
        [Id]             UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
        [Type]           TINYINT          NOT NULL,
        [Data]           NVARCHAR (MAX)   NOT NULL,
        [Enabled]        BIT              NOT NULL,
        [CreationDate]   DATETIME2 (7)    NOT NULL,
        [RevisionDate]   DATETIME2 (7)    NOT NULL,
        CONSTRAINT [PK_Policy] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Policy_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_Policy_OrganizationId_Enabled]
        ON [dbo].[Policy]([OrganizationId] ASC, [Enabled] ASC);
END
GO

IF OBJECT_ID('[dbo].[Policy_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Policy_Create]
END
GO

CREATE PROCEDURE [dbo].[Policy_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Enabled BIT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Policy]
    (
        [Id],
        [OrganizationId],
        [Type],
        [Data],
        [Enabled],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Type,
        @Data,
        @Enabled,
        @CreationDate,
        @RevisionDate
    )

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[Policy_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Policy_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[Policy_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrganizationId UNIQUEIDENTIFIER = (SELECT TOP 1 [OrganizationId] FROM [dbo].[Policy] WHERE [Id] = @Id)
    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END

    DELETE
    FROM
        [dbo].[Policy]
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[Policy_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Policy_ReadById]
END
GO

CREATE PROCEDURE [dbo].[Policy_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[PolicyView]
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[Policy_ReadByOrganizationId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Policy_ReadByOrganizationId]
END
GO

CREATE PROCEDURE [dbo].[Policy_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[PolicyView]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[Policy_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Policy_Update]
END
GO

CREATE PROCEDURE [dbo].[Policy_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Enabled BIT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Policy]
    SET
        [OrganizationId] = @OrganizationId,
        [Type] = @Type,
        [Data] = @Data,
        [Enabled] = @Enabled,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
GO

IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'PolicyView')
BEGIN
    DROP VIEW [dbo].[PolicyView]
END
GO

CREATE VIEW [dbo].[PolicyView]
AS
SELECT
    *
FROM
    [dbo].[Policy]
GO
