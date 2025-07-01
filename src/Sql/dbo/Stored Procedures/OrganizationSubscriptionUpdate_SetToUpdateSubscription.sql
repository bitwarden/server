CREATE PROCEDURE [dbo].[OrganizationSubscriptionUpdate_SetToUpdateSubscription]
    @OrganizationId UNIQUEIDENTIFIER,
    @SeatsLastUpdated DATETIME2,
    @SyncAttempts INT
AS
BEGIN
    SET NOCOUNT ON

    IF EXISTS (SELECT 1 FROM [dbo].[OrganizationSubscriptionUpdate] WHERE [OrganizationId] = @OrganizationId)
        UPDATE
            [dbo].[OrganizationSubscriptionUpdate]
        SET
            [SeatsLastUpdated] = @SeatsLastUpdated,
            [SyncAttempts] = @SyncAttempts
        WHERE
            [OrganizationId] = @OrganizationId
    ELSE
        INSERT INTO [dbo].[OrganizationSubscriptionUpdate] (Id, OrganizationId, SeatsLastUpdated, SyncAttempts)
        VALUES (NEWID(), @OrganizationId, @SeatsLastUpdated, @SyncAttempts)
END
