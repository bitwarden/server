CREATE PROCEDURE [dbo].[OrganizationSubscriptionUpdate_UpdateSubscriptionStatus]
    @SuccessfulOrganizations NVARCHAR(MAX),
    @FailedOrganizations NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @SuccessfulOrgIds TABLE (Id UNIQUEIDENTIFIER)
    DECLARE @FailedOrgIds TABLE (Id UNIQUEIDENTIFIER)

    INSERT INTO @SuccessfulOrgIds (Id)
    SELECT [value] FROM OPENJSON(@SuccessfulOrganizations)

    INSERT INTO @FailedOrgIds (Id)
    SELECT [value] FROM OPENJSON(@FailedOrganizations)

    UPDATE [dbo].[OrganizationSubscriptionUpdate]
    SET
        [SeatsLastUpdated] = NULL,
        [SyncAttempts] = 0
    WHERE [OrganizationId] IN (SELECT Id FROM @SuccessfulOrgIds)

    UPDATE [dbo].[OrganizationSubscriptionUpdate]
    SET
        [SyncAttempts] = [SyncAttempts] + 1
    WHERE [OrganizationId] IN (SELECT Id FROM @FailedOrgIds)
END
