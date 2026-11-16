CREATE PROCEDURE [dbo].[PamPendingAccessEnd_DeleteByRotationConfigId]
    @RotationConfigId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[PamPendingAccessEnd]
    WHERE
        [RotationConfigId] = @RotationConfigId
END
