CREATE TABLE [dbo].[SsoConfig] (
    [Id]                 BIGINT              IDENTITY (1, 1) NOT NULL,
    [Enabled]            BIT                 NOT NULL,
    [OrganizationId]     UNIQUEIDENTIFIER    NOT NULL,
    [Data]               NVARCHAR (MAX)      NULL,
    [CreationDate]       DATETIME2 (7)       NOT NULL,
    [RevisionDate]       DATETIME2 (7)       NOT NULL,
    CONSTRAINT [PK_SsoConfig] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_SsoConfig_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
);
