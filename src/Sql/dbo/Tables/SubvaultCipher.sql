CREATE TABLE [dbo].[SubvaultCipher] (
    [SubvaultId] UNIQUEIDENTIFIER NOT NULL,
    [CipherId]   UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_SubvaultCipher] PRIMARY KEY CLUSTERED ([SubvaultId] ASC, [CipherId] ASC),
    CONSTRAINT [FK_SubvaultCipher_Cipher] FOREIGN KEY ([CipherId]) REFERENCES [dbo].[Cipher] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SubvaultCipher_Subvault] FOREIGN KEY ([SubvaultId]) REFERENCES [dbo].[Subvault] ([Id]) ON DELETE CASCADE
);

