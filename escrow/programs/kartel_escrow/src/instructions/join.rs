use anchor_lang::prelude::*;
use anchor_spl::token::{Mint, Token, TokenAccount};

use crate::constants::*;
use crate::error::EscrowError;
use crate::events::Joined;
use crate::state::{Config, Entry, Room, RoomState};
use crate::vault::{take_stake, token_leg};

#[derive(Accounts)]
pub struct Join<'info> {
    #[account(mut)]
    pub player: Signer<'info>,
    #[account(seeds = [CONFIG_SEED], bump = config.bump, constraint = !config.paused @ EscrowError::Paused)]
    pub config: Account<'info, Config>,
    #[account(mut, seeds = [ROOM_SEED, room.code.as_ref(), room.round_seed().as_ref()], bump = room.bump)]
    pub room: Account<'info, Room>,
    // Token rooms only; all four are omitted for SOL.
    pub mint: Option<Account<'info, Mint>>,
    #[account(mut, associated_token::mint = mint, associated_token::authority = room)]
    pub vault: Option<Account<'info, TokenAccount>>,
    /// The player's own associated token account: refunds and payouts go back to it.
    #[account(mut, associated_token::mint = mint, associated_token::authority = player)]
    pub player_token: Option<Account<'info, TokenAccount>>,
    pub token_program: Option<Program<'info, Token>>,
    pub system_program: Program<'info, System>,
}

pub fn handle_join(ctx: Context<Join>, stake: u64) -> Result<()> {
    let now = Clock::get()?.unix_timestamp;
    let a = &ctx.accounts;
    let player = a.player.key();
    require!(a.room.state == RoomState::Open, EscrowError::NotOpen);
    require!(now < a.room.expires_at, EscrowError::Expired);
    require!(a.room.entries.len() < a.room.max_players as usize, EscrowError::RoomFull);
    require!(a.room.find(&player).is_none(), EscrowError::AlreadyJoined);
    require!(
        (a.room.min_stake..=a.room.max_stake).contains(&stake) && stake % a.room.stake_unit == 0,
        EscrowError::StakeOutOfRange
    );

    let leg = token_leg(&a.room, &a.mint, &a.vault, &a.player_token, &a.token_program)?;
    take_stake(&a.room, &a.player, &a.system_program, leg, stake)?;

    let room = &mut ctx.accounts.room;
    room.entries.push(Entry { player, stake, payout: 0, paid: false, refund: false });
    emit!(Joined { room: room.key(), player, stake });
    Ok(())
}
