CREATE PROCEDURE [dbo].[EmergencyAccess_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[EmergencyAccess]
    WHERE
        [Id] = @Id
END