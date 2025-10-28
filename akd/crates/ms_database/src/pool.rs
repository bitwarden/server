use std::sync::RwLock;

use tokio::net::TcpStream;
use tokio_util::compat::TokioAsyncWriteCompatExt;

use bb8::ManageConnection;
use tiberius::{Client, Config};
use tracing::{debug, instrument, info};

#[derive(thiserror::Error, Debug)]
pub enum OnConnectError {
    #[error("Config error: {0}")]
    Config(#[source] tiberius::error::Error),
    #[error("TCP error: {0}")]
    Tcp(#[from] std::io::Error),
    #[error("On Connect error: {0}")]
    OnConnect(#[source] tiberius::error::Error),
}

pub struct ConnectionManager {
    connection_string: String,
    is_healthy: RwLock<bool>,
}

impl ConnectionManager {
    pub fn new(connection_string: String) -> Self {
        Self {
            connection_string,
            is_healthy: RwLock::new(true),
        }
    }

    #[instrument(skip(self), level = "info")]
    pub async fn connect(&self) -> Result<ManagedConnection, OnConnectError> {
        let config =
            Config::from_ado_string(&self.connection_string).map_err(OnConnectError::Config)?;

        info!(config = ?config, "Connecting");
        let tcp = TcpStream::connect(config.get_addr()).await?;
        tcp.set_nodelay(true)?;

        // To be able to use Tokio's tcp, we're using the `compat_write` from
        // the `TokioAsyncWriteCompatExt` to get a stream compatible with the
        // traits from the `futures` crate.
        let client = Client::connect(config, tcp.compat_write())
            .await
            .map_err(OnConnectError::OnConnect)?;
        info!("Successfully connected");

        Ok(ManagedConnection(client))
    }

    /// Mark the pool as unhealthy. This is used to indicate that a connection should be replaced.
    pub async fn set_unhealthy(&self) {
        let mut healthy = self.is_healthy.write().expect("poisoned is_healthy lock");
        *healthy = false;
    }
}

type Stream = tokio_util::compat::Compat<TcpStream>;
pub struct ManagedConnection(Client<Stream>);

// Transparently forward methods to the inner Client
impl ManagedConnection {
    #[instrument(skip(self, params), level = "debug")]
    pub async fn execute(
        &mut self,
        sql: &str,
        params: &[&(dyn tiberius::ToSql)],
    ) -> Result<tiberius::ExecuteResult, tiberius::error::Error> {
        debug!("Executing command");
        self.0.execute(sql, params).await
    }

    #[instrument(skip(self, params), level = "debug")]
    pub async fn query<'a>(
        &'a mut self,
        sql: &str,
        params: &[&(dyn tiberius::ToSql)],
    ) -> Result<tiberius::QueryStream<'a>, tiberius::error::Error> {
        debug!("Executing query");
        self.0.query(sql, params).await
    }

    #[instrument(skip(self), level = "debug")]
    pub async fn simple_query<'a>(
        &'a mut self,
        sql: &str,
    ) -> Result<tiberius::QueryStream<'a>, tiberius::error::Error> {
        debug!("Executing simple query");
        self.0.simple_query(sql).await
    }

    #[instrument(skip(self), level = "debug")]
    pub async fn bulk_insert<'a>(
        &'a mut self,
        table: &'a str,
    ) -> Result<tiberius::BulkLoadRequest<'a, Stream>, tiberius::error::Error> {
        debug!(%table, "Starting bulk insert");
        self.0.bulk_insert(&table).await
    }

    async fn ping(&mut self) -> Result<i32, tiberius::error::Error> {
        let row = self
            .0
            .simple_query("SELECT 1")
            .await?
            .into_first_result()
            .await?;
        debug!(?row, "Ping response");
        let value = row[0].get(0).expect("value is present");
        Ok(value)
    }
}

#[derive(thiserror::Error, Debug)]
pub enum PoolError {
    #[error("Connection error: {0}")]
    Connection(#[from] tiberius::error::Error),
    #[error("On Connect error: {0}")]
    OnConnect(#[source] OnConnectError),
    #[error("Unexpected ping response: {0}")]
    Ping(i32),
}

impl ManageConnection for ConnectionManager {
    type Connection = ManagedConnection;

    type Error = PoolError;

    async fn connect(&self) -> Result<Self::Connection, Self::Error> {
        self.connect().await.map_err(PoolError::OnConnect)
    }

    async fn is_valid(&self, conn: &mut Self::Connection) -> Result<(), Self::Error> {
        match conn.ping().await {
            Ok(v) if v == 1 => Ok(()),
            Ok(v) => Err(PoolError::Ping(v)),
            Err(e) => Err(PoolError::Connection(e)),
        }
    }

    fn has_broken(&self, _conn: &mut Self::Connection) -> bool {
        self.is_healthy
            .read()
            .expect("poisoned is_healthy lock")
            .clone()
    }
}
