-- Public rooms: anyone can browse open lobbies (Among Us style). The room mirrors its live player count here so the
-- list is one SQL query, not a fan-out to every Durable Object.
ALTER TABLE rooms ADD COLUMN is_public INTEGER NOT NULL DEFAULT 0;
ALTER TABLE rooms ADD COLUMN players INTEGER NOT NULL DEFAULT 0;
ALTER TABLE rooms ADD COLUMN host_name TEXT;
ALTER TABLE rooms ADD COLUMN updated_at INTEGER NOT NULL DEFAULT 0;
CREATE INDEX idx_rooms_public ON rooms(is_public, state, updated_at);
