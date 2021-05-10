CREATE PROCEDURE [dbo].[ProviderOrganization_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[User_BumpAccountRevisionDateByProviderOrganizationId] @Id

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
        [dbo].[OrganizationUser]
    WHERE
        [ProviderId] = @ProviderId
    AND
        [OrganizationId] = @OrganizationId
    
    DELETE
    FROM
        [dbo].[ProviderOrganization]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION ProviderOrganization_DeleteById
END
