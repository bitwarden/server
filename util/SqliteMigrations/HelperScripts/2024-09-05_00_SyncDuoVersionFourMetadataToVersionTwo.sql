/*
I spent some time looking into how to batch process these queries, but SQLite does not have
a good interface for batch processing. So to improve readability and maintain veloicty I've opted 
for a single update query for each table: "User" and "Organization".

Part of the Reasoning is that SQLite, while not a "toy database", is not usually used in larger
deployments. Scalability is difficult in SQLite, so the assumption is the database is small for 
installations using SQLite. So not running these in a batch should not impact on users who do 
use SQLite.
*/

-- Update User accounts
UPDATE "User"
SET TwoFactorProviders = json_set(
    json_set("User".TwoFactorProviders,
        '$."2".MetaData.ClientSecret',
        json_extract("User".TwoFactorProviders, '$."2".MetaData.SKey')),
        '$."2".MetaData.ClientId',
        json_extract("User".TwoFactorProviders, '$."2".MetaData.IKey')
)
WHERE TwoFactorProviders LIKE '%"2":%'
    AND JSON_VALID(TwoFactorProviders) = 1;

-- Update Organizations
UPDATE "Organization"
SET TwoFactorProviders = json_set(
    json_set("Organization".TwoFactorProviders,
        '$."6".MetaData.ClientSecret',
        json_extract("Organization".TwoFactorProviders, '$."6".MetaData.SKey')),
        '$."6".MetaData.ClientId',
        json_extract("Organization".TwoFactorProviders, '$."6".MetaData.IKey')
)
WHERE TwoFactorProviders LIKE '%"6":%'
    AND JSON_VALID(TwoFactorProviders) = 1;
    