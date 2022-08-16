IF OBJECT_ID('[dbo].[Project]') IS NULL
BEGIN
CREATE TABLE [dbo].[Project] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]    UNIQUEIDENTIFIER NULL,
    [Name]              NVARCHAR(MAX), 
    [CreationDate]      DATETIME2 (7),
    [RevisionDate]      DATETIME2 (7), 
    [DeletedDate]       DATETIME2 (7),
    CONSTRAINT [PK_Project] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Project_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
);
END

GO

IF OBJECT_ID('[dbo].[Project_ReadByOrganizationId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Project_ReadByOrganizationId]
END
GO

CREATE PROCEDURE [dbo].[Project_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        Id,
        OrganizationId, 
        Name,
        CreationDate,
        RevisionDate,
        DeletedDate
    FROM
        [dbo].[Project]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO


IF OBJECT_ID('[dbo].[Project_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Project_ReadById]
END
GO

CREATE PROCEDURE [dbo].[Project_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        Id,
        OrganizationId, 
        Name,
        CreationDate,
        RevisionDate,
        DeletedDate
    FROM
        [dbo].[Project]
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[Project_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Project_Create]
END
GO

CREATE PROCEDURE [dbo].[Project_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(MAX),
    @CreationDate DATETIME2 (7),
    @RevisionDate DATETIME2 (7),
    @DeletedDate DATETIME2 (7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Project]
    (
        Id,
        OrganizationId, 
        Name,
        CreationDate,
        RevisionDate,
        DeletedDate
    )
    VALUES 
    (
        @Id,
        @OrganizationId,
        @Name,
        @CreationDate,
        @RevisionDate,
        @DeletedDate
    )
END

IF OBJECT_ID('[dbo].[Project_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Project_Update]
END
GO

CREATE PROCEDURE [dbo].[Project_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(MAX),
    @CreationDate DATETIME2 (7),
    @RevisionDate DATETIME2 (7),
    @DeletedDate DATETIME2 (7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE 
        [dbo].[Project]
    SET 
        [OrganizationId] = @OrganizationId,
        [Name] = @Name,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [DeletedDate] = @DeletedDate
    WHERE 
        [Id] = @Id
END
GO

CREATE PROCEDURE [dbo].[Project_SoftDelete]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @OrganizationId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    CREATE TABLE #Temp
    ( 
        [Id] UNIQUEIDENTIFIER NOT NULL
    )

    INSERT INTO #Temp
    SELECT
        [Id]
    FROM
        [dbo].[Project]  
    WHERE
        OrganizationId = @OrganizationId
        AND [DeletedDate] IS NULL
        AND [Id] IN (SELECT * FROM @Ids)

    -- Delete Project
    DECLARE @UtcNow DATETIME2(7) = GETUTCDATE();
    UPDATE
        [dbo].[Project]
    SET
        [DeletedDate] = @UtcNow,
        [RevisionDate] = @UtcNow
    WHERE
        [Id] IN (SELECT [Id] FROM #Temp)

    DROP TABLE #Temp
END
GO