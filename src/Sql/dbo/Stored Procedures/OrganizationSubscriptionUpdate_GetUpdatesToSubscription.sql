CREATE PROCEDURE [dbo].[OrganizationSubscriptionUpdate_GetUpdatesToSubscription]
AS
BEGIN
    SELECT *
    FROM [dbo].[OrganizationSubscriptionUpdate]
    WHERE [SeatsLastUpdated] IS NOT NULL
END
