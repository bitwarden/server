EXEC sp_rename 'dbo.User.StripeSubscriptionId', 'GatewaySubscriptionId', 'COLUMN';
GO
EXEC sp_rename 'dbo.User.StripeCustomerId', 'GatewayCustomerId', 'COLUMN';
GO

EXEC sp_rename 'dbo.Organization.StripeSubscriptionId', 'GatewaySubscriptionId', 'COLUMN';
GO
EXEC sp_rename 'dbo.Organization.StripeCustomerId', 'GatewayCustomerId', 'COLUMN';
GO





alter table [user] add [Gateway] TINYINT NULL
go

alter table [organization] add [Gateway] TINYINT NULL
go



update [user] set [Gateway] = 0 where GatewaySubscriptionId IS NOT NULL
go

update [organization] set [Gateway] = 0 where GatewaySubscriptionId IS NOT NULL
go




drop view [dbo].[OrganizationView]
go

CREATE VIEW [dbo].[OrganizationView]
AS
SELECT
    *
FROM
    [dbo].[Organization]
GO




drop view [dbo].[UserView]
go

CREATE VIEW [dbo].[UserView]
AS
SELECT
    *
FROM
    [dbo].[User]
GO
