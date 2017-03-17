CREATE PROCEDURE [dbo].[CipherDetails_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CipherDetailsView]
    WHERE
        [Id] = @Id
END