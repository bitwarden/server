BEGIN TRY
    BEGIN TRANSACTION;

    UPDATE
        [dbo].[Organization]
    SET
        [UseSso] = 1,
        [UseScim] = 1,
        [UseResetPassword] = 1
    WHERE
        [PlanType] IN (4, 5) -- Enterprise 2019

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    THROW;
END CATCH
