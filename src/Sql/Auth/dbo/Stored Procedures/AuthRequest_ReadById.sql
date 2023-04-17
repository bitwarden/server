CREATE PROCEDURE [dbo].[AuthRequest_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[AuthRequestView]
    WHERE
        [Id] = @Id
END