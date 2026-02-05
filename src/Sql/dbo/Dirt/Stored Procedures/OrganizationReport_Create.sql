CREATE PROCEDURE [dbo].[OrganizationReport_Create]
   @Id UNIQUEIDENTIFIER OUTPUT,
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


INSERT INTO [dbo].[OrganizationReport](
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
)
VALUES (
    @Id,
    @OrganizationId,
    @ReportData,
    @CreationDate,
    @ContentEncryptionKey,
    @SummaryData,
    @ApplicationData,
    @RevisionDate,
    @ApplicationCount,
    @ApplicationAtRiskCount,
    @CriticalApplicationCount,
    @CriticalApplicationAtRiskCount,
    @MemberCount,
    @MemberAtRiskCount,
    @CriticalMemberCount,
    @CriticalMemberAtRiskCount,
    @PasswordCount,
    @PasswordAtRiskCount,
    @CriticalPasswordCount,
    @CriticalPasswordAtRiskCount
    );
END
