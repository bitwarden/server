CREATE TABLE [dbo].[Group] (
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
    [Name]           NVARCHAR (100)   NOT NULL,
    [AccessAll]      BIT              NOT NULL,
    [ExternalId]     NVARCHAR (300)   NULL,
    [CreationDate]   DATETIME         NOT NULL,
    [RevisionDate]   DATETIME         NOT NULL,
    CONSTRAINT [PK_Group] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Group_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
);

