CREATE TABLE [dbo].[RiskInsightCriticalApplication] (
    [Id]                       UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]           UNIQUEIDENTIFIER NOT NULL,
    [Applications]             NVARCHAR(MAX)    NOT NULL,
    [CreationDate]             DATETIME2 (7)    NOT NULL,
    [RevisionDate]             DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_RiskInsightCriticalApplication] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_RiskInsightCriticalApplication_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
    );

CREATE NONCLUSTERED INDEX [IX_RiskInsightCriticalApplication_OrganizationId]
    ON [dbo].[RiskInsightCriticalApplication]([OrganizationId] ASC);
