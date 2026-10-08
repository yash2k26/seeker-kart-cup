use anchor_lang::prelude::*;
use anchor_spl::token::{Mint, Token, TokenAccount};

use crate::constants::*;
use crate::error::EscrowError;
use crate::events::CutCollected;
use crate::state::{Room, RoomState};
use crate::vault::{pay_out, token_leg};

/// Sends a settled room's cut to the treasury it was created with. Anyone may send it; it pays only that treasury.
#[derive(Accounts)]
pub struct CollectCut<'info> {
    #[account(mut, seeds = [ROOM_SEED, room.code.as_ref(), room.round_seed().as_ref()], bump = room.bump)]
    pub room: Account<'info, Room>,
    /// CHECK: the treasury fixed in the room when it was created.
    #[account(mut, address = room.treasury @ EscrowError::WrongRecipient)]
    pub treasury: UncheckedAccount<'info>,
    pub mint: Option<Account<'info, Mint>>,
    #[account(mut, associated_token::mint = mint, associated_token::authority = room)]
    pub vault: Option<Account<'info, TokenAccount>>,
    #[account(mut, token::mint = mint, token::authority = treasury)]
    pub treasury_token: Option<Account<'info, TokenAccount>>,
    pub token_program: Option<Program<'info, Token>>,
}

pub fn handle_collect_cut(ctx: Context<CollectCut>) -> Result<()> {
    let a = &ctx.accounts;
    require!(a.room.state == RoomState::Settled, EscrowError::NotFinished);
    require!(!a.room.cut_paid && a.room.house_cut > 0, EscrowError::NothingToCollect);
    let amount = a.room.house_cut;

    let leg = token_leg(&a.room, &a.mint, &a.vault, &a.treasury_token, &a.token_program)?;
    pay_out(&a.room, &a.treasury.to_account_info(), leg, amount)?;

    let room = &mut ctx.accounts.room;
    room.cut_paid = true;
    emit!(CutCollected { room: room.key(), amount });
    Ok(())
}
