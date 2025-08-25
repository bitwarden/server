CREATE PROCEDURE [dbo].[OrganizationReport_UpdateSummaryData]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @SummaryData NVARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[OrganizationReport]
    SET
        [SummaryData] = @SummaryData,
        [RevisionDate] = @RevisionDate
    WHERE [Id] = @Id
      AND [OrganizationId] = @OrganizationId;
END
