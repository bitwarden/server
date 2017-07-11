CREATE PROCEDURE [dbo].[User_UpdateStorage]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @Storage BIGINT

    ;WITH [CTE] AS (
        SELECT
            [Id],
            (
                SELECT
                    SUM(CAST(JSON_VALUE(value,'$.Size') AS BIGINT))
                FROM
                    OPENJSON([Attachments])
            ) [Size]
        FROM
            [dbo].[Cipher]
    )
    SELECT
        @Storage = SUM([CTE].[Size])
    FROM
        [dbo].[Cipher] C
    LEFT JOIN
        [CTE] ON C.[Id] = [CTE].[Id]
    WHERE
        C.[UserId] = @Id
        AND C.[Attachments] IS NOT NULL

    UPDATE
        [dbo].[User]
    SET
        [Storage] = @Storage,
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @Id
END