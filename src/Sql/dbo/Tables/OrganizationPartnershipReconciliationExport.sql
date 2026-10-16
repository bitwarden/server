CREATE TABLE [dbo].[OrganizationPartnershipReconciliationExport]
(
    [Id]                        UNIQUEIDENTIFIER NOT NULL,
    [OrganizationPartnershipId] UNIQUEIDENTIFIER NOT NULL,
    [Status]                    TINYINT          NOT NULL,
    [RecordCount]               INT              NULL,
    [BlobName]                  NVARCHAR(256)    NULL,
    [CreationDate]              DATETIME2(7)     NOT NULL,
    [CompletionDate]            DATETIME2(7)     NULL,
    [ExpirationDate]            DATETIME2(7)     NOT NULL,
    [RevisionDate]              DATETIME2(7)     NOT NULL,
    CONSTRAINT [PK_OrganizationPartnershipReconciliationExport] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationPartnershipReconciliationExport_OrganizationPartnership] FOREIGN KEY ([OrganizationPartnershipId]) REFERENCES [dbo].[OrganizationPartnership] ([Id])
);
GO

CREATE NONCLUSTERED INDEX [IX_OrganizationPartnershipReconciliationExport_OrganizationPartnershipId]
    ON [dbo].[OrganizationPartnershipReconciliationExport]([OrganizationPartnershipId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_OrganizationPartnershipReconciliationExport_Status]
    ON [dbo].[OrganizationPartnershipReconciliationExport]([Status] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_OrganizationPartnershipReconciliationExport_ExpirationDate]
    ON [dbo].[OrganizationPartnershipReconciliationExport]([ExpirationDate] ASC);
GO
