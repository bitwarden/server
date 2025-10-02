mod migrate;
pub use migrate::{Migration, MigrationError, run_pending_migrations};

mod pool;
pub use pool::ConnectionManager as MsSqlConnectionManager;

// re-expose tiberius types for convenience
pub use tiberius::{error, Column, Row, ToSql};

// re-expose bb8 types for convenience
pub type Pool = bb8::Pool<MsSqlConnectionManager>;
pub type PooledConnection<'a> = bb8::PooledConnection<'a, MsSqlConnectionManager>;

// re-expose macros for convenience
pub use macros::{load_migrations, migration};
