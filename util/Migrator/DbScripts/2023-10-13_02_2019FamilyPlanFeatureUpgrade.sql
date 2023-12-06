BEGIN TRY
    BEGIN TRANSACTION;

    UPDATE
        [dbo].[Organization]
    SET
        [UsersGetPremium] = 1
    WHERE
        [PlanType] = 1 -- Families 2019 Annual

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    THROW;
END CATCH
