CREATE PROCEDURE [dbo].[OrganizationDomain_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Txt VARCHAR(MAX),
    @DomainName NVARCHAR(255),
    @CreationDate   DATETIME2(7),
    @VerifiedDate   DATETIME2(7),
    @LastCheckedDate DATETIME2(7),
    @NextRunDate    DATETIME2(7),
    @JobRunCount   TINYINT
AS
BEGIN
    SET NOCOUNT ON
    
    UPDATE
        [dbo].[OrganizationDomain]
    SET
        [OrganizationId] = @OrganizationId,
        [Txt] = @Txt,
        [DomainName] = @DomainName,
        [CreationDate] = @CreationDate,
        [VerifiedDate] = @VerifiedDate,
        [LastCheckedDate] = @LastCheckedDate,
        [NextRunDate] = @NextRunDate,
        [JobRunCount] = @JobRunCount
    WHERE
        [Id] = @Id
END
