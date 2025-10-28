CREATE TABLE [dbo].[OrganizationReport] (
    [Id]                                UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]                    UNIQUEIDENTIFIER NOT NULL,
    [ReportData]                        NVARCHAR(MAX)    NOT NULL,
    [CreationDate]                      DATETIME2 (7)    NOT NULL,
    [ContentEncryptionKey]              VARCHAR(MAX)     NOT NULL,
    [SummaryData]                       NVARCHAR(MAX)    NULL,
    [ApplicationData]                   NVARCHAR(MAX)    NULL,
    [RevisionDate]                      DATETIME2 (7)    NULL,
    [ApplicationCount]                  INT              NULL,
    [ApplicationAtRiskCount]            INT              NULL,
    [CriticalApplicationCount]          INT              NULL,
    [CriticalApplicationAtRiskCount]    INT              NULL,
    [MemberCount]                       INT              NULL,
    [MemberAtRiskCount]                 INT              NULL,
    [CriticalMemberCount]               INT              NULL,
    [CriticalMemberAtRiskCount]         INT              NULL,
    [PasswordCount]                     INT              NULL,
    [PasswordAtRiskCount]               INT              NULL,
    [CriticalPasswordCount]             INT              NULL,
    [CriticalPasswordAtRiskCount]       INT              NULL,
    CONSTRAINT [PK_OrganizationReport] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationReport_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
    );
GO


CREATE NONCLUSTERED INDEX [IX_OrganizationReport_OrganizationId]
   ON [dbo].[OrganizationReport] ([OrganizationId] ASC);
GO


CREATE NONCLUSTERED INDEX [IX_OrganizationReport_OrganizationId_RevisionDate]
   ON [dbo].[OrganizationReport]([OrganizationId] ASC, [RevisionDate] DESC);
GO

