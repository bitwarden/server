alter table [user] add [Premium] BIT NULL
go

update [user] set [premium] = 0
go

alter table [user] alter column [premium] BIT NOT NULL
go

drop view [dbo].[UserView]
go

CREATE VIEW [dbo].[UserView]
AS
SELECT
    *
FROM
    [dbo].[User]
GO
