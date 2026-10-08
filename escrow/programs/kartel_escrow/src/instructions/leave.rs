use anchor_lang::prelude::*;
use anchor_spl::token::{Mint, Token, TokenAccount};

use crate::constants::*;
use crate::error::EscrowError;
use crate::events::Refunded;
use crate::state::{Room, RoomState};
use crate::vault::{pay_out, token_leg};

/// A player takes their own stake back while the room is still open. Allowed even when the program is paused or the
/// room has expired: money can always leave an open room.
#[derive(Accounts)]
pub struct Leave<'info> {
    #[account(mut)]
    pub player: Signer<'info>,
    #[account(mut, seeds = [ROOM_SEED, room.code.as_ref(), room.round_seed().as_ref()], bump = room.bump)]
    pub room: Account<'info, Room>,
    pub mint: Option<Account<'info, Mint>>,
    #[account(mut, associated_token::mint = mint, associated_token::authority = room)]
    pub vault: Option<Account<'info, TokenAccount>>,
    #[account(mut, token::mint = mint, token::authority = player)]
    pub player_token: Option<Account<'info, TokenAccount>>,
    pub token_program: Option<Program<'info, Token>>,
}

pub fn handle_leave(ctx: Context<Leave>) -> Result<()> {
    let a = &ctx.accounts;
    let player = a.player.key();
    require!(a.room.state == RoomState::Open, EscrowError::NotOpen);
    let i = a.room.find(&player).ok_or(EscrowError::NoEntry)?;
    let amount = a.room.entries[i].stake;

    let leg = token_leg(&a.room, &a.mint, &a.vault, &a.player_token, &a.token_program)?;
    pay_out(&a.room, &a.player.to_account_info(), leg, amount)?;

    let room = &mut ctx.accounts.room;
    room.entries.remove(i);
    emit!(Refunded { room: room.key(), player, amount });
    Ok(())
}
