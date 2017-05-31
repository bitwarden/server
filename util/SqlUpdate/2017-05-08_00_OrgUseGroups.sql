alter table [organization] add [UseGroups] bit null
go
update [organization] set UseGroups = 0
go
alter table [organization] alter column [UseGroups] bit not null
go