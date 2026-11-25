CREATE PROCEDURE [dbo].[PamPendingAccessEnd_DeleteByRotationConfigId]
    @RotationConfigId UNIQUEIDENTIFIER,
    @CreatedOnOrBefore DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[PamPendingAccessEnd]
    WHERE
        [RotationConfigId] = @RotationConfigId
        AND (@CreatedOnOrBefore IS NULL OR [CreationDate] <= @CreatedOnOrBefore)
END
