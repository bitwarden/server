CREATE PROCEDURE [dbo].[ProviderUserUserDetails_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderUserUserDetailsView]
    WHERE
        [Id] = @Id
END
