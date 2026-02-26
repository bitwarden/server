CREATE VIEW [dbo].[OrganizationReportMetricsView] AS
    SELECT OrganizationId, 
        RevisionDate, 
        ApplicationCount, 
        ApplicationAtRiskCount, 
        CriticalApplicationCount, 
        CriticalApplicationAtRiskCount,
        MemberCount, 
        MemberAtRiskCount,
        CriticalMemberCount, 
        CriticalMemberAtRiskCount,
        PasswordCount, 
        PasswordAtRiskCount, 
        CriticalPasswordCount, 
        CriticalPasswordAtRiskCount
    FROM dbo.OrganizationReport
;