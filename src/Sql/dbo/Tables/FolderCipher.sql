CREATE TABLE [dbo].[FolderCipher] (
    [FolderId] UNIQUEIDENTIFIER NOT NULL,
    [CipherId] UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_FolderCipher] PRIMARY KEY CLUSTERED ([FolderId] ASC, [CipherId] ASC),
    CONSTRAINT [FK_FolderCipher_Cipher] FOREIGN KEY ([CipherId]) REFERENCES [dbo].[Cipher] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_FolderCipher_Folder] FOREIGN KEY ([FolderId]) REFERENCES [dbo].[Folder] ([Id])
);

