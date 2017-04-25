CREATE TABLE [dbo].[Cipher] (
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [UserId]         UNIQUEIDENTIFIER NULL,
    [OrganizationId] UNIQUEIDENTIFIER NULL,
    [Type]           TINYINT          NOT NULL,
    [Data]           NVARCHAR (MAX)   NOT NULL,
    [Favorites]      NVARCHAR (MAX)   NULL,
    [Folders]        NVARCHAR (MAX)   NULL,
    [CreationDate]   DATETIME2 (7)    NOT NULL,
    [RevisionDate]   DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Cipher] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Cipher_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
    CONSTRAINT [FK_Cipher_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_Cipher_OrganizationId_Type]
    ON [dbo].[Cipher]([OrganizationId] ASC, [Type] ASC) WHERE ([OrganizationId] IS NOT NULL);


GO
CREATE NONCLUSTERED INDEX [IX_Cipher_UserId_Type]
    ON [dbo].[Cipher]([UserId] ASC, [Type] ASC);

