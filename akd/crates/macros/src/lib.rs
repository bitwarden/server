//! SQL Migration Macros
//!
//! This crate provides procedural macros for managing SQL migrations at compile time.
//!
//! # Overview
//!
//! The macros in this crate are inspired by Diesel's `embed_migrations!` macro and
//! enable embedding SQL migration files directly into your Rust binary at compile time.
//! 
//! It is up to the caller to ensure that a `Migration` struct is in scope and defines an accessible `new` constructor.
//!
//! # Migration Name Requirements
//!
//! Migration directory names must:
//! - Be valid UTF-8
//! - Not exceed 50 characters (to fit in a VARCHAR(50) database column)
//!
//! # Available Macros
//!
//! - [`migration!`] - Load a single migration from a directory
//! - [`load_migrations!`] - Automatically load all migrations from a directory
//!
//! # Usage Example
//!
//! ```rust
//! use ms_database::Migration;
//! use macros::{migration, load_migrations};
//!
//! // Load a single migration
//! let single_migration: Migration = migration!("./example_migrations/20250930_01_initial");
//!
//! // Load all migrations from a directory
//! const ALL_MIGRATIONS: &[Migration] = load_migrations!("./example_migrations");
//! ```

use proc_macro::TokenStream;
use quote::quote;
use std::fs;
use std::path::Path;
use syn::{parse_macro_input, LitStr};

/// Resolves a directory path by walking up to find the workspace root (first Cargo.toml).
/// All paths are resolved relative to the workspace root.
fn resolve_path(path_str: &str) -> std::path::PathBuf {
    // Try to get the current crate directory from CARGO_MANIFEST_DIR
    let start_dir = if let Ok(crate_dir) = std::env::var("CARGO_MANIFEST_DIR") {
        std::path::PathBuf::from(crate_dir)
    } else {
        // CARGO_MANIFEST_DIR not set (e.g., in trybuild tests)
        // Start from the current directory and walk up
        std::env::current_dir().expect("Could not determine current directory")
    };

    let mut current = start_dir.as_path();

    // Walk up to find the first Cargo.toml (workspace root)
    loop {
        if current.join("Cargo.toml").exists() {
            return current.join(path_str);
        }

        // Move to parent directory
        match current.parent() {
            Some(parent) => current = parent,
            None => {
                // Reached filesystem root without finding Cargo.toml
                panic!(
                    "Could not find Cargo.toml in any parent directory starting from {}",
                    start_dir.display()
                );
            }
        }
    }
}

/// Helper function to load a migration from a directory path.
/// Returns a tuple of (migration_name, up_content, down_content_tokens).
fn load_migration_from_path(full_path: &Path, relative_path: &str) -> (String, String, proc_macro2::TokenStream) {
    // Get the migration name from the directory name
    let migration_name = full_path
        .file_name()
        .expect("Invalid directory path")
        .to_str()
        .expect("Invalid UTF-8 in directory name")
        .to_string();

    // Validate that the migration name fits in a varchar(50)
    if migration_name.len() > 50 {
        panic!(
            "Migration name '{}' exceeds 50 characters (length: {})",
            migration_name,
            migration_name.len()
        );
    }

    // Read up.sql (required)
    let up_sql_path = full_path.join("up.sql");
    if !up_sql_path.exists() {
        panic!(
            "Required file 'up.sql' not found in migration directory: {}",
            full_path.display()
        );
    }
    let up_content = fs::read_to_string(&up_sql_path)
        .unwrap_or_else(|e| panic!("Failed to read up.sql in {relative_path}: {e}"));

    // Read down.sql (optional)
    let down_sql_path = full_path.join("down.sql");
    let down_content = if down_sql_path.exists() {
        let content = fs::read_to_string(&down_sql_path)
            .unwrap_or_else(|e| panic!("Failed to read down.sql in {relative_path}: {e}"));
        quote! { Some(#content) }
    } else {
        quote! { None }
    };

    (migration_name, up_content, down_content)
}

/// Loads a single migration from a directory.
///
/// The directory must contain:
/// - `up.sql` (required) - SQL to apply the migration
/// - `down.sql` (optional) - SQL to roll back the migration
///
/// # Arguments
///
/// * `path` - A string literal containing the relative path to the migration directory
///
/// # Returns
///
/// A `Migration` struct with:
/// - `name`: The directory name (must fit in VARCHAR(50))
/// - `up`: The contents of `up.sql`
/// - `down`: `Some(contents)` if `down.sql` exists, `None` otherwise
/// - `run_in_transaction`: Always `true`
///
/// # Usage
///
/// ```rust
/// use ms_database::Migration;
/// use macros::migration;
///
/// // This will load the migration at compile time
/// let migration: Migration = migration!("./example_migrations/20250930_01_initial");
/// assert_eq!(migration.name, "20250930_01_initial");
/// assert!(migration.run_in_transaction);
/// ```
///
/// # Generated Code
///
/// This macro generates code that creates a `Migration` struct:
///
/// ```text
/// // Given a directory structure:
/// // example_migrations/20250930_01_initial/
/// //   ├── up.sql    (contains: "CREATE TABLE users (id INT PRIMARY KEY);")
/// //   └── down.sql  (contains: "DROP TABLE users;")
///
/// // The macro call:
/// migration!("./example_migrations/20250930_01_initial")
///
/// // Expands to:
/// Migration {
///     name: #migration_name,
///     up: #up_content,
///     down: #down_content,
///     run_in_transaction: true,
/// }
/// ```
///
/// # Panics
///
/// - If the migration name exceeds 50 characters
/// - If `up.sql` is not found
/// - If any file cannot be read
#[proc_macro]
pub fn migration(input: TokenStream) -> TokenStream {
    // Parse the input as a string literal (the directory path)
    let dir_path = parse_macro_input!(input as LitStr).value();

    // Resolve the path (supports both absolute and relative paths)
    let full_path = resolve_path(&dir_path);

    // Load the migration using the helper function
    let (migration_name, up_content, down_content) = load_migration_from_path(&full_path, &dir_path);

    // Generate the Migration struct
    let expanded = quote! {
        Migration {
            name: #migration_name,
            up: #up_content,
            down: #down_content,
            run_in_transaction: true,
        }
    };

    expanded.into()
}

/// Automatically loads all migrations from a directory.
///
/// Scans the specified directory for subdirectories containing migration files.
/// Each subdirectory must contain at least an `up.sql` file to be considered
/// a valid migration. Directories are processed in alphabetical order to ensure
/// consistent migration ordering.
///
/// # Arguments
///
/// * `path` - A string literal containing the relative path to the migrations directory
///
/// # Returns
///
/// A static reference to an array of `Migration` structs: `&[Migration]`
///
/// # Directory Structure
///
/// ```text
/// migrations/
/// ├── 20250930_01_initial/
/// │   └── up.sql
/// ├── 20250930_02_add_users/
/// │   ├── up.sql
/// │   └── down.sql
/// └── 20250930_03_add_permissions/
///     ├── up.sql
///     └── down.sql
/// ```
///
/// # Usage
///
/// ```rust
/// use ms_database::Migration;
/// use macros::load_migrations;
///
/// // This will load all migrations at compile time
/// const MIGRATIONS: &[Migration] = load_migrations!("./example_migrations");
///
/// // Migrations are loaded in alphabetical order
/// for migration in MIGRATIONS {
///     println!("Migration: {}", migration.name);
/// }
/// ```
///
/// # Generated Code
///
/// This macro generates code that creates an array of `Migration` structs:
///
/// ```text
/// // Given a directory structure:
/// // migrations/
/// //   ├── 20250930_01_initial/
/// //   │   └── up.sql
/// //   ├── 20250930_02_add_users/
/// //   │   ├── up.sql
/// //   │   └── down.sql
/// //   └── 20250930_03_add_permissions/
/// //       └── up.sql
///
/// // The macro call:
/// const MIGRATIONS: &[Migration] = load_migrations!("./migrations");
///
/// // Expands to:
/// &[
///     Migration {
///         name: "20250930_01_initial",
///         up: "...",
///         down: None,
///         run_in_transaction: true,
///     },
///     Migration {
///         name: "20250930_02_add_users",
///         up: "...",
///         down: Some("..."),
///         run_in_transaction: true,
///     },
///     Migration {
///         name: "20250930_03_add_permissions",
///         up: "...",
///         down: None,
///         run_in_transaction: true,
///     },
/// ]
/// ```
///
/// # Panics
///
/// - If the migrations directory does not exist
/// - If the migrations directory cannot be read
/// - If any migration name exceeds 50 characters
/// - If any required `up.sql` file is missing
#[proc_macro]
pub fn load_migrations(input: TokenStream) -> TokenStream {
    // Parse the input as a string literal (the migrations directory path)
    let migrations_dir = parse_macro_input!(input as LitStr).value();

    // Resolve the path (supports both absolute and relative paths)
    let migrations_path = resolve_path(&migrations_dir);

    if !migrations_path.exists() || !migrations_path.is_dir() {
        panic!(
            "Migrations directory not found: {}",
            migrations_path.display()
        );
    }

    // Read all directories in the migrations directory
    let mut migration_paths = Vec::new();
    for entry in fs::read_dir(&migrations_path)
        .unwrap_or_else(|e| panic!("Failed to read migrations directory: {e}"))
    {
        let entry = entry.unwrap();
        let path = entry.path();

        if path.is_dir() {
            // Check if it has up.sql
            if path.join("up.sql").exists() {
                migration_paths.push(path);
            }
        }
    }

    // Sort the migrations by name to ensure consistent ordering
    migration_paths.sort();

    // Generate Migration structs inline for each directory
    let migrations: Vec<_> = migration_paths
        .iter()
        .map(|full_path| {
            // Use the directory name as the display path for error messages
            let display_path = full_path
                .file_name()
                .and_then(|n| n.to_str())
                .unwrap_or("unknown");

            // Load the migration using the helper function
            let (migration_name, up_content, down_content) = load_migration_from_path(full_path, display_path);

            quote! {
                Migration {
                    name: #migration_name,
                    up: #up_content,
                    down: #down_content,
                    run_in_transaction: true,
                }
            }
        })
        .collect();

    // Generate the static array
    let expanded = quote! {
        &[#(#migrations),*]
    };

    expanded.into()
}
