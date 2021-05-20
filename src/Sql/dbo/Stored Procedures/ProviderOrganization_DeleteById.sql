CREATE PROCEDURE [dbo].[ProviderOrganization_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    BEGIN TRANSACTION ProviderOrganization_DeleteById

    DECLARE @ProviderId UNIQUEIDENTIFIER
    DECLARE @OrganizationId UNIQUEIDENTIFIER

    SELECT
        @ProviderId = [ProviderId],
        @OrganizationId = [OrganizationId]
    FROM
        [dbo].[ProviderOrganization]
    WHERE
        [Id] = @Id

    DELETE
    FROM
        [dbo].[ProviderOrganization]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION ProviderOrganization_DeleteById
END
