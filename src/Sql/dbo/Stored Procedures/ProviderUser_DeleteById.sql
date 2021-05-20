CREATE PROCEDURE [dbo].[ProviderUser_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[User_BumpAccountRevisionDateByProviderUserId] @Id

    BEGIN TRANSACTION ProviderUser_DeleteById

    DECLARE @ProviderId UNIQUEIDENTIFIER
    DECLARE @UserId UNIQUEIDENTIFIER

    SELECT
        @ProviderId = [ProviderId],
        @UserId = [UserId]
    FROM
        [dbo].[ProviderUser]
    WHERE
        [Id] = @Id

    DELETE
    FROM
        [dbo].[ProviderUser]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION ProviderUser_DeleteById
END
