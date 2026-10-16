CREATE PROCEDURE [dbo].[PamRotationJob_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[PamRotationJobView]
    WHERE
        [Id] = @Id
END
