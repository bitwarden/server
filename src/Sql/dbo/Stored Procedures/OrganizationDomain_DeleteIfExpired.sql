CREATE PROCEDURE [dbo].[OrganizationDomain_DeleteIfExpired]
    @ExpirationPeriod TINYINT
AS
BEGIN
    SET NOCOUNT OFF
        
    DELETE FROM [dbo].[OrganizationDomain]
    WHERE DATEDIFF(DAY, [LastCheckedDate], GETUTCDATE()) >= @ExpirationPeriod
    AND [VerifiedDate] IS NULL
END