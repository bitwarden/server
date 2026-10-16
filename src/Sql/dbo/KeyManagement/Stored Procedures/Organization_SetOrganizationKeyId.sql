CREATE PROCEDURE [dbo].[Organization_SetOrganizationKeyId]
    @Id UNIQUEIDENTIFIER,
    @OrganizationKeyId VARCHAR(32),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The key id is set once. Changing an existing key id must happen through an organization key rotation, so a
    -- row that already has a key id is not updated. Returns 1 when the row was updated, and 0 when it was not.
    UPDATE
        [dbo].[Organization]
    SET
        [OrganizationKeyId] = @OrganizationKeyId,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
        AND [OrganizationKeyId] IS NULL

    SELECT CAST(CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END AS BIT)
END
