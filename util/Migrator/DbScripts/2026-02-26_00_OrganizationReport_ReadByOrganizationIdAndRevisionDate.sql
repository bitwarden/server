CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_ReadByOrganizationIdAndRevisionDate]
    @OrganizationId UNIQUEIDENTIFIER,
    @MinRevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [OrganizationId], 
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
        AND [RevisionDate] >= @MinRevisionDate
END
