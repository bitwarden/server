CREATE PROCEDURE [dbo].[OrganizationScopedApiKey_Create]
    @Id               UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId   UNIQUEIDENTIFIER,
    @Name             NVARCHAR(200),
    @ClientSecretHash VARCHAR(128),
    @Scopes           NVARCHAR(4000),
    @ExpireAt         DATETIME2(7),
    @CreationDate     DATETIME2(7),
    @RevisionDate     DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationScopedApiKey]
    (
        [Id],
        [OrganizationId],
        [Name],
        [ClientSecretHash],
        [Scopes],
        [ExpireAt],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Name,
        @ClientSecretHash,
        @Scopes,
        @ExpireAt,
        @CreationDate,
        @RevisionDate
    )
END
