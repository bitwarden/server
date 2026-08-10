CREATE PROCEDURE [dbo].[OrganizationApiKey_UpdateManyApiKeys]
    @Updates NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    -- Set-based compare-and-swap: the OriginalApiKey predicate means a concurrently rotated key
    -- never gets clobbered — the rotation's write wins and this row simply doesn't match.
    -- RevisionDate is intentionally NOT bumped: it is surfaced to admins as the key's rotation
    -- date, and protecting the value at rest is not a rotation.
    UPDATE OAK
    SET
        [ApiKey] = U.[ProtectedApiKey]
    FROM [dbo].[OrganizationApiKey] OAK
    INNER JOIN OPENJSON(@Updates) WITH (
        [Id] UNIQUEIDENTIFIER '$.id',
        [OriginalApiKey] VARCHAR(300) '$.originalApiKey',
        [ProtectedApiKey] VARCHAR(300) '$.protectedApiKey'
    ) U ON OAK.[Id] = U.[Id]
    WHERE OAK.[ApiKey] = U.[OriginalApiKey]

    SELECT @@ROWCOUNT
END
