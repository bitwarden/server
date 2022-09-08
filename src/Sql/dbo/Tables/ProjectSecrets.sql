CREATE TABLE [dbo].[ProjectSecrets] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]    UNIQUEIDENTIFIER NOT NULL,
    [SecretId]          INT NOT NULL,
    [ProjectId]         INT NOT NULL,
    [CreationDate]      DATETIME2 (7),
    [RevisionDate]      DATETIME2 (7), 
    [DeletedDate]       DATETIME2 (7) NULL,
    CONSTRAINT [PK_ProjectSecrets] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Project_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE,
);

GO
CREATE NONCLUSTERED INDEX [IX_Project_OrganizationId] ON [dbo].[ProjectSecrets] ([OrganizationId] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_Project_DeletedDate] ON [dbo].[ProjectSecrets] ([DeletedDate] ASC);
