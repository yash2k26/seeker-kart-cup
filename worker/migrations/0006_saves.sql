-- Cloud save (12 Oct 2026): a player's progress kept with their wallet, so a reinstall or a new phone signs back into
-- the same game. `data` is the phone's own snapshot (CloudSave.cs); `rev` counts saves, so a phone holding an older
-- one can never overwrite a newer one.
CREATE TABLE saves (
  wallet     TEXT PRIMARY KEY,
  data       TEXT NOT NULL,
  rev        INTEGER NOT NULL,
  updated_at INTEGER NOT NULL
);
