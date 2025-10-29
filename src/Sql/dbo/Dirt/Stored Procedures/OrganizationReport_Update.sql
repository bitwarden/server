CREATE PROCEDURE [dbo].[OrganizationReport_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ReportData NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @ContentEncryptionKey VARCHAR(MAX),
    @SummaryData NVARCHAR(MAX),
    @ApplicationData NVARCHAR(MAX),
    @RevisionDate DATETIME2(7),
    @ApplicationCount INT = NULL,
    @ApplicationAtRiskCount INT = NULL,
    @CriticalApplicationCount INT = NULL,
    @CriticalApplicationAtRiskCount INT = NULL,
    @MemberCount INT = NULL,
    @MemberAtRiskCount INT = NULL,
    @CriticalMemberCount INT = NULL,
    @CriticalMemberAtRiskCount INT = NULL,
    @PasswordCount INT = NULL,
    @PasswordAtRiskCount INT = NULL,
    @CriticalPasswordCount INT = NULL,
    @CriticalPasswordAtRiskCount INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[OrganizationReport]
    SET
        [OrganizationId] = @OrganizationId,
        [ReportData] = @ReportData,
        [CreationDate] = @CreationDate,
        [ContentEncryptionKey] = @ContentEncryptionKey,
        [SummaryData] = @SummaryData,
        [ApplicationData] = @ApplicationData,
        [RevisionDate] = @RevisionDate,
        [ApplicationCount] = @ApplicationCount,
        [ApplicationAtRiskCount] = @ApplicationAtRiskCount,
        [CriticalApplicationCount] = @CriticalApplicationCount,
        [CriticalApplicationAtRiskCount] = @CriticalApplicationAtRiskCount,
        [MemberCount] = @MemberCount,
        [MemberAtRiskCount] = @MemberAtRiskCount,
        [CriticalMemberCount] = @CriticalMemberCount,
        [CriticalMemberAtRiskCount] = @CriticalMemberAtRiskCount,
        [PasswordCount] = @PasswordCount,
        [PasswordAtRiskCount] = @PasswordAtRiskCount,
        [CriticalPasswordCount] = @CriticalPasswordCount,
        [CriticalPasswordAtRiskCount] = @CriticalPasswordAtRiskCount
    WHERE [Id] = @Id;
END;
