alter table [organization] add [UseTotp] BIT NULL
go

-- all but free plans
update [organization]
set
[UseTotp] = CASE WHEN [organization].[plantype] != 0 THEN 1 ELSE 0 END,
[MaxStorageGb] = CASE WHEN [organization].[plantype] != 0 THEN 1 ELSE NULL END
go

alter table [organization] alter column [UseTotp] BIT NOT NULL
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
