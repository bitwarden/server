CREATE PROCEDURE [dbo].[OrganizationDomain_DeleteIfExpired]
AS
BEGIN
    SET NOCOUNT OFF
        
    DELETE FROM [dbo].[OrganizationDomain]
    WHERE [CreationDate] < DATEADD(day, -7, GETUTCDATE())
    AND [VerifiedDate] IS NULL
END