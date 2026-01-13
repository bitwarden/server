use ms_database::{IntoRow, TokenRow};
use tracing::{debug, error};
use uuid::Uuid;

use crate::{
    ms_sql::{
        migrations::TABLE_PUBLISH_QUEUE,
        sql_params::SqlParams,
        tables::{
            akd_storable_for_ms_sql::{QueryStatement, Statement},
            temp_table::TempTable,
        },
    },
    publish_queue::{PublishQueueError, PublishQueueItem},
};

pub fn enqueue_statement(raw_label: Vec<u8>, raw_value: Vec<u8>) -> Statement {
    debug!("Building enqueue_statement for publish queue");
    let mut params = SqlParams::new();
    params.add("id", Box::new(uuid::Uuid::now_v7()));
    params.add("raw_label", Box::new(raw_label));
    params.add("raw_value", Box::new(raw_value));

    let sql = format!(
        r#"
        INSERT INTO {}
        (id, raw_label, raw_value)
        VALUES ({}, {}, {})"#,
        TABLE_PUBLISH_QUEUE,
        params
            .key_for("id")
            .expect("id was added to the params list"),
        params
            .key_for("raw_label")
            .expect("raw_label was added to the params list"),
        params
            .key_for("raw_value")
            .expect("raw_value was added to the params list"),
    );
    Statement::new(sql, params)
}

pub fn peek_statement(limit: isize) -> QueryStatement<PublishQueueItem, PublishQueueError> {
    debug!("Building peek_statement for publish queue");
    let sql = format!(
        r#"
        SELECT TOP {} id, raw_label, raw_value
        FROM {}
        ORDER BY id ASC"#,
        limit, TABLE_PUBLISH_QUEUE
    );
    QueryStatement::new(sql, SqlParams::new(), |row: &ms_database::Row| {
        let id: uuid::Uuid = row.get("id").ok_or_else(|| {
            error!("id is NULL or missing in publish queue row");
            PublishQueueError
        })?;
        let raw_label: &[u8] = row.get("raw_label").ok_or_else(|| {
            error!("raw_label is NULL or missing in publish queue row");
            PublishQueueError
        })?;
        let raw_value: &[u8] = row.get("raw_value").ok_or_else(|| {
            error!("raw_value is NULL or missing in publish queue row");
            PublishQueueError
        })?;

        Ok(PublishQueueItem {
            id,
            raw_label: raw_label.to_vec(),
            raw_value: raw_value.to_vec(),
        })
    })
}

pub fn bulk_delete_rows(ids: &'_ [Uuid]) -> Result<Vec<TokenRow<'_>>, PublishQueueError> {
    debug!("Building bulk_delete_rows for publish queue");
    let mut rows = Vec::new();
    for id in ids {
        let row = (id.clone()).into_row();
        rows.push(row);
    }
    Ok(rows)
}

pub fn bulk_delete_statement(temp_table_name: &str) -> Statement {
    debug!("Building bulk_delete_statement deleting ids in temp table from the publish queue");
    let sql = format!(
        r#"
        DELETE pq
        FROM {} pq
        INNER JOIN {} temp ON pq.id = temp.id
        "#,
        TABLE_PUBLISH_QUEUE, temp_table_name
    );
    Statement::new(sql, SqlParams::new())
}

// pub fn delete_statement(ids: Vec<uuid::Uuid>) -> Statement {
//     debug!("Building delete_statement for publish queue");
//     let mut params = SqlParams::new();
//     let mut id_placeholders = Vec::new();

//     for (i, id) in ids.iter().enumerate() {
//         let param_name = format!("id_{}", i);
//         params.add(&param_name, Box::new(*id));
//         id_placeholders.push(params.key_for(&param_name).expect("id was added to params"));
//     }

//     let sql = format!(
//         r#"
//         DELETE FROM {}
//         WHERE id IN ({})"#,
//         TABLE_PUBLISH_QUEUE,
//         id_placeholders.join(", ")
//     );
//     Statement::new(sql, params)
// }
