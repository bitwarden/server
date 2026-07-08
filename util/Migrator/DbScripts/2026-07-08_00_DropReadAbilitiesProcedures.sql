-- Remove obsolete stored procedures used by the retired in-memory application cache.
-- The organization/provider ability caches now read individual records via
-- [dbo].[Organization_ReadAbilityById] and [dbo].[Provider_ReadAbilityById].

DROP PROCEDURE IF EXISTS [dbo].[Organization_ReadAbilities]
GO

DROP PROCEDURE IF EXISTS [dbo].[Provider_ReadAbilities]
GO
