-- Assumptions:
-- When a 2FA method is disabled, it is removed from the TwoFactorProviders array

-- Problem statement:
-- We have users who currently do not have any 2FA method, with the only one being
-- WebAuthn, which is effectively disabled by a server-side permission check for Premium status.
-- With WebAuthn being made free, we want to avoid these users suddenly being forced
-- to provide 2FA using a key that they haven't used in a long time, by deleting that key from their TwoFactorProviders.
declare @TwoFactorByUser TABLE
(
    Id UNIQUEIDENTIFIER,
    Email NVARCHAR(256),
    TwoFactorType INT
)

INSERT INTO @TwoFactorByUser
SELECT  u.Id, 
        u.Email,
        tfp1.[key] as TwoFactorType
FROM [User] u
LEFT OUTER JOIN [OrganizationUser] ou on ou.UserId = u.Id
LEFT OUTER JOIN [Organization] o on o.Id = ou.OrganizationId
CROSS APPLY OPENJSON(u.TwoFactorProviders) tfp1
CROSS APPLY OPENJSON(tfp1.[value]) WITH (
   [Enabled] nvarchar(10) '$.Enabled'
) tfp2
WHERE [Enabled] = 'true' -- We only want enabled 2FA methods
AND Premium = 0 -- User isn't Premium
AND (o IS NULL OR (o IS NOT NULL AND o.Enabled = 1 AND o.UsersGetPremium = 0)) -- User doesn't have Premium from their org

select *
from @TwoFactorByUser t1
where t1.TwoFactorType = 7
AND NOT EXISTS (SELECT * FROM @TwoFactorByUser t2 WHERE t2.Id = t1.Id and t2.TwoFactorType <> 7)

-- UPDATE [User]
-- SET TwoFactorProviders = NULL
-- FROM @TwoFactorByUser tf
-- WHERE tf.Id = [User].Id