CREATE PROCEDURE [dbo].[SsoConfig_ReadManyByNotBeforeRevisionDate]
    @NotBefore DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SsoConfigView]
    WHERE
        [Enabled] = 1
        AND [RevisionDate] >= COALESCE(@NotBefore, [RevisionDate]);
END
