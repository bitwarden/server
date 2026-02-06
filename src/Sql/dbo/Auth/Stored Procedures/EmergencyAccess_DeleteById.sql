CREATE PROCEDURE [dbo].[EmergencyAccess_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    
    EXEC [dbo].[User_BumpAccountRevisionDateByEmergencyAccessGranteeId] @Id
    
    DELETE
    FROM
        [dbo].[EmergencyAccess]
    WHERE
        [Id] = @Id
END