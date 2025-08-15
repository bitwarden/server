CREATE PROCEDURE [dbo].[OrganizationReport_UpdateSummaryData]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @SummaryData NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[OrganizationReport]
    SET
        [SummaryData] = @SummaryData,
        [RevisionDate] = GETUTCDATE()
    WHERE [Id] = @Id
      AND [OrganizationId] = @OrganizationId;
END