CREATE PROCEDURE [dbo].[AuthRequest_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[AuthRequest]
    WHERE
        [Id] = @Id
END