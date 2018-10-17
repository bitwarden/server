CREATE PROCEDURE [dbo].[Collection_ReadByIdUserId]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    SELECT TOP 1
        *
    FROM
        [dbo].[UserCollectionDetails](@UserId)
    WHERE
        [Id] = @Id
    ORDER BY
        [ReadOnly] ASC
END