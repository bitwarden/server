CREATE PROCEDURE [dbo].[Cipher_ExcludeFromReports]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UtcNow DATETIME2(7) = SYSUTCDATETIME();

    UPDATE
        C
    SET
        [ReportExclusions] = JSON_MODIFY(
            COALESCE(C.[ReportExclusions], N'{}'),
            CONCAT('$."', @UserId, '"'),
            CONVERT(NVARCHAR(30), @UtcNow, 127)
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
