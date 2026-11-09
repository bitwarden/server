CREATE TABLE [dbo].[TwoFactorRememberToken] (
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [UserId]         UNIQUEIDENTIFIER NOT NULL,
    [DeviceId]       UNIQUEIDENTIFIER NOT NULL,
    [Stamp]          NVARCHAR(50)     NOT NULL,
    [CreationDate]   DATETIME2(7)     NOT NULL,
    [RevisionDate]   DATETIME2(7)     NOT NULL,
    [ExpirationDate] DATETIME2(7)     NOT NULL,
    CONSTRAINT [PK_TwoFactorRememberToken] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_TwoFactorRememberToken_User] FOREIGN KEY ([UserId])
        REFERENCES [dbo].[User] ([Id]),
    CONSTRAINT [FK_TwoFactorRememberToken_Device] FOREIGN KEY ([DeviceId])
        REFERENCES [dbo].[Device] ([Id]) ON DELETE CASCADE
);

GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_TwoFactorRememberToken_UserId_DeviceId]
    ON [dbo].[TwoFactorRememberToken]([UserId] ASC, [DeviceId] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_TwoFactorRememberToken_DeviceId]
    ON [dbo].[TwoFactorRememberToken]([DeviceId] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_TwoFactorRememberToken_ExpirationDate]
    ON [dbo].[TwoFactorRememberToken]([ExpirationDate] ASC);
