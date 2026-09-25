CREATE PROCEDURE [dbo].[TwoFactorRememberToken_DeleteExpired]
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Deleted in batches so each statement holds few enough locks to avoid escalating to a table
    -- lock, which would block logins while the sweep runs. The loop ends when a batch deletes
    -- nothing.
    DECLARE @BatchSize INT = 100

    WHILE @BatchSize > 0
    BEGIN
        DELETE TOP(@BatchSize)
        FROM
            [dbo].[TwoFactorRememberToken]
        WHERE
            [ExpirationDate] < @Now

        SET @BatchSize = @@ROWCOUNT
    END
END
