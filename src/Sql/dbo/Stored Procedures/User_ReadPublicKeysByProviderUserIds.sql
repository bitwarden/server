CREATE PROCEDURE [dbo].[User_ReadPublicKeysByProviderUserIds]
    @ProviderId UNIQUEIDENTIFIER,
    @ProviderUserIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        PU.[Id],
        PU.[UserId],
        U.[PublicKey]
    FROM
        @ProviderUserIds PUIDs
    INNER JOIN
        [dbo].[ProviderUser] PU ON PUIDs.Id = PU.Id AND PU.[Status] = 1 -- Accepted
    INNER JOIN
        [dbo].[User] U ON PU.UserId = U.Id
    WHERE
        PU.ProviderId = @ProviderId
END
