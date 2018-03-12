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

DECLARE @UrisPath VARCHAR(50) = '$.Uris'

UPDATE
    [dbo].[Cipher]
SET
    [Data] = JSON_MODIFY(
        [Data],
        @UrisPath,
        JSON_QUERY(
            JSON_VALUE([Data], @UrisPath),
            '$'
        )
    )
WHERE
    LEFT(JSON_VALUE([Data], @UrisPath), 8) = '[{"Uri":'
GO
