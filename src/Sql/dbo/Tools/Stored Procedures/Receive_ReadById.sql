CREATE PROCEDURE [dbo].[Receive_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ReceiveView]
    WHERE
        [Id] = @Id
END
