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
    TwoFactorType INT,
    TypeEnabled BIT
)

INSERT INTO @TwoFactorByUser
SELECT  u.Id, 
        u.Email,
        tfp1.[key] as TwoFactorType, 
        CASE [Enabled] WHEN 'true' THEN 1 ELSE 0 END as TypeEnabled
FROM [User] u
CROSS APPLY OPENJSON(u.TwoFactorProviders) tfp1
CROSS APPLY OPENJSON(tfp1.[value]) WITH (
   [Enabled] nvarchar(10) '$.Enabled'
) tfp2
WHERE [Enabled] = 'true'
AND Premium = 0

select *
from @TwoFactorByUser t1
where t1.TwoFactorType = 7 and t1.TypeEnabled = 1
AND NOT EXISTS (SELECT * FROM @TwoFactorByUser t2 WHERE t2.Id = t1.Id and t2.TwoFactorType <> 7)

UPDATE [User]
SET TwoFactorProviders = NULL
FROM @TwoFactorByUser tf
WHERE tf.Id = [User].Id