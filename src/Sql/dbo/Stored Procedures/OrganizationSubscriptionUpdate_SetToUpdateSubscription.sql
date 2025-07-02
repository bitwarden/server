CREATE PROCEDURE [dbo].[OrganizationSubscriptionUpdate_SetToUpdateSubscription]
    @OrganizationId UNIQUEIDENTIFIER,
    @SeatsLastUpdated DATETIME2
AS
BEGIN
    SET NOCOUNT ON

    IF EXISTS (SELECT 1 FROM [dbo].[OrganizationSubscriptionUpdate] WHERE [OrganizationId] = @OrganizationId)
        UPDATE
            [dbo].[OrganizationSubscriptionUpdate]
        SET
            [SeatsLastUpdated] = @SeatsLastUpdated,
            [SyncAttempts] = 1
        WHERE
            [OrganizationId] = @OrganizationId
    ELSE
        INSERT INTO [dbo].[OrganizationSubscriptionUpdate] (Id, OrganizationId, SeatsLastUpdated, SyncAttempts)
        VALUES (NEWID(), @OrganizationId, @SeatsLastUpdated, 1)
END
