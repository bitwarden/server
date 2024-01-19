IF COL_LENGTH('[dbo].[AccessPolicy]', 'GrantedSecretId') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessPolicy] ADD [GrantedSecretId] [uniqueidentifier] NULL
    CONSTRAINT [FK_AccessPolicy_Secret_GrantedSecretId] FOREIGN KEY ([GrantedSecretId]) REFERENCES [Secret] ([Id]) ON DELETE CASCADE
END
GO

IF NOT EXISTS(SELECT name
FROM sys.indexes
WHERE name = 'IX_AccessPolicy_GrantedSecretId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessPolicy_GrantedSecretId] ON [dbo].[AccessPolicy] ([GrantedSecretId] ASC);
END
GO
