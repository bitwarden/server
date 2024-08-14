BEGIN TRY
    BEGIN TRANSACTION;

    UPDATE
        [dbo].[Organization]
    SET
        [Use2fa] = 1,
        [UseApi] = 1,
        [UseDirectory] = 1,
        [UseEvents] = 1,
        [UseGroups] = 1,
        [UsersGetPremium] = 1
    WHERE
        [PlanType] IN (2, 3); -- Teams 2019

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    THROW;
END CATCH
