-- Data Migration
UPDATE ApiKey
SET ClientSecretHash = (
    SELECT CAST(N'' AS XML).value('xs:base64Binary(sql:column("HASH"))', 'VARCHAR(128)')
    FROM (
      SELECT HASHBYTES('SHA2_256', ClientSecret) AS HASH
      ) SRC
    )
WHERE ClientSecretHash IS NULL
