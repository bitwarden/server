/*
PM-5157 updating the metadata for the Duo 2FA provider to use names that are more
descriptive of the values they hold and match the verbiage used by Duo
*/

-- Update [dbo].[Users]
update
	[User]
set
	[TwoFactorProviders] = replace([TwoFactorProviders],
	'SKey',
	'ClientSecret')
where
	[TwoFactorProviders] like '%{"2":%';

update
	[User]
set
	[TwoFactorProviders] = replace([TwoFactorProviders],
	'IKey',
	'ClientId')
where
	[TwoFactorProviders] like '%{"2":%';

-- Update [dbo].[Organizations]
update
	[Organization]
set
	[TwoFactorProviders] = replace([TwoFactorProviders],
	'SKey',
	'ClientSecret')
where
	[TwoFactorProviders] like '%{"6":%';

update
	[Organization]
set
	[TwoFactorProviders] = replace([TwoFactorProviders],
	'IKey',
	'ClientId')
where
	[TwoFactorProviders] like '%{"6":%';