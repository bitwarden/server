/* Update User Table */
UPDATE
	`User` U
SET
	U.TwoFactorProviders = JSON_SET(
        JSON_SET(
            U.TwoFactorProviders, '$."2".MetaData.ClientSecret',
	JSON_UNQUOTE(JSON_EXTRACT(U.TwoFactorProviders,'$."2".MetaData.SKey'))),
	'$."2".MetaData.ClientId',
	JSON_UNQUOTE(JSON_EXTRACT(U.TwoFactorProviders,'$."2".MetaData.IKey')))
WHERE
	JSON_CONTAINS(TwoFactorProviders,
	'{"2":{}}')
	AND JSON_VALID(TwoFactorProviders);

/* Update Organization Table */
UPDATE
	Organization o
SET
	o.TwoFactorProviders = JSON_SET(
        JSON_SET(
            o.TwoFactorProviders, '$."6".MetaData.ClientSecret',
	JSON_UNQUOTE(JSON_EXTRACT(o.TwoFactorProviders,'$."6".MetaData.SKey'))),
	'$."6".MetaData.ClientId',
	JSON_UNQUOTE(JSON_EXTRACT(o.TwoFactorProviders,'$."6".MetaData.IKey')))
WHERE
	JSON_CONTAINS(o.TwoFactorProviders,
	'{"6":{}}')
	AND JSON_VALID(o.TwoFactorProviders);
