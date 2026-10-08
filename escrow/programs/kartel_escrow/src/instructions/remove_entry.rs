use anchor_lang::prelude::*;
use anchor_spl::token::{Mint, Token, TokenAccount};

use crate::constants::*;
use crate::error::EscrowError;
use crate::events::Refunded;
use crate::state::{Config, Room, RoomState};
use crate::vault::{pay_out, token_leg};

/// The referee hands a seat's stake back while the room is open: the player left the lobby, was removed by the host,
/// or never made it to the grid. The money can only go to the wallet that staked it.
#[derive(Accounts)]
pub struct RemoveEntry<'info> {
    pub referee: Signer<'info>,
    #[account(seeds = [CONFIG_SEED], bump = config.bump, has_one = referee @ EscrowError::NotReferee)]
    pub config: Account<'info, Config>,
    #[account(mut, seeds = [ROOM_SEED, room.code.as_ref(), room.round_seed().as_ref()], bump = room.bump)]
    pub room: Account<'info, Room>,
    /// CHECK: the wallet whose seat is removed; it must hold an entry, and receives its stake.
    #[account(mut)]
    pub player: UncheckedAccount<'info>,
    pub mint: Option<Account<'info, Mint>>,
    #[account(mut, associated_token::mint = mint, associated_token::authority = room)]
    pub vault: Option<Account<'info, TokenAccount>>,
    #[account(mut, token::mint = mint, token::authority = player)]
    pub player_token: Option<Account<'info, TokenAccount>>,
    pub token_program: Option<Program<'info, Token>>,
}

pub fn handle_remove_entry(ctx: Context<RemoveEntry>) -> Result<()> {
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
