use anchor_lang::prelude::*;

#[event]
pub struct RoomCreated {
    pub room: Pubkey,
    pub code: [u8; 5],
    pub round: u32,
    pub mint: Option<Pubkey>,
    pub min_stake: u64,
    pub max_stake: u64,
    pub max_players: u8,
    pub cut_bps: u16,
    pub expires_at: i64,
}

#[event]
pub struct Joined {
    pub room: Pubkey,
    pub player: Pubkey,
    pub stake: u64,
}

/// A stake handed back while the room was still open (the player left, or the referee removed the seat).
#[event]
pub struct Refunded {
    pub room: Pubkey,
    pub player: Pubkey,
    pub amount: u64,
}

#[event]
pub struct RoomLocked {
    pub room: Pubkey,
    /// Wallets in the race; any other stake was left out and is owed back.
    pub players: u8,
    pub pot: u64,
}

#[event]
pub struct RoomSettled {
    pub room: Pubkey,
    /// The finishing order the referee reported, winner first.
    pub order: Vec<Pubkey>,
    pub payouts: Vec<u64>,
    pub house_cut: u64,
}

#[event]
pub struct RoomCancelled {
    pub room: Pubkey,
    pub by_referee: bool,
}

#[event]
pub struct Claimed {
    pub room: Pubkey,
    pub player: Pubkey,
    pub amount: u64,
}

#[event]
pub struct CutCollected {
    pub room: Pubkey,
    pub amount: u64,
}

#[event]
pub struct RoomClosed {
    pub room: Pubkey,
}
