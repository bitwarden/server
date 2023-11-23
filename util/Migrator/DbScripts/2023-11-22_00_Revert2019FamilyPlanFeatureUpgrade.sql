BEGIN TRY
    UPDATE [dbo].[Organization]
    SET [UsersGetPremium] = 0
    WHERE [PlanType] = 1; -- Families 2019 Annual
END TRY
BEGIN CATCH
    THROW;
END CATCH