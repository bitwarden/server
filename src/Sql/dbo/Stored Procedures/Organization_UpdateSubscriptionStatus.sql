CREATE PROCEDURE [dbo].[Organization_UpdateSubscriptionStatus]
    @SuccessfulOrganizations AS [dbo].[GuidIdArray] READONLY,
    @SyncDate DATETIME2
AS
BEGIN
    SET NOCOUNT ON

    UPDATE o
    SET
        [SyncSeats] = 0,
        [RevisionDate] = @SyncDate
    FROM [dbo].[Organization] o
    INNER JOIN @SuccessfulOrganizations success on success.Id = o.Id
END
