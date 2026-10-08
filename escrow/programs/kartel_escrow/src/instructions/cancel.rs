use anchor_lang::prelude::*;

use crate::constants::*;
use crate::error::EscrowError;
use crate::events::RoomCancelled;
use crate::state::{Config, Room, RoomState};

/// Every stake becomes claimable back in full. The referee may cancel an open or locked room at any time; once the
/// room has expired, anyone may, so stakes never depend on the game server staying up.
#[derive(Accounts)]
pub struct Cancel<'info> {
    pub caller: Signer<'info>,
    #[account(seeds = [CONFIG_SEED], bump = config.bump)]
    pub config: Account<'info, Config>,
    #[account(mut, seeds = [ROOM_SEED, room.code.as_ref(), room.round_seed().as_ref()], bump = room.bump)]
    pub room: Account<'info, Room>,
}

pub fn handle_cancel(ctx: Context<Cancel>) -> Result<()> {
    let now = Clock::get()?.unix_timestamp;
    let by_referee = ctx.accounts.caller.key() == ctx.accounts.config.referee;
    let room = &mut ctx.accounts.room;
    require!(!room.finished(), EscrowError::AlreadyFinished);
    require!(by_referee || now >= room.expires_at, EscrowError::CannotCancelYet);
    for entry in room.entries.iter_mut() {
        entry.payout = entry.stake;
    }
    room.house_cut = 0;
    room.state = RoomState::Cancelled;
    emit!(RoomCancelled { room: room.key(), by_referee });
    Ok(())
}
