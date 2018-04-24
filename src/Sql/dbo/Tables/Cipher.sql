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
    CONSTRAINT [PK_Cipher] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Cipher_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
    CONSTRAINT [FK_Cipher_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_Cipher_UserId_OrganizationId_IncludeAll]
    ON [dbo].[Cipher]([UserId] ASC, [OrganizationId] ASC)
    INCLUDE ([Type], [Data], [Favorites], [Folders], [Attachments], [CreationDate], [RevisionDate]);

