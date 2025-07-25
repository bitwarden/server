CREATE PROCEDURE [dbo].[OrganizationReportSummary_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationReportId UNIQUEIDENTIFIER,
    @SummaryDetails VARCHAR(MAX),
    @ContentEncryptionKey VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @UpdateDate DATETIME2(7)
AS
    SET NOCOUNT ON;
    INSERT INTO [dbo].[OrganizationReportSummary] (
          [Id],
          [OrganizationReportId],
          [SummaryDetails],
          [ContentEncryptionKey],
          [CreationDate],
          [UpdateDate]
    )
    VALUES (
          @Id,
          @OrganizationReportId,
          @SummaryDetails,
          @ContentEncryptionKey,
          @CreationDate,
          @UpdateDate
    );

