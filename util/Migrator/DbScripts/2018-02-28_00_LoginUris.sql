/*
{
    "Name":"2.xx",
    "Uri":"2.yy",
    "Username":"2.zz",
    "Password":"2.aa"
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

DECLARE @UriPath VARCHAR(50) = '$.Uri'
DECLARE @UrisPath VARCHAR(50) = '$.Uris'
DECLARE @BatchSize INT = 1000
DECLARE @LastId UNIQUEIDENTIFIER = NULL

-- Step 1: Add new Uris property with data from Uri
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
            AND JSON_VALUE([Data], @UriPath) IS NOT NULL
            AND JSON_QUERY([Data], @UrisPath) IS NULL
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
                '[{"Uri":"' + CAST(JSON_VALUE([Data], @UriPath) AS NVARCHAR(MAX)) + '"}]',
                '$'
            )
        )
    OUTPUT INSERTED.[Id] INTO [CompletedIds]

    SET @BatchSize = @@ROWCOUNT
    RAISERROR('Updated %d ciphers with Uris', 0, 1, @BatchSize) WITH NOWAIT
END
GO

-- Reset
TRUNCATE TABLE [CompletedIds]
DECLARE @UriPath VARCHAR(50) = '$.Uri'
DECLARE @UrisPath VARCHAR(50) = '$.Uris'
DECLARE @BatchSize INT = 1000
DECLARE @LastId UNIQUEIDENTIFIER = NULL

-- Step 2: Remove old Uri Property
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
            AND JSON_VALUE([Data], @UriPath) IS NOT NULL
            AND JSON_QUERY([Data], @UrisPath) IS NOT NULL
        ORDER BY
            [Id] ASC
    )
    UPDATE
        [CTE]
    SET
        [Data] = JSON_MODIFY([Data], @UriPath, NULL)
    OUTPUT INSERTED.[Id] INTO [CompletedIds]

    SET @BatchSize = @@ROWCOUNT
    RAISERROR('Updated %d ciphers with Uri removal', 0, 1, @BatchSize) WITH NOWAIT
END
GO

IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CompletedIds')
BEGIN
    DROP TABLE [CompletedIds]
END
GO
