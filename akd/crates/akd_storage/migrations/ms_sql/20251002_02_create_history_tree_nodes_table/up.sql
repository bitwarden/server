IF OBJECT_ID('dbo.akd_history_tree_nodes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.akd_history_tree_nodes (
        label_len INT NOT NULL CHECK (label_len >= 0),
        label_val VARBINARY(32) NOT NULL,
        last_epoch BIGINT NOT NULL CHECK (last_epoch >= 0),
        least_descendant_ep BIGINT NOT NULL CHECK (least_descendant_ep >= 0),
        parent_label_len INT NOT NULL CHECK (parent_label_len >= 0),
        parent_label_val VARBINARY(32) NOT NULL,
        node_type SMALLINT NOT NULL CHECK (node_type >= 0),
        left_child_len INT NULL CHECK (left_child_len IS NULL OR left_child_len >= 0),
        left_child_label_val VARBINARY(32) NULL,
        right_child_len INT NULL CHECK (right_child_len IS NULL OR right_child_len >= 0),
        right_child_label_val VARBINARY(32) NULL,
        [hash] VARBINARY(32) NOT NULL,
        p_last_epoch BIGINT NULL CHECK (p_last_epoch IS NULL OR p_last_epoch >= 0),
        p_least_descendant_ep BIGINT NULL CHECK (p_least_descendant_ep IS NULL OR p_least_descendant_ep >= 0),
        p_parent_label_len INT NULL CHECK (p_parent_label_len IS NULL OR p_parent_label_len >= 0),
        p_parent_label_val VARBINARY(32) NULL,
        p_node_type SMALLINT NULL CHECK (p_node_type IS NULL OR p_node_type >= 0),
        p_left_child_len INT NULL CHECK (p_left_child_len IS NULL OR p_left_child_len >= 0),
        p_left_child_label_val VARBINARY(32) NULL,
        p_right_child_len INT NULL CHECK (p_right_child_len IS NULL OR p_right_child_len >= 0),
        p_right_child_label_val VARBINARY(32) NULL,
        p_hash VARBINARY(32) NULL,
        PRIMARY KEY (label_len, label_val)
    );
END
