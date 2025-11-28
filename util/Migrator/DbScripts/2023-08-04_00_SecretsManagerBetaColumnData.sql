/*
    Set SecretsManagerBeta to 1 for Organizations where UseSecretsManager is 1.
    At this time (at GA release) these are the Organizations using SecretsManager in beta.
    We want to mark they are using SecretsManager beta for the sunset period, before
    eventually removing the SecretsManagerBeta column and turning off beta access.

    Please note- this was a data migration script until SM-873.
*/
IF COL_LENGTH('[dbo].[Organization]', 'SecretsManagerBeta') IS NOT NULL
BEGIN
    UPDATE [dbo].[Organization]
    SET [SecretsManagerBeta] = 1
    WHERE [UseSecretsManager] = 1
END
GO
