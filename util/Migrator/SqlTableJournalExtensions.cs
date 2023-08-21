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
