CREATE TABLE [dbo].[RiskInsightReport] (
    [Id]                       UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]           UNIQUEIDENTIFIER NOT NULL,
    [Date]                     DATETIME2 (7)    NOT NULL,
    [ReportData]               NVARCHAR(MAX)    NOT NULL,
    [CreationDate]             DATETIME2 (7)    NOT NULL,
    [RevisionDate]             DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_RiskInsightReport] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_RiskInsightReport_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
    );

CREATE NONCLUSTERED INDEX [IX_RiskInsightReport_OrganizationId]
    ON [dbo].[RiskInsightReport]([OrganizationId] ASC);
