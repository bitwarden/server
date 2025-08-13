CREATE PROCEDURE [dbo].[OrganizationReport_GetSummaryData]
    @OrganizationId UNIQUEIDENTIFIER,
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Id],
        [OrganizationId], [
        SummaryData]
    FROM [dbo].[OrganizationReport]
    WHERE [OrganizationId] = @OrganizationId AND [Id] = @Id
END


