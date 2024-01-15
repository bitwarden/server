-- Set `FlexibleCollections` = 1 for all organizations that have not yet been migrated.
UPDATE "Organization"
SET "FlexibleCollections" = 1
WHERE "FlexibleCollections" = 0;
