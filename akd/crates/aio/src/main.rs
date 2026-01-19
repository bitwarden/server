//! AIO process for an AKD. Spins up multiple async tasks to handle publisher and reader roles.
//! Requires both read and write permissions to the underlying data stores.
//! There should only be one instance of this running at a time for a given AKD.

use tracing_subscriber::EnvFilter;

#[tokio::main]
#[allow(unreachable_code)]
async fn main() {
    let env_filter = EnvFilter::builder()
        .with_default_directive(tracing::level_filters::LevelFilter::INFO.into())
        .from_env_lossy();

    tracing_subscriber::fmt().with_env_filter(env_filter).init();

    // Load config and convert to publisher and reader configs
    todo!();

    // Start publisher task
    todo!();

    // Start reader task
    todo!();
}
