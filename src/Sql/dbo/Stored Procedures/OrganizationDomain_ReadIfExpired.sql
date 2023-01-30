CREATE PROCEDURE [dbo].[OrganizationDomain_ReadIfExpired]
AS
BEGIN
    SET NOCOUNT OFF
        
    SELECT 
        *
    FROM
        [dbo].[OrganizationDomain]
    WHERE
        DATEDIFF(DAY, [CreationDate], GETUTCDATE()) >= 4 --Get domains that have not been verified after 3 days (72 hours)
    AND
        [VerifiedDate] IS NULL