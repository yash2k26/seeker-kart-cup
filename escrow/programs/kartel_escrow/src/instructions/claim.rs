use anchor_lang::prelude::*;
use anchor_spl::token::{Mint, Token, TokenAccount};

use crate::constants::*;
use crate::error::EscrowError;
use crate::events::Claimed;
use crate::state::{Room, RoomState};
use crate::vault::{pay_out, token_leg};

/// Delivers one seat's payout to the wallet that staked it. Anyone may send this (the game server does, right after
/// settling), so a winner never has to come back for their money; it can only ever pay the entry's own wallet, once.
/// A stake left out of the race at the lock can be claimed back straight away, without waiting for the result.
#[derive(Accounts)]
pub struct Claim<'info> {
    #[account(mut, seeds = [ROOM_SEED, room.code.as_ref(), room.round_seed().as_ref()], bump = room.bump)]
    pub room: Account<'info, Room>,
    /// CHECK: the wallet owed; it must hold an entry in the room.
    #[account(mut)]
    pub player: UncheckedAccount<'info>,
    pub mint: Option<Account<'info, Mint>>,
    #[account(mut, associated_token::mint = mint, associated_token::authority = room)]
    pub vault: Option<Account<'info, TokenAccount>>,
    #[account(mut, token::mint = mint, token::authority = player)]
    pub player_token: Option<Account<'info, TokenAccount>>,
    pub token_program: Option<Program<'info, Token>>,
}

pub fn handle_claim(ctx: Context<Claim>) -> Result<()> {
    let a = &ctx.accounts;
    let player = a.player.key();
    let i = a.room.find(&player).ok_or(EscrowError::NoEntry)?;
    let entry = a.room.entries[i];
    require!(a.room.finished() || (entry.refund && a.room.state == RoomState::Locked), EscrowError::NotFinished);
    require!(!entry.paid, EscrowError::AlreadyPaid);

    let leg = token_leg(&a.room, &a.mint, &a.vault, &a.player_token, &a.token_program)?;
    pay_out(&a.room, &a.player.to_account_info(), leg, entry.payout)?;

    let room = &mut ctx.accounts.room;
    room.entries[i].paid = true;
    emit!(Claimed { room: room.key(), player, amount: entry.payout });
    Ok(())
}
