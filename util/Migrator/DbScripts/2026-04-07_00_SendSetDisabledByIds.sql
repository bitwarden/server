IF OBJECT_ID('[dbo].[Send_SetDisabledByIds]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Send_SetDisabledByIds]
END
GO

CREATE PROCEDURE [dbo].[Send_SetDisabledByIds]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @Disabled BIT
AS
BEGIN
    SET NOCOUNT ON

    -- Set field
    UPDATE [dbo].[Send]
    SET
      [Disabled] = @Disabled
    WHERE
        [Id] IN (SELECT * FROM @Ids)
END
GO