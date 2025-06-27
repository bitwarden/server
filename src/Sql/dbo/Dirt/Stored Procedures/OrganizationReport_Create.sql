CREATE PROCEDURE [dbo].[OrganizationReport_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Date DATETIME2(7),
    @ReportData NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @ReportKey VARCHAR(MAX)
AS
    SET NOCOUNT ON;

    INSERT INTO [dbo].[OrganizationReport](
        [Id],
        [OrganizationId],
        [Date],
        [ReportData],
        [CreationDate],
        [ReportKey]
    )
    VALUES (
        @Id,
        @OrganizationId,
        @Date,
        @ReportData,
        @CreationDate,
        @ReportKey
    );
