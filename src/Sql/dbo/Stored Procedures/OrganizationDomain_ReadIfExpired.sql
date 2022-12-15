CREATE PROCEDURE [dbo].[OrganizationDomain_ReadIfExpired]
AS
BEGIN
    SET NOCOUNT OFF
        
    SELECT 
        *
    FROM
        [dbo].[OrganizationDomain]
    WHERE
        [CreationDate] < DATEADD(hour, -72, GETUTCDATE()) --Using 72 hours to determine expired period
    AND
        [VerifiedDate] IS NULL