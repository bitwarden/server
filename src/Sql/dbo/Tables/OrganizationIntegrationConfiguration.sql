CREATE TABLE [dbo].[OrganizationIntegrationConfiguration]
(
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [OrganizationIntegrationId] UNIQUEIDENTIFIER NOT NULL,
    [EventType] SMALLINT NOT NULL,
    [Configuration] VARCHAR (MAX) NULL,
    [Template] VARCHAR (MAX) NULL,
    [CreationDate] DATETIME2 (7) NOT NULL,
    [RevisionDate] DATETIME2 (7) NOT NULL,
    [Filters] VARCHAR (MAX) NULL,
    CONSTRAINT [PK_OrganizationIntegrationConfiguration] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationIntegrationConfiguration_OrganizationIntegration] FOREIGN KEY ([OrganizationIntegrationId]) REFERENCES [dbo].[OrganizationIntegration] ([Id]) ON DELETE CASCADE
);
GO
