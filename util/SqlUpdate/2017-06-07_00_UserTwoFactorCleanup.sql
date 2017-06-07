update [user]
set twofactorproviders = '{"0":{"Enabled":'+ (case when twofactorenabled = 1 then 'true' else 'false' end) +',"Remember":true,"MetaData":{"Key":"'+ authenticatorkey +'"}}}'
where twofactorprovider is not null and twofactorprovider = 0
and authenticatorkey is not null
go

alter table [user] drop column authenticatorkey
go

drop view [userview]
go

CREATE VIEW [dbo].[UserView]
AS
SELECT
    *
FROM
    [dbo].[User]
go

