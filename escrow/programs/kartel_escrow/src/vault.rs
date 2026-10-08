//! The only ways money moves: into a room when a player joins, and out of it to the wallet it is owed to.

use anchor_lang::prelude::*;
use anchor_lang::system_program;
use anchor_spl::token::{self, Mint, Token, TokenAccount, TransferChecked};

use crate::constants::ROOM_SEED;
use crate::error::EscrowError;
use crate::state::Room;

/// The token side of a move: the room's mint, its vault, the token account on the person's side, the token program.
pub struct TokenLeg<'a, 'info> {
    pub mint: &'a Account<'info, Mint>,
    pub vault: &'a Account<'info, TokenAccount>,
    pub holder: &'a Account<'info, TokenAccount>,
    pub token_program: &'a Program<'info, Token>,
}

/// The token accounts an instruction was given, checked against the room's currency: none for a SOL room, all four
/// for a token room, with the room's own mint. (Owners and the vault's address are checked by the account constraints.)
pub fn token_leg<'a, 'info>(
    room: &Room,
    mint: &'a Option<Account<'info, Mint>>,
    vault: &'a Option<Account<'info, TokenAccount>>,
    holder: &'a Option<Account<'info, TokenAccount>>,
    token_program: &'a Option<Program<'info, Token>>,
) -> Result<Option<TokenLeg<'a, 'info>>> {
    match room.mint {
        None => {
            require!(
                mint.is_none() && vault.is_none() && holder.is_none() && token_program.is_none(),
                EscrowError::WrongCurrencyAccounts
            );
            Ok(None)
        }
        Some(expected) => match (mint, vault, holder, token_program) {
            (Some(mint), Some(vault), Some(holder), Some(token_program)) => {
                require_keys_eq!(mint.key(), expected, EscrowError::WrongCurrencyAccounts);
                Ok(Some(TokenLeg { mint, vault, holder, token_program }))
            }
            _ => err!(EscrowError::WrongCurrencyAccounts),
        },
    }
}

/// A player's stake into the room: lamports to the room account, or tokens from the player's account to the vault.
pub fn take_stake<'info>(
    room: &Account<'info, Room>,
    player: &Signer<'info>,
    system: &Program<'info, System>,
    leg: Option<TokenLeg<'_, 'info>>,
    amount: u64,
) -> Result<()> {
    match leg {
        None => system_program::transfer(
            CpiContext::new(
                system.key(),
                system_program::Transfer { from: player.to_account_info(), to: room.to_account_info() },
            ),
            amount,
        ),
        Some(leg) => token::transfer_checked(
            CpiContext::new(
                leg.token_program.key(),
                TransferChecked {
                    from: leg.holder.to_account_info(),
                    mint: leg.mint.to_account_info(),
                    to: leg.vault.to_account_info(),
                    authority: player.to_account_info(),
                },
            ),
            amount,
            leg.mint.decimals,
        ),
    }
}

/// `amount` out of the room: lamports straight from the room account (the program owns it), or tokens from the vault
/// under the room's signature. `to_wallet` receives SOL; a token payout goes to the leg's holder account.
pub fn pay_out<'info>(
    room: &Account<'info, Room>,
    to_wallet: &AccountInfo<'info>,
    leg: Option<TokenLeg<'_, 'info>>,
    amount: u64,
) -> Result<()> {
    if amount == 0 {
        return Ok(());
    }
    match leg {
        None => {
            room.sub_lamports(amount)?;
            to_wallet.add_lamports(amount)?;
            Ok(())
        }
        Some(leg) => {
            let round = room.round_seed();
            let bump = [room.bump];
            let seeds: &[&[u8]] = &[ROOM_SEED, &room.code, &round, &bump];
            token::transfer_checked(
                CpiContext::new_with_signer(
                    leg.token_program.key(),
                    TransferChecked {
                        from: leg.vault.to_account_info(),
                        mint: leg.mint.to_account_info(),
                        to: leg.holder.to_account_info(),
                        authority: room.to_account_info(),
                    },
                    &[seeds],
                ),
                amount,
                leg.mint.decimals,
            )
        }
    }
}
