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
    CONSTRAINT [PK_CipherHistory] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_CipherHistory_Cipher] FOREIGN KEY ([CipherId]) REFERENCES [dbo].[Cipher] ([Id]) ON DELETE CASCADE
);

GO
CREATE NONCLUSTERED INDEX [IX_CipherHistory_CipherId]
    ON [dbo].[CipherHistory]([CipherId] ASC);

GO
