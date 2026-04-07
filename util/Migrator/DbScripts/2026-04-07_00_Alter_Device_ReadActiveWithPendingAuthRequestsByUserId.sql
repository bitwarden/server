-- PM-34130: Replace SELECT D.* with an explicit column list.
-- Previously, Dapper mapped results by column position into a 14-parameter constructor.
-- A column addition, removal, or reorder in DeviceView would silently assign wrong values
-- with no compile or runtime error. Explicit columns enable name-based mapping via property
-- setters, eliminating the positional dependency and restoring EDD backwards compatibility.
CREATE OR ALTER PROCEDURE [dbo].[Device_ReadActiveWithPendingAuthRequestsByUserId]
    @UserId UNIQUEIDENTIFIER,
    @ExpirationMinutes INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        D.[Id],
        D.[UserId],
        D.[Name],
        D.[Type],
        D.[Identifier],
        D.[PushToken],
        D.[CreationDate],
        D.[RevisionDate],
        D.[EncryptedUserKey],
        D.[EncryptedPublicKey],
        D.[EncryptedPrivateKey],
        D.[Active],
        AR.[Id] AS [AuthRequestId],
        AR.[CreationDate] AS [AuthRequestCreationDate]
    FROM [dbo].[DeviceView] D
    LEFT OUTER JOIN (
        SELECT
            [Id],
            [CreationDate],
            [RequestDeviceIdentifier],
            [Approved],
            ROW_NUMBER() OVER (PARTITION BY [RequestDeviceIdentifier] ORDER BY [CreationDate] DESC) AS rn
        FROM [dbo].[AuthRequestView]
        WHERE [Type] IN (0,1)  -- AuthenticateAndUnlock and Unlock types only
            AND [CreationDate] >= DATEADD(MINUTE, -@ExpirationMinutes, GETUTCDATE()) -- Ensure the request hasn't expired
            AND [UserId] = @UserId -- Requests for this user only
    ) AR -- This join will get the most recent request per device, regardless of approval status
    ON D.[Identifier] = AR.[RequestDeviceIdentifier] AND AR.[rn] = 1 AND AR.[Approved] IS NULL -- Get only the most recent unapproved request per device
    WHERE
        D.[UserId] = @UserId -- Include only devices for this user
        AND D.[Active] = 1; -- Include only active devices
END;
GO
