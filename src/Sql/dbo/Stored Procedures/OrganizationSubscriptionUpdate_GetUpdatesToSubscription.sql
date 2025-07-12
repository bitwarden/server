CREATE PROCEDURE [dbo].[OrganizationSubscriptionUpdate_GetUpdatesToSubscription]
AS
BEGIN
    SELECT *
    FROM [dbo].[OrganizationSubscriptionUpdateView]
    WHERE [SeatsLastUpdated] IS NOT NULL
END
