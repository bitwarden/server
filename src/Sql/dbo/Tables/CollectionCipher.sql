CREATE TABLE [dbo].[CollectionCipher] (
    [CollectionId] UNIQUEIDENTIFIER NOT NULL,
    [CipherId]     UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_CollectionCipher] PRIMARY KEY CLUSTERED ([CollectionId] ASC, [CipherId] ASC),
    CONSTRAINT [FK_CollectionCipher_Cipher] FOREIGN KEY ([CipherId]) REFERENCES [dbo].[Cipher] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_CollectionCipher_Collection] FOREIGN KEY ([CollectionId]) REFERENCES [dbo].[Collection] ([Id]) ON DELETE CASCADE
);


GO
CREATE NONCLUSTERED INDEX [IX_CollectionCipher_CipherId]
    ON [dbo].[CollectionCipher]([CipherId] ASC);

