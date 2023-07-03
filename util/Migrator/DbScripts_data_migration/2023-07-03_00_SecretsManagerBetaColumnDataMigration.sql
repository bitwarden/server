/*
This is the data migration script for adding the SecretsManagerBeta column.
The initial migration util/Migrator/DbScripts/2023-07-03_00_SecretsManagerBetaColumn.sql should be run prior.
There is no final migration.
*/
IF COL_LENGTH('[dbo].[Organization]', 'SecretsManagerBeta') IS NOT NULL
BEGIN
    -- Set SecretsManagerBeta to 1 for Organizations where UseSecretsManager is 1.
    -- At this time (at GA release) these are the Organizations using SecretsManager in beta.
    -- We want to mark they are using SecretsManager beta for the sunset period, before
    -- eventually removing the SecretsManagerBeta column and turning off beta access.
    UPDATE [dbo].[Organization]
    SET [SecretsManagerBeta] = 1
    WHERE [UseSecretsManager] = 1
    GO
END
GO
