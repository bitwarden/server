-- Populate DefaultCollectionSemaphore from existing Type=1 (DefaultUserCollection) collections
-- This migration is idempotent and can be run multiple times safely
INSERT INTO [dbo].[DefaultCollectionSemaphore]
(
    [OrganizationUserId],
    [CreationDate]
)
SELECT DISTINCT
    cu.[OrganizationUserId],
    GETUTCDATE()
FROM
    [dbo].[Collection] c
INNER JOIN
    [dbo].[CollectionUser] cu ON c.[Id] = cu.[CollectionId]
WHERE
    c.[Type] = 1 -- CollectionType.DefaultUserCollection
    AND NOT EXISTS
    (
        SELECT
            1
        FROM
            [dbo].[DefaultCollectionSemaphore] dcs
        WHERE
            dcs.[OrganizationUserId] = cu.[OrganizationUserId]
    );
GO
