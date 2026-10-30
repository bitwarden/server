CREATE PROCEDURE [dbo].[Cipher_UpdateManyBrowserHealthExclusions]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @UserId AS UNIQUEIDENTIFIER,
    @Exclude AS BIT
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        C
    SET
        [BrowserHealthExclusions] = JSON_MODIFY(
            COALESCE(C.[BrowserHealthExclusions], N'{}'),
            CONCAT('$."', @UserId, '"'),
            IIF(@Exclude = 1, CAST(1 AS BIT), NULL) -- NULL removes the user's key
        )
    FROM
        [dbo].[Cipher] C
        INNER JOIN @Ids I ON I.[Id] = C.[Id]
    WHERE
        C.[UserId] = @UserId
        AND C.[OrganizationId] IS NULL
        AND C.[Type] = 1 -- Login
        AND C.[DeletedDate] IS NULL

    DECLARE @Count INT = @@ROWCOUNT

    IF @Count > 0
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END

    SELECT @Count
END
