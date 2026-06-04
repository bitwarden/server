CREATE PROCEDURE [dbo].[LeaseRequest_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[LeaseRequest]
    WHERE
        [Id] = @Id
END
