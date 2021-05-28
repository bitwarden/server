CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByProviderUserIds]
    @ProviderUserIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        U
    SET
        U.[AccountRevisionDate] = GETUTCDATE()
    FROM
        @ProviderUserIds OUIDs
    INNER JOIN
        [dbo].[ProviderUser] PU ON OUIDs.Id = PU.Id AND PU.[Status] = 2 -- Confirmed
    INNER JOIN
        [dbo].[User] U ON PU.UserId = U.Id
END
GO
