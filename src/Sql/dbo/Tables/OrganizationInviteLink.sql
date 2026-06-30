CREATE TABLE [dbo].[OrganizationInviteLink]
(
    [Id]                 UNIQUEIDENTIFIER NOT NULL,
    [Code]               UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]     UNIQUEIDENTIFIER NOT NULL,
    [AllowedDomains]     NVARCHAR(MAX)    NOT NULL,
    [EncryptedInviteKey] NVARCHAR(MAX)    NOT NULL,
    [EncryptedOrgKey]    NVARCHAR(MAX)    NULL,
    [CreationDate]       DATETIME2(7)     NOT NULL,
    [RevisionDate]       DATETIME2(7)     NOT NULL,
    CONSTRAINT [PK_OrganizationInviteLink] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationInviteLink_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_OrganizationInviteLink_OrganizationId]
    ON [dbo].[OrganizationInviteLink]([OrganizationId] ASC);
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_OrganizationInviteLink_Code]
    ON [dbo].[OrganizationInviteLink]([Code] ASC);
GO
