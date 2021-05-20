CREATE PROCEDURE [dbo].[Provider_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[User_BumpAccountRevisionDateByProviderId] @Id

    BEGIN TRANSACTION Provider_DeleteById

    DELETE
    FROM 
        [dbo].[ProviderUser]
    WHERE 
        [ProviderId] = @Id

    DELETE
    FROM
        [dbo].[Provider]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION Provider_DeleteById
END
