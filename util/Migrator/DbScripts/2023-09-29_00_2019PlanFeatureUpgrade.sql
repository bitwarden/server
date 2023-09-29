BEGIN TRY
    BEGIN TRANSACTION;

    UPDATE [dbo].[Organization]
    SET [Use2fa] = 1,
        [UseApi] = 1,
        [UseDirectory] = 1,
        [UseEvents] = 1,
        [UseGroups] = 1,
        [UsersGetPremium] = 1
    WHERE [PlanType] >= 2 -- Teams (Monthly) 2019
      AND [PlanType] <= 5; -- Enterprise (Annually) 2019

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    THROW;
END CATCH
