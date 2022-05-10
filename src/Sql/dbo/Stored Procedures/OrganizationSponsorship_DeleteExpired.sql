CREATE PROCEDURE [dbo].[OrganizationSponsorship_DeleteExpired]
    @ValidUntilBeforeDate DATETIME2 (7)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @BatchSize INT = 100

    WHILE @BatchSize > 0
    BEGIN
        DELETE TOP(@BatchSize)
        FROM
            [dbo].[OrganizationSponsorship]
        WHERE
            [ValidUntil] < @ValidUntilBeforeDate

        SET @BatchSize = @@ROWCOUNT
    END
END