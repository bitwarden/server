CREATE OR ALTER PROCEDURE [dbo].[EmergencyAccess_DeleteManyById]
   @EmergencyAccessIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @BatchSize INT = 100

    -- Delete EmergencyAccess Records
    WHILE @BatchSize > 0
    BEGIN

        DELETE TOP(@BatchSize) EA
        FROM
            [dbo].[EmergencyAccess] EA
        INNER JOIN
            @EmergencyAccessIds EAI ON EAI.Id = EA.Id

        SET @BatchSize = @@ROWCOUNT

    END
END
GO