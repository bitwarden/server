CREATE PROCEDURE [dbo].[EmergencyAccessDetails_ReadByIdGrantorId]
    @Id UNIQUEIDENTIFIER,
    @GrantorId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[EmergencyAccessDetailsView]
    WHERE
        [Id] = @Id
    AND
        [GrantorId] = @GrantorId
END