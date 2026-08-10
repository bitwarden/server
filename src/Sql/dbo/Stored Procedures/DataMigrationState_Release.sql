CREATE PROCEDURE [dbo].[DataMigrationState_Release]
    @Name VARCHAR(100),
    @Partition INT,
    @LeaseOwner VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON

    -- Fenced like the checkpoint: releasing a lease we no longer hold is a no-op.
    UPDATE [dbo].[DataMigrationState]
    SET
        [LeaseOwner] = NULL,
        [LeaseExpiresDate] = NULL,
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Name] = @Name
        AND [Partition] = @Partition
        AND [LeaseOwner] = @LeaseOwner
END
