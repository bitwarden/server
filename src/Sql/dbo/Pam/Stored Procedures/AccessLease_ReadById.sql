CREATE PROCEDURE [dbo].[AccessLease_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[AccessLease]
    WHERE
        [Id] = @Id
END
