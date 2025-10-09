CREATE TABLE [dbo].[SecretVersion] (
    [Id]                       UNIQUEIDENTIFIER NOT NULL,
    [SecretId]                 UNIQUEIDENTIFIER NOT NULL,
    [Value]                    NVARCHAR (MAX)   NOT NULL,
    [VersionDate]              DATETIME2 (7)    NOT NULL,
    [EditorServiceAccountId]   UNIQUEIDENTIFIER NULL,
    [EditorOrganizationUserId] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_SecretVersion] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_SecretVersion_OrganizationUser] FOREIGN KEY ([EditorOrganizationUserId]) REFERENCES [dbo].[OrganizationUser] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_SecretVersion_Secret] FOREIGN KEY ([SecretId]) REFERENCES [dbo].[Secret] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SecretVersion_ServiceAccount] FOREIGN KEY ([EditorServiceAccountId]) REFERENCES [dbo].[ServiceAccount] ([Id]) ON DELETE SET NULL
);

GO
CREATE NONCLUSTERED INDEX [IX_SecretVersion_SecretId]
    ON [dbo].[SecretVersion]([SecretId] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_SecretVersion_EditorServiceAccountId]
    ON [dbo].[SecretVersion]([EditorServiceAccountId] ASC)
    WHERE [EditorServiceAccountId] IS NOT NULL;

GO
CREATE NONCLUSTERED INDEX [IX_SecretVersion_EditorOrganizationUserId]
    ON [dbo].[SecretVersion]([EditorOrganizationUserId] ASC)
    WHERE [EditorOrganizationUserId] IS NOT NULL;