CREATE TABLE [dbo].[OrganizationReport] (
    [Id]                       UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]           UNIQUEIDENTIFIER NOT NULL,
    [Date]                     DATETIME2 (7)    NOT NULL,
    [ReportData]               NVARCHAR(MAX)    NOT NULL,
    [CreationDate]             DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_OrganizationReport] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationReport_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
    );
GO

CREATE NONCLUSTERED INDEX [IX_OrganizationReport_OrganizationId]
    ON [dbo].[OrganizationReport]([OrganizationId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_OrganizationReport_OrganizationId_Date]
    ON [dbo].[OrganizationReport]([OrganizationId] ASC, [Date] DESC);
GO
