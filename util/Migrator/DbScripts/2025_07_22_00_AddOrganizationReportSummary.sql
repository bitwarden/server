IF OBJECT_ID('dbo.OrganizationReportSummary') IS NULL
BEGIN
CREATE TABLE [dbo].[OrganizationReportSummary]
(
    [Id]                       UNIQUEIDENTIFIER   NOT NULL,
    [OrganizationReportId]     UNIQUEIDENTIFIER   NOT NULL,
    [SummaryDetails]           VARCHAR(MAX)       NOT NULL,
    [ContentEncryptionKey]     VARCHAR(MAX)       NOT NULL,
    [CreationDate]             DATETIME2 (7)      NOT NULL,
    [UpdateDate]               DATETIME2 (7)      NOT NULL,

    CONSTRAINT [PK_OrganizationReportSummary] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationReportSummary_OrganizationReport] FOREIGN KEY ([OrganizationReportId]) REFERENCES [dbo].[OrganizationReport] ([Id])
);

CREATE NONCLUSTERED INDEX [IX_OrganizationReportSummary_OrganizationReportId]
    ON [dbo].[OrganizationReportSummary]([OrganizationReportId] ASC);

END
GO

CREATE OR ALTER VIEW [dbo].[OrganizationReportSummaryView]
AS
SELECT
    *
FROM
    [dbo].[OrganizationReportSummary];
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationReportSummary_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationReportId UNIQUEIDENTIFIER,
    @SummaryDetails VARCHAR(MAX),
    @ContentEncryptionKey VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @UpdateDate DATETIME2(7)
AS
    SET NOCOUNT ON;
INSERT INTO [dbo].[OrganizationReportSummary] (
    [Id],
    [OrganizationReportId],
    [SummaryDetails],
    [ContentEncryptionKey],
    [CreationDate],
[UpdateDate]
)
VALUES (
    @Id,
    @OrganizationReportId,
    @SummaryDetails,
    @ContentEncryptionKey,
    @CreationDate,
    @UpdateDate
    );

GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationReportSummary_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

DELETE FROM [dbo].[OrganizationReportSummary]
WHERE [Id] = @Id;


GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationReportSummary_ReadById]
    @Id UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;
SELECT
    *
FROM [dbo].[OrganizationReportSummary]
WHERE [Id] = @Id;

GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationReportSummary_ReadByOrganizationReportId]
    @OrganizationReportId UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;
SELECT
    *
FROM [dbo].[OrganizationReportSummary]
WHERE [OrganizationReportId] = @OrganizationReportId;

GO

CREATE PROCEDURE [dbo].[OrganizationReportSummary_Update]
    @Id                   UNIQUEIDENTIFIER,
    @OrganizationReportId UNIQUEIDENTIFIER,
    @SummaryDetails       VARCHAR(MAX),
    @ContentEncryptionKey VARCHAR(MAX),
    @UpdateDate           DATETIME2(7)
AS
    SET NOCOUNT ON;
UPDATE [dbo].[OrganizationReportSummary]
SET
    [OrganizationReportId] = @OrganizationReportId,
    [SummaryDetails] = @SummaryDetails,
    [ContentEncryptionKey] = @ContentEncryptionKey,
    [UpdateDate] = @UpdateDate
WHERE [Id] = @Id;

GO
