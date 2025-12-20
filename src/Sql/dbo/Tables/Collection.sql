CREATE TABLE [dbo].[Collection] (
    [Id]                            UNIQUEIDENTIFIER    NOT NULL,
    [OrganizationId]                UNIQUEIDENTIFIER    NOT NULL,
    [Name]                          VARCHAR (MAX)       NOT NULL,
    [ExternalId]                    NVARCHAR (300)      NULL,
    [CreationDate]                  DATETIME2 (7)       NOT NULL,
    [RevisionDate]                  DATETIME2 (7)       NOT NULL,
    [DefaultUserCollectionEmail]    NVARCHAR(256)       NULL,
    [Type]                          TINYINT             NOT NULL DEFAULT(0),
    [DefaultCollectionOwner]        UNIQUEIDENTIFIER    NULL,
    CONSTRAINT [PK_Collection] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Collection_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Collection_OrganizationUser] FOREIGN KEY ([DefaultCollectionOwner]) REFERENCES [dbo].[OrganizationUser] ([Id]) ON DELETE SET NULL
);
GO

CREATE NONCLUSTERED INDEX [IX_Collection_OrganizationId_IncludeAll]
    ON [dbo].[Collection]([OrganizationId] ASC)
    INCLUDE([CreationDate], [Name], [RevisionDate], [Type]);
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_Collection_DefaultCollectionOwner_OrganizationId_Type]
    ON [dbo].[Collection]([DefaultCollectionOwner], [OrganizationId], [Type])
    WHERE [Type] = 1;
GO

