use anchor_lang::prelude::*;

use crate::constants::*;
use crate::error::EscrowError;
use crate::events::RoomLocked;
use crate::state::{Config, Room, RoomState};

/// The referee acting on one room: lock and settle.
#[derive(Accounts)]
pub struct RefereeRoom<'info> {
    pub referee: Signer<'info>,
    #[account(seeds = [CONFIG_SEED], bump = config.bump, has_one = referee @ EscrowError::NotReferee)]
    pub config: Account<'info, Config>,
    #[account(mut, seeds = [ROOM_SEED, room.code.as_ref(), room.round_seed().as_ref()], bump = room.bump)]
    pub room: Account<'info, Room>,
}

/// The race is starting: from here no stake enters or leaves until the room is settled or cancelled. `players` is
/// the field: staked wallets that race. Any other stake (one that landed on the way to the lock, a seat that never
/// reached the grid) is left out and owed back in full at once, so nobody is locked into a race they are not in and
/// nobody can hold the lock up by staking.
pub fn handle_lock(ctx: Context<RefereeRoom>, players: Vec<Pubkey>) -> Result<()> {
    let now = Clock::get()?.unix_timestamp;
    let room = &mut ctx.accounts.room;
    require!(room.state == RoomState::Open, EscrowError::NotOpen);
    require!(now < room.expires_at, EscrowError::Expired);
    require!(players.len() >= 2, EscrowError::TooFewPlayers);
    for (i, player) in players.iter().enumerate() {
        require!(room.find(player).is_some() && !players[..i].contains(player), EscrowError::BadField);
    }
    for entry in room.entries.iter_mut().filter(|e| !players.contains(&e.player)) {
        entry.refund = true;
        entry.payout = entry.stake;
    }
    room.state = RoomState::Locked;
    emit!(RoomLocked { room: room.key(), players: players.len() as u8, pot: room.pot()? });
    Ok(())
}
