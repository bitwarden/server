/*
    Script to backfill the MasterPasswordSalt column in the User table. We are back filling with the email
    because we use the email today as the salt, but we want to separate it out for future flexibility.

    This script performs the following steps:
    1) Creates a temporary index on the MasterPasswordSalt column to speed up the backfill process.
    2) Performs a batched update to populate the MasterPasswordSalt column for existing MasterPassword users.
    3) Drops the temporary index after the backfill is complete.

    Note: The MasterPasswordSalt is populated by normalizing the Email field (lowercase and trimmed).
*/

-- 1) Create a temporary index to speed up the backfill
IF NOT EXISTS (
    SELECT 1
FROM sys.indexes
WHERE name = 'IX_User_MasterPasswordSalt'
    AND object_id = OBJECT_ID('[dbo].[User]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_User_MasterPasswordSalt]
        ON [dbo].[User]([MasterPasswordSalt] ASC);
END
GO

-- 2) Batched backfill (batch size = 1000) with a short delay between batches
SET NOCOUNT ON;

WHILE 1 = 1
BEGIN
    ;WITH
        batch
        AS
        (
            SELECT TOP (1000)
                [Id]
            -- smaller batch size to reduce IO/blocking
            FROM [dbo].[User] WITH (INDEX([IX_User_MasterPasswordSalt]))
            WHERE [MasterPasswordSalt] IS NULL
                AND [MasterPassword] IS NOT NULL -- Only backfill users who have a MasterPassword, as those without can have NULL salt
            ORDER BY [Id]
        )
    UPDATE u
    SET [MasterPasswordSalt] = LOWER(LTRIM(RTRIM(u.[Email]))) -- mirrors current client normalization
    FROM [dbo].[User] u
        INNER JOIN batch b ON b.Id = u.Id;

    IF @@ROWCOUNT = 0 BREAK;

    -- Throttle between batches (adjust as needed)
    WAITFOR DELAY '00:00:01';
END
GO

-- 3) Drop the temporary index since it's not needed afterwards
IF EXISTS (
    SELECT 1
FROM sys.indexes
WHERE name = 'IX_User_MasterPasswordSalt'
    AND object_id = OBJECT_ID('[dbo].[User]')
)
BEGIN
    DROP INDEX [IX_User_MasterPasswordSalt] ON [dbo].[User];
END
GO