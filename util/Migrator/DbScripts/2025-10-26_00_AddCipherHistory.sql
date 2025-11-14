SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.tables
    WHERE name = 'CipherHistory'
      AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE [dbo].[CipherHistory] (
        [Id]             UNIQUEIDENTIFIER NOT NULL,
        [CipherId]       UNIQUEIDENTIFIER NOT NULL,
        [UserId]         UNIQUEIDENTIFIER NULL,
        [OrganizationId] UNIQUEIDENTIFIER NULL,
        [Type]           TINYINT          NOT NULL,
        [Data]           NVARCHAR (MAX)   NOT NULL,
        [Favorites]      NVARCHAR (MAX)   NULL,
        [Folders]        NVARCHAR (MAX)   NULL,
        [Attachments]    NVARCHAR (MAX)   NULL,
        [CreationDate]   DATETIME2 (7)    NOT NULL,
        [RevisionDate]   DATETIME2 (7)    NOT NULL,
        [DeletedDate]    DATETIME2 (7)    NULL,
        [Reprompt]       TINYINT          NULL,
        [Key]            VARCHAR (MAX)    NULL,
        [ArchivedDate]   DATETIME2 (7)    NULL,
        [HistoryDate]    DATETIME2 (7)    NOT NULL,
        CONSTRAINT [PK_CipherHistory] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_CipherHistory_Cipher'
)
BEGIN
    ALTER TABLE [dbo].[CipherHistory]
    ADD CONSTRAINT [FK_CipherHistory_Cipher]
        FOREIGN KEY ([CipherId])
        REFERENCES [dbo].[Cipher] ([Id])
        ON DELETE CASCADE;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_CipherHistory_CipherId'
      AND object_id = OBJECT_ID('[dbo].[CipherHistory]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_CipherHistory_CipherId]
        ON [dbo].[CipherHistory]([CipherId] ASC);
END
GO

IF OBJECT_ID('[dbo].[CipherHistory_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CipherHistory_Create];
END
GO

CREATE PROCEDURE [dbo].[CipherHistory_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @CipherId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Favorites NVARCHAR(MAX),
    @Folders NVARCHAR(MAX),
    @Attachments NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @DeletedDate DATETIME2(7),
    @Reprompt TINYINT,
    @Key VARCHAR(MAX),
    @ArchivedDate DATETIME2(7),
    @HistoryDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON;

    IF @Id IS NULL
    BEGIN
        SET @Id = NEWID();
    END

    INSERT INTO [dbo].[CipherHistory]
    (
        [Id],
        [CipherId],
        [UserId],
        [OrganizationId],
        [Type],
        [Data],
        [Favorites],
        [Folders],
        [Attachments],
        [CreationDate],
        [RevisionDate],
        [DeletedDate],
        [Reprompt],
        [Key],
        [ArchivedDate],
        [HistoryDate]
    )
    VALUES
    (
        @Id,
        @CipherId,
        @UserId,
        @OrganizationId,
        @Type,
        @Data,
        @Favorites,
        @Folders,
        @Attachments,
        @CreationDate,
        @RevisionDate,
        @DeletedDate,
        @Reprompt,
        @Key,
        @ArchivedDate,
        @HistoryDate
    );
END
GO
