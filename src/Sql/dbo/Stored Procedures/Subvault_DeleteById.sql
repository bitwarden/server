CREATE PROCEDURE [dbo].[Subvault_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[Subvault]
    WHERE
        [Id] = @Id
END