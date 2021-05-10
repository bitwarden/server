CREATE PROCEDURE [dbo].[ProviderOrganization_Create]
    @Id UNIQUEIDENTIFIER,
    @ProviderId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Key VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[ProviderOrganization]
    (
        [Id],
        [ProviderId],
        [OrganizationId],
        [Key],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @ProviderId,
        @OrganizationId,
        @Key,
        @CreationDate,
        @RevisionDate
    )
END
