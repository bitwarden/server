CREATE PROCEDURE [dbo].[OrganizationDomain_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Txt    VARCHAR(MAX),
    @DomainName NVARCHAR(255),
    @CreationDate   DATETIME2(7),
    @VerifiedDate   DATETIME2(7),
    @NextRunDate    DATETIME2(7),
    @JobRunCount   TINYINT
AS
BEGIN
    SET NOCOUNT ON
        
    INSERT INTO [dbo].[OrganizationDomain]
    (
        [Id],
        [OrganizationId],
        [Txt],
        [DomainName],
        [CreationDate],
        [VerifiedDate],
        [NextRunDate],
        [JobRunCount]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Txt,
        @DomainName,
        @CreationDate,
        @VerifiedDate,
        @NextRunDate,
        @JobRunCount
    )
END