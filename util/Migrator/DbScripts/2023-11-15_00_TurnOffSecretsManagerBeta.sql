IF COL_LENGTH('[dbo].[Organization]', 'SecretsManagerBeta') IS NOT NULL
BEGIN
    -- Set AccessSecretsManager to 0 for Organization Users in SM Beta orgs
    UPDATE [dbo].[OrganizationUser]
    SET AccessSecretsManager = 0
    FROM [dbo].[OrganizationUser] AS ou
    INNER JOIN [dbo].[Organization] AS o ON ou.OrganizationId = o.Id
    WHERE o.SecretsManagerBeta = 1 AND ou.AccessSecretsManager = 1

    -- Set UseSecretsManager and SecretsManagerBeta to 0 for Organizations in SM Beta
    UPDATE [dbo].[Organization]
    SET UseSecretsManager = 0, SecretsManagerBeta = 0
    FROM [dbo].[Organization] AS o
    WHERE o.SecretsManagerBeta = 1
END
GO
