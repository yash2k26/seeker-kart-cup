-- One row per entry payment. A player may pay, leave the lobby for a refund and pay again in the same round, so
-- (code, round, wallet) is not unique; the payment is. Keyed by signature, this table is what stops one on-chain
-- payment buying two seats, in any room, ever (room.ts checkEntry).
CREATE TABLE room_entries_v2 (
  signature  TEXT PRIMARY KEY,
  code       TEXT NOT NULL,
  round      INTEGER NOT NULL,
  wallet     TEXT NOT NULL,
  amount     TEXT NOT NULL,
  currency   TEXT NOT NULL,
  created_at INTEGER NOT NULL
);
INSERT INTO room_entries_v2 (signature, code, round, wallet, amount, currency, created_at)
  SELECT signature, code, round, wallet, amount, currency, created_at FROM room_entries;
DROP TABLE room_entries;
ALTER TABLE room_entries_v2 RENAME TO room_entries;
CREATE INDEX idx_room_entries_seat ON room_entries(code, round, wallet);
