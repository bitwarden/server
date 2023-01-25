CREATE TABLE [AccessPolicy] (
    [Id]                      UNIQUEIDENTIFIER NOT NULL,
    [Discriminator]           NVARCHAR(50)     NOT NULL,
    [OrganizationUserId]      UNIQUEIDENTIFIER NULL,
    [GroupId]                 UNIQUEIDENTIFIER NULL,
    [ServiceAccountId]        UNIQUEIDENTIFIER NULL,
    [GrantedProjectId]        UNIQUEIDENTIFIER NULL,
    [GrantedServiceAccountId] UNIQUEIDENTIFIER NULL,
    [Read]                    BIT NOT NULL,
    [Write]                   BIT NOT NULL,
    [CreationDate]            DATETIME2 NOT NULL,
    [RevisionDate]            DATETIME2 NOT NULL,
    CONSTRAINT [PK_AccessPolicy] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_AccessPolicy_Group_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [Group] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AccessPolicy_OrganizationUser_OrganizationUserId] FOREIGN KEY ([OrganizationUserId]) REFERENCES [OrganizationUser] ([Id]),
    CONSTRAINT [FK_AccessPolicy_Project_GrantedProjectId] FOREIGN KEY ([GrantedProjectId]) REFERENCES [Project] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AccessPolicy_ServiceAccount_GrantedServiceAccountId] FOREIGN KEY ([GrantedServiceAccountId]) REFERENCES [ServiceAccount] ([Id]),
    CONSTRAINT [FK_AccessPolicy_ServiceAccount_ServiceAccountId] FOREIGN KEY ([ServiceAccountId]) REFERENCES [ServiceAccount] ([Id])
);

GO
CREATE NONCLUSTERED INDEX [IX_AccessPolicy_GroupId] ON [AccessPolicy] ([GroupId]);

GO
CREATE NONCLUSTERED INDEX [IX_AccessPolicy_OrganizationUserId] ON [AccessPolicy] ([OrganizationUserId]);

GO
CREATE NONCLUSTERED INDEX [IX_AccessPolicy_GrantedProjectId] ON [AccessPolicy] ([GrantedProjectId]);

GO
CREATE NONCLUSTERED INDEX [IX_AccessPolicy_ServiceAccountId] ON [AccessPolicy] ([ServiceAccountId]);

GO
CREATE NONCLUSTERED INDEX [IX_AccessPolicy_GrantedServiceAccountId] ON [AccessPolicy] ([GrantedServiceAccountId]);
