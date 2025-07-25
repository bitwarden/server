CREATE PROCEDURE [dbo].[Organization_UpdateSubscriptionStatus]
    @SuccessfulOrganizations NVARCHAR(MAX),
    @SyncDate DATETIME2
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @SuccessfulOrgIds TABLE (Id UNIQUEIDENTIFIER)

    INSERT INTO @SuccessfulOrgIds (Id)
    SELECT [value]
    FROM OPENJSON(@SuccessfulOrganizations)

    UPDATE o
    SET
        [SyncSeats] = 0,
        [RevisionDate] = @SyncDate
    FROM [dbo].[Organization] o
    INNER JOIN @SuccessfulOrgIds success on success.Id = o.Id
END
