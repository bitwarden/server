alter table [user] drop column twofactorenabled
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
