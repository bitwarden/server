CREATE PROCEDURE [dbo].[ProviderOrganization_Update]
    @Id UNIQUEIDENTIFIER,
    @ProviderId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Key VARCHAR(MAX),
    @Settings NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[ProviderOrganization]
    SET
        [ProviderId] = @ProviderId,
        [OrganizationId] = @OrganizationId,
        [Key] = @Key,
        [Settings] = @Settings,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
