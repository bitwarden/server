CREATE PROCEDURE [dbo].[Cipher_UpdateManyBrowserHealthExclusions]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @UserId AS UNIQUEIDENTIFIER,
    @Exclude AS BIT,
    @RiskCategory AS VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        C
    SET
        [BrowserHealthExclusions] = CASE
            WHEN @Exclude = 1 THEN JSON_MODIFY(
                COALESCE(C.[BrowserHealthExclusions], N'{}'),
                CONCAT('$."', @UserId, '"'),
                JSON_QUERY(JSON_MODIFY(N'[]', 'append $', @RiskCategory))
            )
            -- NULL removes the user's key
            ELSE JSON_MODIFY(COALESCE(C.[BrowserHealthExclusions], N'{}'), CONCAT('$."', @UserId, '"'), NULL)
        END
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
