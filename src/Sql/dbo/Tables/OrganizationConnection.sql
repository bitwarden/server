CREATE TABLE [dbo].[OrganizationConnection] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]    UNIQUEIDENTIFIER NOT NULL,
    [Type]              TINYINT NOT NULL,
    [Enabled]           BIT NOT NULL,
    [Config]            NVARCHAR (MAX) NULL,
    CONSTRAINT [PK_OrganizationConnection] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationConnection_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
);
GO

CREATE NONCLUSTERED INDEX [IX_OrganizationConnection_OrganizationId]
    ON [dbo].[OrganizationConnection]([OrganizationId] ASC);
