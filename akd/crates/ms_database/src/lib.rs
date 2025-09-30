mod migrate;

use tokio::net::TcpStream;
use tokio_util::compat::TokioAsyncWriteCompatExt;

pub use migrate::{Migration, MigrationError, run_pending_migrations};
use bb8::ManageConnection;
use tiberius::{AuthMethod, Client, Config};

pub struct ConnectionManager {
    connection_string: String,
}

impl ConnectionManager {
    pub fn new(connection_string: String) -> Self {
        Self { connection_string }
    }

    pub async fn connect(&self) -> Result<ManagedConnection, tiberius::error::Error> {
        let mut config = Config::new();

        config.host("localhost");
        config.port(1433);
        config.authentication(AuthMethod::sql_server("SA", "<YourStrong@Passw0rd>"));
        config.trust_cert(); // on production, it is not a good idea to do this

        let tcp = TcpStream::connect(config.get_addr()).await?;
        tcp.set_nodelay(true)?;

        // To be able to use Tokio's tcp, we're using the `compat_write` from
        // the `TokioAsyncWriteCompatExt` to get a stream compatible with the
        // traits from the `futures` crate.
        let client = Client::connect(config, tcp.compat_write()).await?;

        Ok(ManagedConnection(client))
    }
}

type Stream = tokio_util::compat::Compat<TcpStream>;
pub struct ManagedConnection(Client<Stream>);

// Transparently forward methods to the inner Client
impl ManagedConnection {
    async fn execute(
        &mut self,
        sql: &str,
        params: &[&(dyn tiberius::ToSql)],
    ) -> Result<tiberius::ExecuteResult, tiberius::error::Error> {
        self.0.execute(sql, params).await
    }

    async fn query<'a>(
        &'a mut self,
        sql: &str,
        params: &[&(dyn tiberius::ToSql)],
    ) -> Result<tiberius::QueryStream<'a>, tiberius::error::Error> {
        self.0.query(sql, params).await
    }

    async fn simple_query<'a>(
        &'a mut self,
        sql: &str,
    ) -> Result<tiberius::QueryStream<'a>, tiberius::error::Error> {
        self.0.simple_query(sql).await
    }
    
    async fn bulk_insert<'a>(
        &'a mut self,
        table: &'a str,
    ) -> Result<tiberius::BulkLoadRequest<'a, Stream>, tiberius::error::Error> {
        self.0.bulk_insert(&table).await
    }

    async fn ping(&mut self) -> Result<u8, tiberius::error::Error> {
        let row = self.0.simple_query("SELECT 1").await?.into_first_result().await?;
        let value = row[0].get(0).expect("value is present");
        Ok(value)
    }
}

impl ManageConnection for ConnectionManager {
    type Connection = ManagedConnection;

    type Error = tiberius::error::Error;

    async fn connect(&self) -> Result<Self::Connection, Self::Error> {
        self.connect().await
    }

    async fn is_valid(&self, conn: &mut Self::Connection) -> Result<(), Self::Error> {
        match conn.ping().await {
            Ok(v) if v == 1 => Ok(()),
            Ok(_) => Err(tiberius::error::Error::Protocol(
                "Unexpected ping response".into(),
            )),
            Err(e) => Err(e),
        }
    }

    fn has_broken(&self, _conn: &mut Self::Connection) -> bool {
        // We don't have a good way to determine this sync. r2d2 (which bb8 is based on) recommends
        // always returning false here and relying on `is_valid` to catch broken connections.
        false
    }
}
