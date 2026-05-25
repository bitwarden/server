CREATE TABLE [dbo].[LeasingPolicy] (
    [Id]                UNIQUEIDENTIFIER    NOT NULL,
    [OrganizationId]    UNIQUEIDENTIFIER    NOT NULL,
    [Name]              NVARCHAR(256)       NOT NULL,
    [Description]       NVARCHAR(MAX)       NULL,
    [Policy]            NVARCHAR(MAX)       NOT NULL,
    [CreationDate]      DATETIME2(7)        NOT NULL,
    [RevisionDate]      DATETIME2(7)        NOT NULL,
    CONSTRAINT [PK_LeasingPolicy] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_LeasingPolicy_Organization] FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_LeasingPolicy_OrganizationId_Name]
    ON [dbo].[LeasingPolicy] ([OrganizationId] ASC, [Name] ASC);
GO
