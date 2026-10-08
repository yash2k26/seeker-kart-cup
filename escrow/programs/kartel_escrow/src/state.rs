use anchor_lang::prelude::*;

use crate::constants::*;
use crate::error::EscrowError;

/// One per program, at the `config` PDA. Created by the upgrade authority.
#[account]
#[derive(InitSpace)]
pub struct Config {
    /// May change the referee, the treasury, the cut ceiling and the pause switch.
    pub admin: Pubkey,
    /// The game server's key: creates rooms, removes seats, locks, settles and cancels. It decides the finishing
    /// order and nothing else: where money goes is fixed by the rules below.
    pub referee: Pubkey,
    /// Receives the cut: SOL to this wallet, tokens to an account it owns.
    pub treasury: Pubkey,
    pub max_cut_bps: u16,
    /// Stops new rooms and new stakes. Money already in can always leave.
    pub paused: bool,
    pub bump: u8,
}

#[derive(AnchorSerialize, AnchorDeserialize, Clone, Copy, PartialEq, Eq, InitSpace, Debug)]
pub enum RoomState {
    /// Taking stakes; a player may leave and the referee may remove a seat, each with a full refund.
    Open,
    /// The race is on: no stake can enter or leave until the room is settled or cancelled.
    Locked,
    /// Payouts fixed by the finishing order; each is claimable once.
    Settled,
    /// Every stake claimable back in full.
    Cancelled,
}

#[derive(AnchorSerialize, AnchorDeserialize, Clone, Copy, PartialEq, Eq, InitSpace, Debug)]
pub struct Entry {
    pub player: Pubkey,
    pub stake: u64,
    /// What this seat is owed once the room is settled or cancelled.
    pub payout: u64,
    /// The payout has been delivered.
    pub paid: bool,
    /// Left out of the race when the room was locked: the whole stake is owed back, outside the split.
    pub refund: bool,
}

/// One per paid room and round, at `["room", code, round as u32 LE]`. A SOL room holds the stakes in its own
/// lamports; a token room holds them in its associated token account (the vault).
#[account]
#[derive(InitSpace)]
pub struct Room {
    pub code: [u8; CODE_LEN],
    pub round: u32,
    /// The staked token; `None` for SOL.
    pub mint: Option<Pubkey>,
    pub min_stake: u64,
    pub max_stake: u64,
    /// Every stake is a whole number of these, so no payout is ever dust (each SOL payout clears the rent minimum).
    pub stake_unit: u64,
    pub cut_bps: u16,
    pub max_players: u8,
    pub state: RoomState,
    pub created_at: i64,
    /// From this moment anyone may cancel the room if it is still open or locked.
    pub expires_at: i64,
    /// Paid the rent, and gets it back when the room is closed.
    pub payer: Pubkey,
    /// Where the cut goes, fixed when the room is created.
    pub treasury: Pubkey,
    pub house_cut: u64,
    pub cut_paid: bool,
    pub bump: u8,
    #[max_len(6)]
    pub entries: Vec<Entry>,
}

impl Room {
    pub fn find(&self, player: &Pubkey) -> Option<usize> {
        self.entries.iter().position(|e| e.player == *player)
    }

    /// The stakes in the race (not those left out at the lock).
    pub fn pot(&self) -> Result<u64> {
        self.entries
            .iter()
            .filter(|e| !e.refund)
            .try_fold(0u64, |sum, e| sum.checked_add(e.stake))
            .ok_or_else(|| error!(EscrowError::Overflow))
    }

    pub fn finished(&self) -> bool {
        matches!(self.state, RoomState::Settled | RoomState::Cancelled)
    }

    /// Anything still to hand out: a payout not yet claimed, or the cut not yet collected.
    pub fn owes(&self) -> bool {
        self.entries.iter().any(|e| !e.paid && e.payout > 0) || (!self.cut_paid && self.house_cut > 0)
    }

    pub fn round_seed(&self) -> [u8; 4] {
        self.round.to_le_bytes()
    }
}

const _: () = assert!(MAX_PLAYERS == 6, "Room::entries is #[max_len(6)]");
