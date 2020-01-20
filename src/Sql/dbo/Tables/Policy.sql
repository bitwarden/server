CREATE TABLE [dbo].[Policy] (
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
    [Type]           TINYINT          NOT NULL,
    [Data]           NVARCHAR (MAX)   NULL,
    [Enabled]        BIT              NOT NULL,
    [CreationDate]   DATETIME2 (7)    NOT NULL,
    [RevisionDate]   DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Policy] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Policy_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_Policy_OrganizationId_Type]
    ON [dbo].[Policy]([OrganizationId] ASC, [Type] ASC);

