CREATE TABLE [dbo].[OrganizationApplication] (
    [Id]                       UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]           UNIQUEIDENTIFIER NOT NULL,
    [Applications]             NVARCHAR(MAX)    NOT NULL,
    [CreationDate]             DATETIME2 (7)    NOT NULL,
    [RevisionDate]             DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_OrganizationApplication] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationApplication_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
    );

CREATE NONCLUSTERED INDEX [IX_OrganizationApplication_OrganizationId]
    ON [dbo].[OrganizationApplication]([OrganizationId] ASC);
