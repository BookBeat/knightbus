-- create the knightbus schema
CREATE SCHEMA IF NOT EXISTS knightbus;

-- create metadata table
CREATE TABLE knightbus.meta (
    queue_name VARCHAR UNIQUE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT now() NOT NULL
);

-- create a queue
CREATE TABLE IF NOT EXISTS knightbus.q_my_queue (
    message_id BIGINT PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
    read_count INT DEFAULT 0 NOT NULL,
    enqueued_at TIMESTAMP WITH TIME ZONE DEFAULT now() NOT NULL,
    visibility_timeout TIMESTAMP WITH TIME ZONE NOT NULL,
    message JSONB,
    properties JSONB
);

-- create index
CREATE INDEX IF NOT EXISTS knightbus_q_my_queue_visibility_timeout_idx ON knightbus.q_my_queue (visibility_timeout ASC);

-- create a dl queue
	CREATE TABLE IF NOT EXISTS knightbus.dlq_my_queue (
    message_id BIGINT PRIMARY KEY,
    enqueued_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT now() NOT NULL,
    message JSONB,
    properties JSONB
);