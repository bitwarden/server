IF OBJECT_ID('[dbo].[ServiceAccount]') IS NULL
BEGIN
CREATE TABLE [dbo].[ServiceAccount]
(
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
    [Name] NVARCHAR(MAX) NULL,
    [CreationDate] DATETIME2(7) NOT NULL,
    [RevisionDate] DATETIME2(7) NOT NULL,
    CONSTRAINT [PK_ServiceAccount] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ServiceAccount_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization]([Id]) ON DELETE CASCADE
);

CREATE NONCLUSTERED INDEX [IX_ServiceAccount_OrganizationId] ON [dbo].[ServiceAccount] ([OrganizationId] ASC);
END
GO
