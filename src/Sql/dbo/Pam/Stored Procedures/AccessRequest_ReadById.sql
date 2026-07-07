CREATE PROCEDURE [dbo].[AccessRequest_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[AccessRequest]
    WHERE
        [Id] = @Id
END
