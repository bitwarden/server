CREATE TABLE [dbo].[FolderCipher] (
    [FolderId] UNIQUEIDENTIFIER NOT NULL,
    [CipherId] UNIQUEIDENTIFIER NOT NULL,
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_FolderCipher] PRIMARY KEY CLUSTERED ([UserId] ASC, [FolderId] ASC, [CipherId] ASC),
    CONSTRAINT [FK_FolderCipher_Cipher] FOREIGN KEY ([CipherId]) REFERENCES [dbo].[Cipher] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_FolderCipher_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_FolderCipher_Folder] FOREIGN KEY ([FolderId]) REFERENCES [dbo].[Folder] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_FolderCipher_CipherId]
    ON [dbo].[FolderCipher]([CipherId] ASC);

