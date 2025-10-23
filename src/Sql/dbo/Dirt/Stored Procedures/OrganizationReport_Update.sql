CREATE PROCEDURE [dbo].[OrganizationReport_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ReportData NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @ContentEncryptionKey VARCHAR(MAX),
    @SummaryData NVARCHAR(MAX),
    @ApplicationData NVARCHAR(MAX),
    @RevisionDate DATETIME2(7),
    @ApplicationCount INT,
    @CriticalApplicationCount INT,
    @ApplicationAtRiskCount INT,
    @CriticalApplicationAtRiskCount INT,
    @PasswordAtRiskCount INT,
    @CriticalPasswordAtRiskCount INT,
    @MemberAtRiskCount INT,
    @CriticalMemberAtRiskCount INT,
    @CriticalMemberCount INT
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
        [CriticalApplicationCount] = @CriticalApplicationCount,
        [ApplicationAtRiskCount] = @ApplicationAtRiskCount,
        [CriticalApplicationAtRiskCount] = @CriticalApplicationAtRiskCount,
        [PasswordAtRiskCount] = @PasswordAtRiskCount,
        [CriticalPasswordAtRiskCount] = @CriticalPasswordAtRiskCount,
        [MemberAtRiskCount] = @MemberAtRiskCount,
        [CriticalMemberAtRiskCount] = @CriticalMemberAtRiskCount,
        [CriticalMemberCount] = @CriticalMemberCount
    WHERE [Id] = @Id;
END;
