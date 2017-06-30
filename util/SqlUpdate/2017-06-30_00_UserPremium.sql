alter table [user] add [Premium] BIT NULL
go

update [user] set [premium] = 0
go

alter table [user] alter column [premium] BIT NOT NULL
go
