CREATE TABLE [dbo].[AccessPolicy]
(
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [Discriminator] NVARCHAR (50) NOT NULL,
    [OrganizationUserId] UNIQUEIDENTIFIER NULL,
    [GroupId] UNIQUEIDENTIFIER NULL,
    [ServiceAccountId] UNIQUEIDENTIFIER NULL,
    [GrantedProjectId] UNIQUEIDENTIFIER NULL,
    [GrantedServiceAccountId] UNIQUEIDENTIFIER NULL,
    [Read] BIT NOT NULL,
    [Write] BIT NOT NULL,
    [CreationDate] DATETIME2 NOT NULL,
    [RevisionDate] DATETIME2 NOT NULL,
    [GrantedSecretId] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_AccessPolicy] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_AccessPolicy_Group_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [dbo].[Group] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AccessPolicy_OrganizationUser_OrganizationUserId] FOREIGN KEY ([OrganizationUserId]) REFERENCES [dbo].[OrganizationUser] ([Id]),
    CONSTRAINT [FK_AccessPolicy_Project_GrantedProjectId] FOREIGN KEY ([GrantedProjectId]) REFERENCES [dbo].[Project] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AccessPolicy_ServiceAccount_GrantedServiceAccountId] FOREIGN KEY ([GrantedServiceAccountId]) REFERENCES [dbo].[ServiceAccount] ([Id]),
    CONSTRAINT [FK_AccessPolicy_ServiceAccount_ServiceAccountId] FOREIGN KEY ([ServiceAccountId]) REFERENCES [dbo].[ServiceAccount] ([Id]),
    CONSTRAINT [FK_AccessPolicy_Secret_GrantedSecretId] FOREIGN KEY ([GrantedSecretId]) REFERENCES [dbo].[Secret] ([Id]) ON DELETE CASCADE
);

GO
CREATE NONCLUSTERED INDEX [IX_AccessPolicy_GroupId] ON [dbo].[AccessPolicy]([GroupId] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_AccessPolicy_OrganizationUserId] ON [dbo].[AccessPolicy]([OrganizationUserId] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_AccessPolicy_GrantedProjectId] ON [dbo].[AccessPolicy]([GrantedProjectId] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_AccessPolicy_ServiceAccountId] ON [dbo].[AccessPolicy]([ServiceAccountId] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_AccessPolicy_GrantedServiceAccountId] ON [dbo].[AccessPolicy]([GrantedServiceAccountId] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_AccessPolicy_GrantedSecretId] ON [dbo].[AccessPolicy]([GrantedSecretId] ASC);
