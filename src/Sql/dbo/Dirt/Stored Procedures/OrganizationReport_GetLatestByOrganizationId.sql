CREATE PROCEDURE [dbo].[OrganizationReport_GetLatestByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        [Id],
        [OrganizationId],
        [ReportData],
        [CreationDate],
        [ContentEncryptionKey],
        [SummaryData],
        [ApplicationData],
        [RevisionDate],
        [ApplicationCount],
        [ApplicationAtRiskCount],
        [CriticalApplicationCount],
        [CriticalApplicationAtRiskCount],
        [MemberCount],
        [MemberAtRiskCount],
        [CriticalMemberCount],
        [CriticalMemberAtRiskCount],
        [PasswordCount],
        [PasswordAtRiskCount],
        [CriticalPasswordCount],
        [CriticalPasswordAtRiskCount]
    FROM [dbo].[OrganizationReportView]
    WHERE [OrganizationId] = @OrganizationId
    ORDER BY [RevisionDate] DESC
END
