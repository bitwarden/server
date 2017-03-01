CREATE PROCEDURE [dbo].[Subvault_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SubvaultView]
    WHERE
        [Id] = @Id
END