CREATE TABLE [dbo].[OrganizationReportSummary] (
    [Id]                       UNIQUEIDENTIFIER   NOT NULL,
    [OrganizationReportId]     UNIQUEIDENTIFIER   NOT NULL,
    [SummaryDetails]           VARCHAR(MAX)       NOT NULL,
    [ContentEncryptionKey]     VARCHAR(MAX)       NOT NULL,
    [CreationDate]             DATETIME2 (7)      NOT NULL,
    [UpdateDate]               DATETIME2 (7)      NOT NULL,

    CONSTRAINT [PK_OrganizationReportSummary] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationReportSummary_OrganizationReport] FOREIGN KEY ([OrganizationReportId]) REFERENCES [dbo].[OrganizationReport] ([Id])
    );
GO

CREATE NONCLUSTERED INDEX [IX_OrganizationReportSummary_OrganizationReportId]
    ON [dbo].[OrganizationReportSummary]([OrganizationReportId] ASC);
GO


