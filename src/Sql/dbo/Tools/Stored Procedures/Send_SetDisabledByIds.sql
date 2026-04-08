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