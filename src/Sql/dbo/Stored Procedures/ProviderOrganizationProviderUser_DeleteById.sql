CREATE PROCEDURE [dbo].[ProviderOrganizationProviderUser_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    BEGIN TRANSACTION POPU_DeleteById

    DECLARE @ProviderUserId UNIQUEIDENTIFIER

    SELECT
        @ProviderUserId = [ProviderUserId]
    FROM
        [dbo].[ProviderOrganizationProviderUser]
    WHERE
        [Id] = @Id

    DELETE
    FROM
        [dbo].[ProviderOrganizationProviderUser]
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDateByProviderUserId] @ProviderUserId

    COMMIT TRANSACTION POPU_DeleteById
END
