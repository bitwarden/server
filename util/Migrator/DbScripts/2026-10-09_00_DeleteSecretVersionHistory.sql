-- SM-2090: Remove SecretVersion rows recorded before secrets versioning was
-- gated behind the sm-1587-secrets-versioning feature flag (#8330, v2026.9.2).
-- Those rows were written for customers who never enabled the feature, and some
-- were captured by code later fixed in #8307. Version history starts fresh.
--
-- Safe to re-run: deleting from an empty table is a no-op.

DELETE FROM [dbo].[SecretVersion];
GO