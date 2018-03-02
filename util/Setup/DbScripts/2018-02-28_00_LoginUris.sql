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
    "Uris": [
        {
            "Uri": "2.yy",
            "Match": 0
        }
    ]
}
*/

SET NOCOUNT ON

DECLARE @UriPath VARCHAR(50) = '$.Uri'
DECLARE @UrisPath VARCHAR(50) = '$.Uris'
DECLARE @BatchSize INT = 1000

-- Step 1: Add new Uris property with data from Uri
WHILE @BatchSize > 0
BEGIN
    UPDATE TOP (@BatchSize)
        [Cipher]
    SET
        [Data] = JSON_MODIFY(
            [Data],
            @UrisPath,
            JSON_QUERY(
                '[{"Uri":"' + JSON_VALUE([Data], @UriPath) + '"}]',
                '$'
            )
        )
    WHERE
        JSON_VALUE([Data], @UriPath) IS NOT NULL
        AND JSON_QUERY([Data], @UrisPath) IS NULL

    SET @BatchSize = @@ROWCOUNT
    RAISERROR('Updated %d ciphers with Uris', 0, 1, @BatchSize) WITH NOWAIT
END

-- Step 2: Remove old Uri Property
SET @BatchSize = 1000

WHILE @BatchSize > 0
BEGIN
    UPDATE TOP (@BatchSize)
        [Cipher]
    SET
        [Data] = JSON_MODIFY([Data], @UriPath, NULL)
    WHERE
        JSON_VALUE([Data], @UriPath) IS NOT NULL
        AND JSON_QUERY([Data], @UrisPath) IS NOT NULL

    SET @BatchSize = @@ROWCOUNT
    RAISERROR('Updated %d ciphers with Uri removal', 0, 1, @BatchSize) WITH NOWAIT
END
