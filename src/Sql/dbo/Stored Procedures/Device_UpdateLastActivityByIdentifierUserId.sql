CREATE PROCEDURE [dbo].[Device_UpdateLastActivityByIdentifierUserId]
    @Identifier NVARCHAR(50),
    @UserId UNIQUEIDENTIFIER,
    @LastActivityDate DATETIME2(7),
    @ClientVersion NVARCHAR(43) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Both @Identifier and @UserId are required: Identifier is unique per user, not globally
    -- (unique constraint UX_Device_UserId_Identifier is on (UserId, Identifier)). Including
    -- UserId scopes the write to the authenticated user's device and ensures the query hits
    -- UX_Device_UserId_Identifier; without it the query falls back to IX_Device_Identifier,
    -- which is non-unique and would require a scan across all users.
    --
    -- See Device_UpdateLastActivityById for the event-oriented naming rationale and the per-column
    -- guard semantics — same body, different row-lookup key.
    UPDATE
        [dbo].[Device]
    SET
        [LastActivityDate] =
            CASE
                WHEN [LastActivityDate] IS NULL OR CAST([LastActivityDate] AS DATE) < CAST(@LastActivityDate AS DATE)
                    THEN @LastActivityDate
                ELSE [LastActivityDate]
            END,
        [ClientVersion] =
            CASE
                WHEN @ClientVersion IS NOT NULL AND ([ClientVersion] IS NULL OR [ClientVersion] <> @ClientVersion)
                    THEN @ClientVersion
                ELSE [ClientVersion]
            END
    WHERE
        [Identifier] = @Identifier
        AND [UserId] = @UserId
        AND (
            [LastActivityDate] IS NULL
            OR CAST([LastActivityDate] AS DATE) < CAST(@LastActivityDate AS DATE)
            OR (@ClientVersion IS NOT NULL AND ([ClientVersion] IS NULL OR [ClientVersion] <> @ClientVersion))
        )
END
