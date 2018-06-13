CREATE PROCEDURE [dbo].[User_UpdateStorage]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @Storage BIGINT

    CREATE TABLE #UserStorageUpdateTemp
    ( 
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [Attachments] VARCHAR(MAX) NULL
    )

    INSERT INTO #UserStorageUpdateTemp
    SELECT
        [Id],
        [Attachments]
    FROM
        [dbo].[Cipher]
    WHERE
        [UserId] = @Id

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
            #UserStorageUpdateTemp
    )
    SELECT
        @Storage = SUM([CTE].[Size])
    FROM
        [CTE]

    DROP TABLE #UserStorageUpdateTemp

    UPDATE
        [dbo].[User]
    SET
        [Storage] = @Storage,
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @Id
END