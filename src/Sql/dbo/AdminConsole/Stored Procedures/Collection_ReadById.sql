CREATE PROCEDURE [dbo].[Collection_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CollectionView]
    WHERE
        [Id] = @Id
END