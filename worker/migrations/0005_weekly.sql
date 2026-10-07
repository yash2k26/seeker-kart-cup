-- Kartel Weekly (weekly.ts): a time trial's run carries its mode, and the country it was driven from (the Weekly's
-- prizes are not open in India). Older rows stay NULL: ordinary races.
ALTER TABLE race_sessions ADD COLUMN mode TEXT;
ALTER TABLE race_sessions ADD COLUMN country TEXT;
ALTER TABLE results ADD COLUMN mode TEXT;
ALTER TABLE results ADD COLUMN country TEXT;
CREATE INDEX idx_results_weekly ON results(mode, track_id, created_at);
