CREATE OR ALTER VIEW [dbo].[AuthRequestPendingDetailsView]
AS
    WITH
        PendingRequests
        AS
        (
            SELECT
                [AR].*,
                [D].[Id] AS [DeviceId],
                ROW_NUMBER() OVER (PARTITION BY [AR].[RequestDeviceIdentifier] ORDER BY [AR].[CreationDate] DESC) AS [rn]
            FROM [dbo].[AuthRequest] [AR]
                LEFT JOIN [dbo].[Device] [D]
                ON [AR].[RequestDeviceIdentifier] = [D].[Identifier]
                    AND [D].[UserId] = [AR].[UserId]
            WHERE [AR].[Type] IN (0, 1) -- 0 = AuthenticateAndUnlock, 1 = Unlock
        )
    SELECT
        [PR].[Id],
        [PR].[UserId],
        [PR].[OrganizationId],
        [PR].[Type],
        [PR].[RequestDeviceIdentifier],
        [PR].[RequestDeviceType],
        [PR].[RequestIpAddress],
        [PR].[RequestCountryName],
        [PR].[ResponseDeviceId],
        [PR].[AccessCode],
        [PR].[PublicKey],
        [PR].[Key],
        [PR].[MasterPasswordHash],
        [PR].[Approved],
        [PR].[CreationDate],
        [PR].[ResponseDate],
        [PR].[AuthenticationDate],
        [PR].[DeviceId]
    FROM [PendingRequests] [PR]
    WHERE [PR].[rn] = 1
        AND [PR].[Approved] IS NULL -- since we only want pending requests we only want the most recent that is also approved = null
  GO

CREATE OR ALTER PROCEDURE [dbo].[AuthRequest_ReadPendingByUserId]
    @UserId UNIQUEIDENTIFIER,
    @ExpirationMinutes INT
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[AuthRequestPendingDetailsView]
    WHERE [UserId] = @UserId
        AND [CreationDate] >= DATEADD(MINUTE, -@ExpirationMinutes, GETUTCDATE())
END
  GO
