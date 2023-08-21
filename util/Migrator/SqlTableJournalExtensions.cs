using DbUp.Builder;

namespace Bit.Migrator;

public static class SqlTableJournalExtensions
{
    public static UpgradeEngineBuilder JournalRerunableToSqlTable(this UpgradeEngineBuilder builder, string schema, string table)
    {
        builder.Configure(c => c.Journal = new RerunableSqlTableJournal(() => c.ConnectionManager, () => c.Log, schema, table));
        return builder;
    }
}

public class RerunableSqlTableJournal : TableJournal
{

    public RerunableSqlTableJournal(Func<IConnectionManager> connectionManager, Func<IUpgradeLog> logger, string schema, string table)
        : base(connectionManager, logger, new SqlServerObjectParser(), schema, table)
    {
    }

    protected new IDbCommand GetInsertScriptCommand(Func<IDbCommand> dbCommandFactory, SqlScript script)
    {
        var command = dbCommandFactory();

        var scriptNameParam = command.CreateParameter();
        scriptNameParam.ParameterName = "scriptName";
        scriptNameParam.Value = script.Name;
        command.Parameters.Add(scriptNameParam);

        var appliedParam = command.CreateParameter();
        appliedParam.ParameterName = "applied";
        appliedParam.Value = DateTime.Now;
        command.Parameters.Add(appliedParam);

        command.CommandText = GetInsertJournalEntrySql("@scriptName", "@applied");
        command.CommandType = CommandType.Text;
        return command;
    }

    protected override string GetInsertJournalEntrySql(string @scriptName, string @applied)
    {
        return $"insert into {FqSchemaTableName} (ScriptName, Applied) values ({@scriptName}, {@applied})";
    }

    protected override string GetJournalEntriesSql()
    {
        return $"select [ScriptName] from {FqSchemaTableName} order by [ScriptName]";
    }

    protected override string CreateSchemaTableSql(string quotedPrimaryKeyName)
    {
        return
$@"create table {FqSchemaTableName} (
    [Id] int identity(1,1) not null constraint {quotedPrimaryKeyName} primary key,
    [ScriptName] nvarchar(255) not null,
    [Applied] datetime not null
)";
    }
}
