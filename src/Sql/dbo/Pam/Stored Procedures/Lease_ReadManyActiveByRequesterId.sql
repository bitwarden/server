CREATE PROCEDURE [dbo].[Lease_ReadManyActiveByRequesterId]
    @RequesterId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[Lease]
    WHERE
        [RequesterId] = @RequesterId
        AND [Status] = 0 -- Active
        AND [NotBefore] <= @Now
        AND [NotAfter] > @Now
    ORDER BY
        [NotAfter] ASC
END
