/*
{
    "Name":"2.xx",
    "Username":"2.zz",
    "Password":"2.aa",
    "Uris":"[{\"Uri\":\"2.yy\"}]"
}
=>
{
    "Name":"2.xx",
    "Username":"2.zz",
    "Password":"2.aa",
    "Uris":[{"Uri": "2.yy"}]
}
*/

SET NOCOUNT ON;

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CompletedIds')
BEGIN
    CREATE TABLE [CompletedIds] ([Id] UNIQUEIDENTIFIER PRIMARY KEY)
END
GO

DECLARE @UrisPath VARCHAR(50) = '$.Uris'
DECLARE @BatchSize INT = 1000
DECLARE @LastId UNIQUEIDENTIFIER = NULL

WHILE @BatchSize > 0
BEGIN
    SELECT TOP 1
        @LastId = [Id]
    FROM
        [CompletedIds]
    ORDER BY
        [Id] DESC

    ;WITH [CTE] AS (
        SELECT TOP (@BatchSize)
            *
        FROM
            [Cipher]
        WHERE
            (@LastId IS NULL OR [Id] > @LastId)
            AND LEFT(JSON_VALUE([Data], @UrisPath), 8) = '[{"Uri":'
        ORDER BY
            [Id] ASC
    )
    UPDATE
        [CTE]
    SET
        [Data] = JSON_MODIFY(
            [Data],
            @UrisPath,
            JSON_QUERY(
                JSON_VALUE([Data], @UrisPath),
                '$'
            )
        )
    OUTPUT INSERTED.[Id] INTO [CompletedIds]

    SET @BatchSize = @@ROWCOUNT
    RAISERROR('Updated %d ciphers with Uris', 0, 1, @BatchSize) WITH NOWAIT
END
GO

IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CompletedIds')
BEGIN
    DROP TABLE [CompletedIds]
END
GO
