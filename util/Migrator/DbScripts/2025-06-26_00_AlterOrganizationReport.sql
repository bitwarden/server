IF COL_LENGTH('[dbo].[OrganizationReport]', 'ContentEncryptionKey') IS NULL
BEGIN
    ALTER TABLE [dbo].[OrganizationReport]
    ADD [ContentEncryptionKey] VARCHAR(MAX) NOT NULL;
END
GO

CREATE OR ALTER VIEW [dbo].[OrganizationReportView]
AS
SELECT
    *
FROM
    [dbo].[OrganizationReport]
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Date DATETIME2(7),
    @ReportData NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @ContentEncryptionKey VARCHAR(MAX)
AS
    SET NOCOUNT ON;

    INSERT INTO [dbo].[OrganizationReport]
    (
        [Id],
        [OrganizationId],
        [Date],
        [ReportData],
        [CreationDate],
        [ContentEncryptionKey]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Date,
        @ReportData,
        @CreationDate,
        @ContentEncryptionKey
    );
GO
