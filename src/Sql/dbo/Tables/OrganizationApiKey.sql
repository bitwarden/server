CREATE TABLE [dbo].[OrganizationApiKey] (
    [Id]                UNIQUEIDENTIFIER,
    [OrganizationId]    UNIQUEIDENTIFIER NOT NULL,
    [Type]              TINYINT NOT NULL,
    [ApiKey]            VARCHAR(30) NOT NULL,
    [RevisionDate]      DATETIME2(7) NOT NULL
    CONSTRAINT [PK_OrganizationApiKey] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationApiKey_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
);

GO

CREATE NONCLUSTERED INDEX [IX_OrganizationApiKey_OrganizationId]
    ON [dbo].[OrganizationApiKey]([OrganizationId] ASC);
