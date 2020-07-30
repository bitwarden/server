IF EXISTS (
    SELECT *  FROM sys.indexes  WHERE [Name] = 'IX_Grant_SubjectId_ClientId_Type'
    AND object_id = OBJECT_ID('[dbo].[Grant]')
)
BEGIN
    DROP INDEX [IX_Grant_SubjectId_ClientId_Type]
        ON [dbo].[Grant]
END
GO

IF EXISTS (
    SELECT *  FROM sys.indexes  WHERE [Name] = 'IX_Grant_SubjectId_SessionId_Type'
    AND object_id = OBJECT_ID('[dbo].[Grant]')
)
BEGIN
    DROP INDEX [IX_Grant_SubjectId_SessionId_Type]
        ON [dbo].[Grant]
END
GO

IF COL_LENGTH('[dbo].[Grant]', 'SessionId') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[Grant]
    ADD
        [SessionId] NVARCHAR (100) NULL
END
GO

IF COL_LENGTH('[dbo].[Grant]', 'Description') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[Grant]
    ADD
        [Description] NVARCHAR (200) NULL
END
GO

IF COL_LENGTH('[dbo].[Grant]', 'ConsumedDate') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[Grant]
    ADD
        [ConsumedDate] DATETIME2 (7) NULL
END
GO

ALTER TABLE
    [dbo].[Grant]
ALTER COLUMN
    [Type] NVARCHAR (50) NOT NULL
GO

ALTER TABLE
    [dbo].[Grant]
ALTER COLUMN
    [SubjectId] NVARCHAR (200) NULL
GO

ALTER TABLE
    [dbo].[Grant]
ALTER COLUMN
    [ClientId] NVARCHAR (200) NOT NULL
GO

IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'GrantView')
BEGIN
    DROP VIEW [dbo].[GrantView]
END
GO

CREATE VIEW [dbo].[GrantView]
AS
SELECT
    *
FROM
    [dbo].[Grant]
GO

IF NOT EXISTS (
    SELECT *  FROM sys.indexes  WHERE [Name] = 'IX_Grant_SubjectId_ClientId_Type'
    AND object_id = OBJECT_ID('[dbo].[Grant]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Grant_SubjectId_ClientId_Type]
        ON [dbo].[Grant]([SubjectId] ASC, [ClientId] ASC, [Type] ASC)
    -- TODO WITH ONLINE
END
GO

IF NOT EXISTS (
    SELECT *  FROM sys.indexes  WHERE [Name] = 'IX_Grant_SubjectId_SessionId_Type'
    AND object_id = OBJECT_ID('[dbo].[Grant]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Grant_SubjectId_SessionId_Type]
        ON [dbo].[Grant]([SubjectId] ASC, [SessionId] ASC, [Type] ASC)
    -- TODO WITH ONLINE
END
GO

IF OBJECT_ID('[dbo].[Grant_Delete]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Grant_Delete]
END
GO

CREATE PROCEDURE [dbo].[Grant_Delete]
    @SubjectId NVARCHAR(200),
    @SessionId NVARCHAR(100),
    @ClientId NVARCHAR(200),
    @Type NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[Grant]
    WHERE
        (@SubjectId IS NULL OR [SubjectId] = @SubjectId)
        AND (@ClientId IS NULL OR [ClientId] = @ClientId)
        AND (@SessionId IS NULL OR [SessionId] = @SessionId)
        AND (@Type IS NULL OR [Type] = @Type)
END
GO

IF OBJECT_ID('[dbo].[Grant_Read]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Grant_Read]
END
GO

CREATE PROCEDURE [dbo].[Grant_Read]
    @SubjectId NVARCHAR(200),
    @SessionId NVARCHAR(100),
    @ClientId NVARCHAR(200),
    @Type NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[GrantView]
    WHERE
        (@SubjectId IS NULL OR [SubjectId] = @SubjectId)
        AND (@ClientId IS NULL OR [ClientId] = @ClientId)
        AND (@SessionId IS NULL OR [SessionId] = @SessionId)
        AND (@Type IS NULL OR [Type] = @Type)
END
GO

IF OBJECT_ID('[dbo].[Grant_Save]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Grant_Save]
END
GO

CREATE PROCEDURE [dbo].[Grant_Save]
    @Key  NVARCHAR(200),
    @Type  NVARCHAR(50),
    @SubjectId NVARCHAR(200),
    @SessionId NVARCHAR(100),
    @ClientId NVARCHAR(200),
    @Description NVARCHAR(200),
    @CreationDate DATETIME2,
    @ExpirationDate DATETIME2,
    @ConsumedDate DATETIME2,
    @Data NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    MERGE
        [dbo].[Grant] AS [Target]
    USING
    ( 
        VALUES
        (
            @Key,
            @Type,
            @SubjectId,
            @SessionId,
            @ClientId,
            @Description,
            @CreationDate,
            @ExpirationDate,
            @ConsumedDate,
            @Data
        )
    ) AS [Source]
    (
        [Key],
        [Type],
        [SubjectId],
        [SessionId],
        [ClientId],
        [Description],
        [CreationDate],
        [ExpirationDate],
        [ConsumedDate],
        [Data]
    ) 
    ON
        [Target].[Key] = [Source].[Key]
    WHEN MATCHED THEN
        UPDATE
        SET
            [Type] = [Source].[Type],
            [SubjectId] = [Source].[SubjectId],
            [SessionId] = [Source].[SessionId],
            [ClientId] = [Source].[ClientId],
            [Description] = [Source].[Description],
            [CreationDate] = [Source].[CreationDate],
            [ExpirationDate] = [Source].[ExpirationDate],
            [ConsumedDate] = [Source].[ConsumedDate],
            [Data] = [Source].[Data]
    WHEN NOT MATCHED THEN
        INSERT
        (
            [Key],
            [Type],
            [SubjectId],
            [SessionId],
            [ClientId],
            [Description],
            [CreationDate],
            [ExpirationDate],
            [ConsumedDate],
            [Data]
        )
        VALUES
        (
            [Source].[Key],
            [Source].[Type],
            [Source].[SubjectId],
            [Source].[SessionId],
            [Source].[ClientId],
            [Source].[Description],
            [Source].[CreationDate],
            [Source].[ExpirationDate],
            [Source].[ConsumedDate],
            [Source].[Data]
        )
    ;
END
GO

IF OBJECT_ID('[dbo].[Grant_DeleteBySubjectIdClientId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Grant_DeleteBySubjectIdClientId]
END
GO

IF OBJECT_ID('[dbo].[Grant_DeleteBySubjectIdClientIdType]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Grant_DeleteBySubjectIdClientIdType]
END
GO

IF OBJECT_ID('[dbo].[Grant_ReadBySubjectId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Grant_ReadBySubjectId]
END
GO
