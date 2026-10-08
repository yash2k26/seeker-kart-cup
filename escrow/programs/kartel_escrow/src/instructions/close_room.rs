use anchor_lang::prelude::*;
use anchor_spl::token::{self, CloseAccount, Mint, Token, TokenAccount};

use crate::constants::*;
use crate::error::EscrowError;
use crate::events::RoomClosed;
use crate::state::Room;
use crate::vault::{pay_out, token_leg};

/// Once nothing is owed, the room's accounts are closed and their rent goes back to whoever paid it. Tokens sent
/// straight to the vault (not stakes) go to the treasury first, so the vault can close empty. Anyone may send it.
#[derive(Accounts)]
pub struct CloseRoom<'info> {
    #[account(
        mut,
        seeds = [ROOM_SEED, room.code.as_ref(), room.round_seed().as_ref()],
        bump = room.bump,
        close = payer,
    )]
    pub room: Account<'info, Room>,
    /// CHECK: whoever paid the room's rent.
    #[account(mut, address = room.payer @ EscrowError::WrongRecipient)]
    pub payer: UncheckedAccount<'info>,
    pub mint: Option<Account<'info, Mint>>,
    #[account(mut, associated_token::mint = mint, associated_token::authority = room)]
    pub vault: Option<Account<'info, TokenAccount>>,
    #[account(mut, token::mint = mint, token::authority = room.treasury)]
    pub treasury_token: Option<Account<'info, TokenAccount>>,
    pub token_program: Option<Program<'info, Token>>,
}

pub fn handle_close_room(ctx: Context<CloseRoom>) -> Result<()> {
    let a = &ctx.accounts;
    require!(a.room.finished(), EscrowError::NotFinished);
    require!(!a.room.owes(), EscrowError::PayoutsOwed);

    if let Some(leg) = token_leg(&a.room, &a.mint, &a.vault, &a.treasury_token, &a.token_program)? {
        let (vault, token_program) = (leg.vault, leg.token_program);
        pay_out(&a.room, &a.payer.to_account_info(), Some(leg), vault.amount)?;
        let round = a.room.round_seed();
        let bump = [a.room.bump];
        let seeds: &[&[u8]] = &[ROOM_SEED, &a.room.code, &round, &bump];
        token::close_account(CpiContext::new_with_signer(
            token_program.key(),
            CloseAccount {
                account: vault.to_account_info(),
                destination: a.payer.to_account_info(),
                authority: a.room.to_account_info(),
            },
            &[seeds],
        ))?;
    }
    emit!(RoomClosed { room: a.room.key() });
    Ok(())
}
