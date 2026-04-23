CREATE PROCEDURE [dbo].[Device_UpdateLastActivityDateById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Only update if LastActivityDate has never been set or was last set on a prior calendar day.
    -- This acts as a fallback guard against redundant writes in case the application-layer cache
    -- is unavailable or evicted. In normal operation the cache
    -- prevents this procedure from being called more than once per device per day entirely.
    -- Product only requires day-level granularity (today / this week / last week / etc.).
    UPDATE
        [dbo].[Device]
    SET
        [LastActivityDate] = GETUTCDATE()
    WHERE
        [Id] = @Id
        AND (
            [LastActivityDate] IS NULL
            OR CAST([LastActivityDate] AS DATE) < CAST(GETUTCDATE() AS DATE)
        )
END
