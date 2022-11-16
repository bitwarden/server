CREATE PROCEDURE [dbo].[OrganizationDomain_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @VerifiedDate   DATETIME2(7),
    @NextRunDate    DATETIME2(7),
    @NextRunCount   TINYINT
AS
BEGIN
    SET NOCOUNT ON
    
    UPDATE
        [dbo].[OrganizationDomain]
    SET
        [VerifiedDate] = @VerifiedDate,
        [NextRunDate] = @NextRunDate,
        [NextRunCount] = @NextRunCount
    WHERE
        [Id] = @Id
END