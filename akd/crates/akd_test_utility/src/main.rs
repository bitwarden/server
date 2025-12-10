use akd::ecvrf::HardCodedAkdVRF;
use akd::storage::StorageManager;
use akd::Directory;
use akd_storage::DatabaseType;
use akd_storage::db_config::DbConfig;
use anyhow::{Context, Result};
use clap::{Parser, ValueEnum};
use commands::Command;
use rand::distributions::Alphanumeric;
use rand::rngs::StdRng;
use rand::{Rng, SeedableRng};
use std::io::*;
use std::time::{Duration, Instant};
use tokio::sync::mpsc::*;
use tokio::time::timeout;
use tracing::{debug, error, info, warn};
use tracing_subscriber::Layer;

mod commands;
mod directory_host;

type TC = akd::ExperimentalConfiguration<akd::ExampleLabel>;

#[derive(ValueEnum, Clone, Debug)]
enum LogLevel {
    Error,
    Warn,
    Info,
    Debug,
    Trace,
}

impl LogLevel {
    fn to_tracing_level(&self) -> tracing::Level {
        match self {
            LogLevel::Error => tracing::Level::ERROR,
            LogLevel::Warn => tracing::Level::WARN,
            LogLevel::Info => tracing::Level::INFO,
            LogLevel::Debug => tracing::Level::DEBUG,
            LogLevel::Trace => tracing::Level::TRACE,
        }
    }
}

#[derive(Parser, Debug, Clone)]
enum Mode {
    #[clap(about = "Benchmark publish API")]
    BenchPublish {
        num_users: u64,
        num_updates_per_user: u64,
    },
    #[clap(about = "Benchmark lookup API")]
    BenchLookup {
        num_users: u64,
        num_lookups_per_user: u64,
    },
    #[clap(about = "Benchmark database insertion")]
    BenchDbInsert { num_users: u64 },
    #[clap(about = "Run database migrations")]
    Migrate,
    #[clap(about = "Drop all database tables")]
    Drop,
    #[clap(about = "Clean database (drop and recreate)")]
    Clean,
}

#[derive(Parser, Debug, Clone)]
#[clap(name = "akd-test-utility", about = "AKD MS SQL test utility and benchmark tool")]
struct CliArgs {
    /// Database connection string (also reads from AKD_MSSQL_CONNECTION_STRING env var)
    #[clap(long = "connection-string", short = 'c')]
    connection_string: Option<String>,

    /// Log level
    #[clap(
        value_enum,
        long = "log-level",
        short = 'l',
        default_value = "info",
        help = "Set the log level"
    )]
    log_level: LogLevel,

    /// Optional log file path (suppresses console logging when specified)
    #[clap(long = "log-file", short = 'f', help = "Write logs to file (suppresses console output)")]
    log_file: Option<String>,

    /// Connection pool size
    #[clap(
        long = "pool-size",
        short = 'p',
        default_value = "10",
        help = "Database connection pool size"
    )]
    pool_size: u32,

    #[clap(subcommand)]
    mode: Option<Mode>,
}

#[tokio::main]
async fn main() -> Result<()> {
    let args = CliArgs::parse();

    // Get connection string from CLI or env var
    let connection_string = args
        .connection_string
        .clone()
        .or_else(|| std::env::var("AKD_MSSQL_CONNECTION_STRING").ok())
        .context("Connection string required via --connection-string or AKD_MSSQL_CONNECTION_STRING env var")?;

    // Initialize logging
    let mut layers = Vec::new();

    // If a log file is specified, only log to file
    // Otherwise, log to console
    if let Some(ref log_file) = args.log_file {
        let file = std::fs::File::create(log_file)
            .with_context(|| format!("Failed to create log file: {log_file}"))?;
        let file_layer = tracing_subscriber::fmt::layer()
            .with_writer(file)
            .with_ansi(false)
            .with_target(true)
            .with_level(true);
        layers.push(file_layer.boxed());

        // Print a one-time message about logging to file
        eprintln!("Logging to file: {log_file}");
    } else {
        // Console logging with colors (only when no file specified)
        let console_layer = tracing_subscriber::fmt::layer()
            .with_writer(std::io::stdout)
            .with_ansi(true)
            .with_target(true)
            .with_level(true);
        layers.push(console_layer.boxed());
    }

    use tracing_subscriber::layer::SubscriberExt;
    use tracing_subscriber::util::SubscriberInitExt;

    tracing_subscriber::registry()
        .with(tracing_subscriber::EnvFilter::try_from_default_env()
            .unwrap_or_else(|_| {
                tracing_subscriber::EnvFilter::new(args.log_level.to_tracing_level().as_str())
            }))
        .with(layers)
        .init();

    info!("Starting AKD test utility");

    // Create database connection
    info!("Connecting to MS SQL database");
    let config = DbConfig::MsSql { connection_string, pool_size: args.pool_size };
    let db = config.connect().await.context("Failed to connect to database")?;
    
    // Handle pre-processing modes
    if let Some(()) = pre_process_mode(&args, &db).await? {
        return Ok(());
    }

    let storage_manager = StorageManager::new(db.clone(), None, None, None);
    let vrf = HardCodedAkdVRF {};
    let mut directory = Directory::<TC, _, _>::new(storage_manager.clone(), vrf)
        .await
        .context("Failed to create AKD directory")?;

    let (tx, mut rx) = channel(2);

    tokio::spawn(async move {
        directory_host::init_host::<TC, _, HardCodedAkdVRF>(&mut rx, &mut directory).await
    });

    process_mode(&args, &tx, &db).await?;

    Ok(())
}

// Process modes that run before creating the directory
async fn pre_process_mode(
    args: &CliArgs,
    db: &DatabaseType,
) -> Result<Option<()>> {
    match (db, &args.mode) {
        (DatabaseType::MsSql(db), Some(Mode::Drop)) => {
            info!("Dropping database tables");
            db.drop().await.context("Failed to drop tables")?;
            info!("Tables dropped successfully");
            return Ok(Some(()));
        }
        (DatabaseType::MsSql(db), Some(Mode::Migrate)) => {
            info!("Running database migrations");
            db.migrate().await.context("Failed to run migrations")?;
            info!("Migrations completed successfully");
            return Ok(Some(()));
        }
        (DatabaseType::MsSql(db), Some(Mode::Clean)) => {
            info!("Cleaning database (drop + migrate)");
            db.drop().await.context("Failed to drop tables")?;
            info!("Tables dropped");
            db.migrate().await.context("Failed to run migrations")?;
            info!("Migrations completed - database is clean");
            return Ok(Some(()));
        }
        _ => {}
    }
    Ok(None)
}

async fn process_mode(
    args: &CliArgs,
    tx: &Sender<directory_host::Rpc>,
    db: &DatabaseType,
) -> Result<()> {
    if let Some(mode) = &args.mode {
        match mode {
            Mode::BenchDbInsert { num_users } => {
                bench_db_insert(*num_users, db).await?;
            }
            Mode::BenchPublish {
                num_users,
                num_updates_per_user,
            } => {
                bench_publish(*num_users, *num_updates_per_user, tx).await?;
            }
            Mode::BenchLookup {
                num_users,
                num_lookups_per_user,
            } => {
                bench_lookup(*num_users, *num_lookups_per_user, tx).await?;
            }
            Mode::Drop | Mode::Migrate | Mode::Clean => {
                // Already handled in pre_process_mode
            }
        }
    } else {
        // REPL mode
        repl_loop(args, tx, db).await?;
    }

    // Shutdown directory host
    let _ = tx
        .send(directory_host::Rpc(
            directory_host::DirectoryCommand::Terminate,
            None,
        ))
        .await;

    Ok(())
}

async fn bench_db_insert(num_users: u64, db: &DatabaseType) -> Result<()> {
    use owo_colors::OwoColorize;

    println!("{}", "======= Benchmark operation requested =======".cyan());
    println!("Beginning DB INSERT benchmark of {num_users} users");

    let mut values: Vec<String> = vec![];
    for i in 0..num_users {
        values.push(
            StdRng::seed_from_u64(i)
                .sample_iter(&Alphanumeric)
                .take(30)
                .map(char::from)
                .collect(),
        );
    }

    let mut data = Vec::new();
    for value in values.iter() {
        let state = akd::storage::types::DbRecord::build_user_state(
            value.as_bytes().to_vec(),
            value.as_bytes().to_vec(),
            1u64,
            1u32,
            [1u8; 32],
            1u64,
        );
        data.push(akd::storage::types::DbRecord::ValueState(state));
    }

    debug!("Starting storage request");
    let tic = Instant::now();
    let len = data.len();

    use akd::storage::Database;
    db.batch_set(data, akd::storage::DbSetState::General)
        .await
        .context("Failed to batch insert records")?;

    let toc: Duration = Instant::now() - tic;
    println!(
        "{}",
        format!("Insert batch of {} items in {} ms", len, toc.as_millis()).green()
    );

    Ok(())
}

async fn bench_publish(
    num_users: u64,
    num_updates_per_user: u64,
    tx: &Sender<directory_host::Rpc>,
) -> Result<()> {
    use owo_colors::OwoColorize;

    println!("{}", "======= Benchmark operation requested =======".cyan());
    println!(
        "Beginning PUBLISH benchmark of {num_users} users with {num_updates_per_user} updates/user"
    );

    let users: Vec<String> = (1..=num_users)
        .map(|i| {
            StdRng::seed_from_u64(i)
                .sample_iter(&Alphanumeric)
                .take(256)
                .map(char::from)
                .collect()
        })
        .collect();

    let data: Vec<String> = (1..=num_updates_per_user)
        .map(|i| {
            StdRng::seed_from_u64(i)
                .sample_iter(&Alphanumeric)
                .take(1024)
                .map(char::from)
                .collect()
        })
        .collect();

    let tic = Instant::now();
    let mut code = None;

    for value in data {
        let user_data: Vec<(String, String)> = users
            .iter()
            .map(|user| (user.clone(), value.clone()))
            .collect();

        let (rpc_tx, rpc_rx) = tokio::sync::oneshot::channel();
        let rpc = directory_host::Rpc(
            directory_host::DirectoryCommand::PublishBatch(user_data),
            Some(rpc_tx),
        );

        if tx.send(rpc).await.is_err() {
            error!("Error sending message to directory");
            continue;
        }

        match rpc_rx.await {
            Err(err) => code = Some(format!("{err}")),
            Ok(Err(dir_err)) => code = Some(dir_err),
            Ok(Ok(msg)) => info!("{}", msg),
        }

        if code.is_some() {
            break;
        }
    }

    if let Some(err) = code {
        error!("Benchmark operation error: {}", err);
    } else {
        let toc = tic.elapsed();
        println!(
            "{}",
            format!(
                "Benchmark output: Inserted {} users with {} updates/user\n\
                Execution time: {} ms\n\
                Time-per-user (avg): {} µs\n\
                Time-per-op (avg): {} µs",
                num_users,
                num_updates_per_user,
                toc.as_millis(),
                toc.as_micros() / num_users as u128,
                toc.as_micros() / num_users as u128 / num_updates_per_user as u128
            )
            .green()
        );
    }

    Ok(())
}

async fn bench_lookup(
    num_users: u64,
    num_lookups_per_user: u64,
    tx: &Sender<directory_host::Rpc>,
) -> Result<()> {
    use owo_colors::OwoColorize;

    println!("{}", "======= Benchmark operation requested =======".cyan());
    println!(
        "Beginning LOOKUP benchmark of {num_users} users with {num_lookups_per_user} lookups/user"
    );

    let user_data: Vec<(String, String)> = (1..=num_users)
        .map(|i| {
            (
                StdRng::seed_from_u64(i)
                    .sample_iter(&Alphanumeric)
                    .take(256)
                    .map(char::from)
                    .collect(),
                StdRng::seed_from_u64(i)
                    .sample_iter(&Alphanumeric)
                    .take(1024)
                    .map(char::from)
                    .collect(),
            )
        })
        .collect();

    info!("Inserting {} users", num_users);
    let (rpc_tx, _) = tokio::sync::oneshot::channel();
    let rpc = directory_host::Rpc(
        directory_host::DirectoryCommand::PublishBatch(user_data.clone()),
        Some(rpc_tx),
    );
    let _ = tx.send(rpc).await;

    let tic = Instant::now();
    let mut code = None;

    for i in 1..=num_lookups_per_user {
        for (user, _) in &user_data {
            let (rpc_tx, rpc_rx) = tokio::sync::oneshot::channel();
            let rpc = directory_host::Rpc(
                directory_host::DirectoryCommand::Lookup(String::from(user)),
                Some(rpc_tx),
            );

            if tx.send(rpc).await.is_err() {
                error!("Error sending message to directory");
                continue;
            }

            match rpc_rx.await {
                Err(err) => code = Some(format!("{err}")),
                Ok(Err(dir_err)) => code = Some(dir_err),
                Ok(Ok(_)) => {}
            }

            if code.is_some() {
                break;
            }
        }
        info!("LOOKUP of {} users complete (iteration {})", num_users, i);
    }

    if let Some(err) = code {
        error!("Benchmark operation error: {}", err);
    } else {
        let toc = tic.elapsed();
        println!(
            "{}",
            format!(
                "Benchmark output: Looked up and verified {} users with {} lookups/user\n\
                Execution time: {} ms\n\
                Time-per-user (avg): {} µs\n\
                Time-per-op (avg): {} µs",
                num_users,
                num_lookups_per_user,
                toc.as_millis(),
                toc.as_micros() / num_users as u128,
                toc.as_micros() / num_users as u128 / num_lookups_per_user as u128
            )
            .green()
        );
    }

    Ok(())
}

async fn repl_loop(
    args: &CliArgs,
    tx: &Sender<directory_host::Rpc>,
    db: &DatabaseType,
) -> Result<()> {
    loop {
        println!("Please enter a command");
        print!("> ");
        stdout().flush()?;

        let mut line = String::new();
        stdin().read_line(&mut line)?;

        match (db, Command::parse(&mut line)) {
            (_, Command::Unknown(other)) => {
                println!("Input '{other}' is not supported, enter 'help' for the help menu")
            }
            (_,Command::InvalidArgs(message)) => println!("Invalid arguments: {message}"),
            (_, Command::Exit) => {
                info!("Exiting...");
                break;
            }
            (_, Command::Help) => {
                Command::print_help_menu();
            }
            (DatabaseType::MsSql(db), Command::Clean) => {
                println!("Cleaning the database (drop + migrate)...");
                match db.drop().await {
                    Ok(_) => {
                        info!("Tables dropped");
                        match db.migrate().await {
                            Ok(_) => {
                                println!("Database cleaned successfully");
                            }
                            Err(error) => {
                                println!("Error running migrations: {error}");
                            }
                        }
                    }
                    Err(error) => {
                        println!("Error dropping tables: {error}");
                    }
                }
            }
            (_, Command::Clean) => {
                println!("Clean command is only supported for MS SQL databases");
            }
            (_, Command::Directory(cmd)) => {
                let (rpc_tx, rpc_rx) = tokio::sync::oneshot::channel();
                let rpc = directory_host::Rpc(cmd, Some(rpc_tx));

                if tx.send(rpc).await.is_err() {
                    warn!("Error sending message to directory");
                    continue;
                }

                match timeout(Duration::from_secs(30), rpc_rx).await {
                    Ok(Ok(Ok(success))) => {
                        println!("Response: {success}");
                    }
                    Ok(Ok(Err(dir_err))) => {
                        error!("Error in directory processing command: {}", dir_err);
                    }
                    Ok(Err(_)) => {
                        error!("Failed to receive result from directory");
                    }
                    Err(_) => {
                        warn!("Timeout waiting on receive from directory");
                    }
                }
            }
        }
    }

    Ok(())
}
