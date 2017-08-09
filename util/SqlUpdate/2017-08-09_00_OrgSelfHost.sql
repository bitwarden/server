alter table [Organization] add [SelfHost] BIT NULL
go


update [Organization] set [SelfHost] = 0
go

update [Organization] set [SelfHost] = 1 where PlanType = 4 or PlanType = 5
go


alter table [Organization] alter column [SelfHost] BIT NOT NULL
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

