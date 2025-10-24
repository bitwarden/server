CREATE PROCEDURE [dbo].[OrganizationReport_Create]
   @Id UNIQUEIDENTIFIER OUTPUT,
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
    [CriticalApplicationCount],
    [ApplicationAtRiskCount],
    [CriticalApplicationAtRiskCount],
    [PasswordAtRiskCount],
    [CriticalPasswordAtRiskCount],
    [MemberAtRiskCount],
    [CriticalMemberAtRiskCount],
    [CriticalMemberCount]
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
    @CriticalApplicationCount,
    @ApplicationAtRiskCount,
    @CriticalApplicationAtRiskCount,
    @PasswordAtRiskCount,
    @CriticalPasswordAtRiskCount,
    @MemberAtRiskCount,
    @CriticalMemberAtRiskCount,
    @CriticalMemberCount
    );
END
