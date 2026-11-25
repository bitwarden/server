CREATE PROCEDURE [dbo].[PamRotationConfig_ReadManyPendingAccessEnd]
    @CreatedBefore DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The sweep's access-end phase. Newer rows are left to the trigger the lease end runs inline.
    SELECT
        C.*
    FROM
        [dbo].[PamRotationConfigView] C
    WHERE
        EXISTS (
            SELECT
                1
            FROM
                [dbo].[PamPendingAccessEnd] P
            WHERE
                P.[RotationConfigId] = C.[Id]
                AND P.[CreationDate] <= @CreatedBefore
        )
END
