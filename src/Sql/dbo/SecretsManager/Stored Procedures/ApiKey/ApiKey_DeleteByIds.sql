CREATE PROCEDURE [dbo].[ApiKey_DeleteByIds]
    @Ids [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @BatchSize INT = 100

    WHILE @BatchSize > 0
        BEGIN
            BEGIN TRANSACTION ApiKey_DeleteMany

            DELETE TOP(@BatchSize) AK
            FROM
                [dbo].[ApiKey] AK
            INNER JOIN
                @Ids I ON I.Id = AK.Id

            SET @BatchSize = @@ROWCOUNT

            COMMIT TRANSACTION ApiKey_DeleteMany
        END
END
