-- 0001_Extensions.sql — Wave-0 base script.
-- Enables PgVector when available. Wrapped in a DO block so a missing pgvector binary
-- logs a NOTICE and continues instead of killing Phase-1 bring-up; the RAG cluster's
-- scripts must re-check for the extension before creating vector columns.
DO $$
BEGIN
    CREATE EXTENSION IF NOT EXISTS vector;
    RAISE NOTICE 'pgvector extension is installed.';
EXCEPTION
    WHEN OTHERS THEN
        RAISE NOTICE 'pgvector extension unavailable, continuing without it: %', SQLERRM;
END
$$;
