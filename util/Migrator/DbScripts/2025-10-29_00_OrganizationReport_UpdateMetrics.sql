CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_UpdateMetrics]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ApplicationCount INT,
    @ApplicationAtRiskCount INT,
    @CriticalApplicationCount INT,
    @CriticalApplicationAtRiskCount INT,
    @MemberCount INT,
    @MemberAtRiskCount INT,
    @CriticalMemberCount INT,
    @CriticalMemberAtRiskCount INT,
    @PasswordCount INT,
    @PasswordAtRiskCount INT,
    @CriticalPasswordCount INT,
    @CriticalPasswordAtRiskCount INT,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.OrganizationReport 
    SET
        ApplicationCount = @ApplicationCount,
        ApplicationAtRiskCount = @ApplicationAtRiskCount,
        CriticalApplicationCount = @CriticalApplicationCount,
        CriticalApplicationAtRiskCount = @CriticalApplicationAtRiskCount,
        MemberCount = @MemberCount,
        MemberAtRiskCount = @MemberAtRiskCount,
        CriticalMemberCount = @CriticalMemberCount,
        CriticalMemberAtRiskCount = @CriticalMemberAtRiskCount,
        PasswordCount = @PasswordCount,
        PasswordAtRiskCount = @PasswordAtRiskCount,
        CriticalPasswordCount = @CriticalPasswordCount,
        CriticalPasswordAtRiskCount = @CriticalPasswordAtRiskCount,
        RevisionDate = @RevisionDate
    WHERE 
        ID = @Id
        AND OrganizationId = @OrganizationId
END
