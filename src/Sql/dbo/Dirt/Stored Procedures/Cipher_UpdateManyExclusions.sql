CREATE PROCEDURE [dbo].[Cipher_UpdateManyExclusions]
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
        [Exclusions] = CASE
            -- First exclusion for this user: start the user's array
            WHEN @Exclude = 1 AND JSON_QUERY(C.[Exclusions], CONCAT('$."', @UserId, '"')) IS NULL THEN JSON_MODIFY(
                COALESCE(C.[Exclusions], N'{}'),
                CONCAT('$."', @UserId, '"'),
                JSON_QUERY(JSON_MODIFY(N'[]', 'append $', @RiskCategory))
            )
            -- Category already excluded: keep the value
            WHEN @Exclude = 1 AND EXISTS (
                SELECT 1
                FROM OPENJSON(C.[Exclusions], CONCAT('$."', @UserId, '"'))
                WHERE [value] COLLATE Latin1_General_BIN2 = @RiskCategory -- exact match, like EF
            ) THEN C.[Exclusions]
            WHEN @Exclude = 1 THEN JSON_MODIFY(
                C.[Exclusions],
                CONCAT('append $."', @UserId, '"'),
                @RiskCategory
            )
            -- NULL removes the user's key
            ELSE JSON_MODIFY(COALESCE(C.[Exclusions], N'{}'), CONCAT('$."', @UserId, '"'), NULL)
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
