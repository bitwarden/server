# Bitwarden MSSQL Database Migrator Utility

A command-line utility for performing MSSQL database migrations for Bitwarden's self-hosted and cloud deployments.

## Overview

The MSSQL Migrator Utility is a specialized tool that leverages the [Migrator library](../Migrator) to handle MSSQL database migrations. The utility uses [DbUp](https://dbup.github.io/) to handle the execution and tracking of database migrations. It runs SQL scripts in order, tracking which scripts have been executed to avoid duplicate runs.

## Features

- Command-line interface for executing database migrations
- Integration with DbUp for reliable migration management
- Execution inside or outside of transactions for different application scenarios
- Script execution tracking to prevent duplicate migrations and support retries

See the [documentation](https://contributing.bitwarden.com/getting-started/server/database/mssql/#updating-the-database) for usage.
