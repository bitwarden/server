/*
Revert PM-5157 the metadata for the Duo 2FA provider to use legacy names used by Duo
*/

-- Update Users
update
	"User"
set
	"TwoFactorProviders" = replace("TwoFactorProviders",
	'ClientSecret',
	'SKey')
where
	"TwoFactorProviders" like '%{"2":%';

update
	"User"
set
	"TwoFactorProviders" = replace("TwoFactorProviders",
	'ClientId',
	'IKey')
where
	"TwoFactorProviders" like '%{"2":%';

-- Update Organizations
update
	"Organization" 
set
	"TwoFactorProviders" = replace("TwoFactorProviders",
	'ClientSecret',
	'SKey')
where
	"TwoFactorProviders" like '%{"6":%';

update
	"Organization"
set
	"TwoFactorProviders" = replace("TwoFactorProviders",
	'ClientId',
	'IKey')
where
	"TwoFactorProviders" like '%{"6":%';