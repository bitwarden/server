CREATE TABLE [dbo].[Collection] (
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
    [Name]           VARCHAR (MAX)    NOT NULL,
    [ExternalId]     NVARCHAR (300)   NULL,
    [CreationDate]   DATETIME2 (7)    NOT NULL,
    [RevisionDate]   DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Collection] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Collection_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_Collection_OrganizationId_IncludeAll]
    ON [dbo].[Collection]([OrganizationId] ASC)
    INCLUDE([CreationDate], [Name], [RevisionDate]);
GO

