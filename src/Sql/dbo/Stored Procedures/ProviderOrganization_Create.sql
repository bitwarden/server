CREATE PROCEDURE [dbo].[ProviderOrganization_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @ProviderId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Key VARCHAR(MAX),
    @Settings NVARCHAR(MAX),
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
        [Settings],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @ProviderId,
        @OrganizationId,
        @Key,
        @Settings,
        @CreationDate,
        @RevisionDate
    )
END
