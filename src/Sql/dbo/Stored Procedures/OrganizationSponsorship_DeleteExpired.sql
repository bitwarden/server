CREATE PROCEDURE [dbo].[OrganizationSponsorship_DeleteExpired]
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @BatchSize INT = 100
    DECLARE @Now DATETIME2(7) = GETUTCDATE()

    WHILE @BatchSize > 0
    BEGIN
        DELETE TOP(@BatchSize)
        FROM
            [dbo].[OrganizationSponsorship]
        WHERE
            [ValidUntil] < @Now

        SET @BatchSize = @@ROWCOUNT
    END
END