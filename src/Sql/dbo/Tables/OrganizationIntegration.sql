CREATE TABLE [dbo].[OrganizationIntegration]
(
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
    [Type] SMALLINT NOT NULL,
    [Configuration] VARCHAR (MAX) NULL,
    [CreationDate] DATETIME2 (7) NOT NULL,
    [RevisionDate] DATETIME2 (7) NOT NULL,
    CONSTRAINT [PK_OrganizationIntegration] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationIntegration_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
);
GO

CREATE NONCLUSTERED INDEX [IX_OrganizationIntegration_OrganizationId]
    ON [dbo].[OrganizationIntegration]([OrganizationId] ASC);
GO

CREATE UNIQUE INDEX [IX_OrganizationIntegration_Organization_Type]
    ON [dbo].[OrganizationIntegration]([OrganizationId], [Type]);
GO
