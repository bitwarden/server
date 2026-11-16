CREATE PROCEDURE [dbo].[PamRotationAttempt_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[PamRotationAttemptView]
    WHERE
        [Id] = @Id
END
