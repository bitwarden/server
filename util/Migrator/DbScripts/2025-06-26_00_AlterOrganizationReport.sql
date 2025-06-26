ALTER TABLE
    [dbo].[OrganizationReport]
ADD
    [ReportKey] NVARCHAR(MAX) NULL;
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
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[OrganizationReport]
        (
        [Id],
        [OrganizationId],
        [Date],
        [ReportKey],
        [ReportData],
        [CreationDate]
        )
    VALUES
        (
            @Id,
            @OrganizationId,
            @Date,
            @ReportKey,
            @ReportData,
            @CreationDate
        );
END
GO
