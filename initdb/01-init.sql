-- initdb/01-init.sql
-- Выполняется один раз при первой инициализации тома postgres_data.

DO
$$
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'contact_type') THEN
            CREATE TYPE contact_type AS ENUM ('Email', 'Phone');
        END IF;
    END
$$;