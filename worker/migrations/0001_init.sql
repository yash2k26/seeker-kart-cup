CREATE TABLE players (
  wallet       TEXT PRIMARY KEY,
  skr_name     TEXT,
  skr_checked  INTEGER,
  is_seeker    INTEGER NOT NULL DEFAULT 0,
  sgt_mint     TEXT,
  created_at   INTEGER NOT NULL,
  last_seen    INTEGER NOT NULL
);
CREATE UNIQUE INDEX idx_players_sgt ON players(sgt_mint) WHERE sgt_mint IS NOT NULL;

CREATE TABLE siws_nonces (
  nonce      TEXT PRIMARY KEY,
  payload    TEXT NOT NULL,
  expires_at INTEGER NOT NULL
);

CREATE TABLE race_sessions (
  race_id     TEXT PRIMARY KEY,
  device_id   TEXT NOT NULL,
  wallet      TEXT,
  track_id    TEXT NOT NULL,
  started_at  INTEGER NOT NULL,
  finished_at INTEGER
);

CREATE TABLE results (
  id          INTEGER PRIMARY KEY AUTOINCREMENT,
  race_id     TEXT NOT NULL UNIQUE REFERENCES race_sessions(race_id),
  device_id   TEXT NOT NULL,
  wallet      TEXT,
  track_id    TEXT NOT NULL,
  total_ms    INTEGER NOT NULL,
  best_lap_ms INTEGER NOT NULL,
  laps_json   TEXT NOT NULL,
  position    INTEGER,
  is_pb       INTEGER NOT NULL DEFAULT 0,
  memo_sig    TEXT,
  created_at  INTEGER NOT NULL
);
CREATE INDEX idx_results_board ON results(track_id, total_ms);
CREATE INDEX idx_results_wallet ON results(wallet, track_id, total_ms);

CREATE TABLE unlocks (
  wallet     TEXT NOT NULL,
  sku        TEXT NOT NULL,
  source     TEXT NOT NULL,
  signature  TEXT,
  created_at INTEGER NOT NULL,
  PRIMARY KEY (wallet, sku)
);

CREATE TABLE tx_log (
  signature  TEXT PRIMARY KEY,
  wallet     TEXT NOT NULL,
  kind       TEXT NOT NULL,
  status     TEXT NOT NULL,
  slot       INTEGER,
  created_at INTEGER NOT NULL
);

CREATE TABLE pending_tx (
  id           TEXT PRIMARY KEY,
  wallet       TEXT NOT NULL,
  kind         TEXT NOT NULL,
  memo         TEXT NOT NULL,
  created_at   INTEGER NOT NULL,
  resolved_sig TEXT
);

CREATE TABLE events (
  id         INTEGER PRIMARY KEY AUTOINCREMENT,
  device_id  TEXT NOT NULL,
  wallet     TEXT,
  name       TEXT NOT NULL,
  props      TEXT,
  created_at INTEGER NOT NULL
);
CREATE INDEX idx_events_name ON events(name, created_at);
