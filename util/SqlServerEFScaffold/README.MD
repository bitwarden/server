## Usage

The SqlServerEFScaffold project is intended to be used as a tool for developers to validate their Microsoft SQL Server database changes and Infrastructure.EntityFramework.Models stay in sync for entity framework MS SQL Server repositories.

## Check Infrastructure.EntityFramework.Models (Database First)

Run the following:

```dotnet ef dbcontext scaffold "<local db connection string>" Microsoft.EntityFrameworkCore.SqlServer -o Model```

The dotnet entity framework command will generate models from the local database provided. 

Engineers can reference these models and validate they match with Infrastructure.EntityFramework.Models.


## Check Microsoft SQL Server Database changes (Code/Model First)

Run the following: 

```
dotnet ef migrations add Init
dotnet ef migrations script
```

This will generate a SQL script to initialize a database based on the models in Infrastructure.EntityFramework.Models.

This is helpful to check against the proposed database changes provided in /src/SQL