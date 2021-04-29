
CREATE TABLE [dbo].[Cipher] (
    [Id]             UNIQUEIDENTIFIER NOT NULL,
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
    CONSTRAINT [PK_Cipher] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Cipher_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
    CONSTRAINT [FK_Cipher_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_Cipher_UserId_OrganizationId_IncludeAll]
    ON [dbo].[Cipher]([UserId] ASC, [OrganizationId] ASC)
    INCLUDE ([Type], [Data], [Favorites], [Folders], [Attachments], [CreationDate], [RevisionDate], [DeletedDate]);


GO
CREATE NONCLUSTERED INDEX [IX_Cipher_OrganizationId]
    ON [dbo].[Cipher]([OrganizationId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Cipher_DeletedDate]
    ON [dbo].[Cipher]([DeletedDate] ASC);

