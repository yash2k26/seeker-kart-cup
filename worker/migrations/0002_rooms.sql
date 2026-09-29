-- Online rooms and tournaments. The Durable Object holds the live room; these rows are the durable record:
-- invite page, "my winnings", audits and refunds.
CREATE TABLE rooms (
  code        TEXT PRIMARY KEY,
  host        TEXT NOT NULL,             -- wallet, or g:<deviceId> for a guest host of a free room
  tracks      TEXT NOT NULL,             -- JSON array of track ids
  fee         TEXT NOT NULL DEFAULT '0', -- base units
  currency    TEXT NOT NULL DEFAULT 'SOL',
  state       TEXT NOT NULL,             -- lobby|racing|finished|cancelled
  round       INTEGER NOT NULL DEFAULT 1,
  pool        TEXT NOT NULL DEFAULT '0',
  cut         TEXT NOT NULL DEFAULT '0',
  created_at  INTEGER NOT NULL,
  finished_at INTEGER
);

CREATE TABLE room_entries (
  code       TEXT NOT NULL,
  round      INTEGER NOT NULL,
  wallet     TEXT NOT NULL,
  signature  TEXT NOT NULL UNIQUE,
  amount     TEXT NOT NULL,
  currency   TEXT NOT NULL,
  created_at INTEGER NOT NULL,
  PRIMARY KEY (code, round, wallet)
);

CREATE TABLE payouts (
  id                      TEXT PRIMARY KEY,
  code                    TEXT NOT NULL,
  round                   INTEGER NOT NULL,
  wallet                  TEXT NOT NULL,
  kind                    TEXT NOT NULL,   -- prize|refund
  place                   INTEGER,
  amount                  TEXT NOT NULL,
  currency                TEXT NOT NULL,
  status                  TEXT NOT NULL,   -- pending|sent|confirmed|failed
  signature               TEXT,
  attempts                INTEGER NOT NULL DEFAULT 0,
  created_at              INTEGER NOT NULL,
  updated_at              INTEGER NOT NULL
);
CREATE INDEX idx_payouts_status ON payouts(status);
CREATE INDEX idx_payouts_wallet ON payouts(wallet, created_at);
