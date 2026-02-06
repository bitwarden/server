CREATE PROCEDURE [dbo].[Organization_GetOrganizationsForSubscriptionSync]
AS
BEGIN
    SELECT *
    FROM [dbo].[OrganizationView]
    WHERE [Seats] IS NOT NULL AND [SyncSeats] = 1
END
