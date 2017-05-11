alter table [CollectionUser] drop constraint [PK_CollectionUser]
go

alter table [CollectionUser] drop column id
go

alter table [CollectionUser] drop column revisiondate
go

alter table [CollectionUser] drop column creationdate
go
