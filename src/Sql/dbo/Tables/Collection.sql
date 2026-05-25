CREATE TABLE [dbo].[Collection] (
    [Id]                            UNIQUEIDENTIFIER    NOT NULL,
    [OrganizationId]                UNIQUEIDENTIFIER    NOT NULL,
    [Name]                          VARCHAR (MAX)       NOT NULL,
    [ExternalId]                    NVARCHAR (300)      NULL,
    [CreationDate]                  DATETIME2 (7)       NOT NULL,
    [RevisionDate]                  DATETIME2 (7)       NOT NULL,
    [DefaultUserCollectionEmail]    NVARCHAR(256)       NULL,
    [Type]                          TINYINT             NOT NULL DEFAULT(0),
    [LeasingPolicyId]               UNIQUEIDENTIFIER    NULL,
    CONSTRAINT [PK_Collection] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Collection_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Collection_LeasingPolicy] FOREIGN KEY ([LeasingPolicyId]) REFERENCES [dbo].[LeasingPolicy] ([Id]) ON DELETE SET NULL
);
GO

CREATE NONCLUSTERED INDEX [IX_Collection_OrganizationId_IncludeAll]
    ON [dbo].[Collection]([OrganizationId] ASC)
    INCLUDE([CreationDate], [Name], [RevisionDate], [Type]);
GO

CREATE NONCLUSTERED INDEX [IX_Collection_LeasingPolicyId]
    ON [dbo].[Collection]([LeasingPolicyId] ASC);
GO

