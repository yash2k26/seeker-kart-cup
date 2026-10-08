use anchor_lang::prelude::*;

/// Seats per room: every track has six grid slots (TrackSetup.StartGrid), and the phone is measured racing six karts.
#[constant]
pub const MAX_PLAYERS: u8 = 6;

/// The cut can never be configured above 20% of a contested layer.
#[constant]
pub const MAX_CUT_BPS: u16 = 2_000;

/// A room's lifetime bounds. Past `expires_at` anyone may cancel it and every stake becomes claimable again.
#[constant]
pub const MIN_TTL_SECS: i64 = 10 * 60;
#[constant]
pub const MAX_TTL_SECS: i64 = 24 * 60 * 60;

#[constant]
pub const CONFIG_SEED: &[u8] = b"config";
#[constant]
pub const ROOM_SEED: &[u8] = b"room";

/// Room codes are five characters from the Worker's alphabet (no 0/O/1/I: they are read aloud and typed).
pub const CODE_LEN: usize = 5;
pub const CODE_ALPHABET: &[u8] = b"ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

const _: () = assert!(MAX_PLAYERS as usize == crate::sidepot::MAX_SEATS);
