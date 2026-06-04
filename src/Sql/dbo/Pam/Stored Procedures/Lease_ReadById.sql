CREATE PROCEDURE [dbo].[Lease_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[Lease]
    WHERE
        [Id] = @Id
END
