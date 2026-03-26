CREATE PROCEDURE [dbo].[Device_BumpLastActivityDateByIdentifierAndUserId]
    @Identifier NVARCHAR(50),
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Both @Identifier and @UserId are required: Identifier is unique per user, not globally
    -- (unique constraint UX_Device_UserId_Identifier is on (UserId, Identifier)). Including
    -- UserId scopes the write to the authenticated user's device and ensures the query hits
    -- UX_Device_UserId_Identifier; without it the query falls back to IX_Device_Identifier,
    -- which is non-unique and would require a scan across all users.
    --
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
        [Identifier] = @Identifier
        AND [UserId] = @UserId
        AND (
            [LastActivityDate] IS NULL
            OR CAST([LastActivityDate] AS DATE) < CAST(GETUTCDATE() AS DATE)
        )
END
