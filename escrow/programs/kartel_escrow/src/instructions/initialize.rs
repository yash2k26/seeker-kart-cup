use anchor_lang::prelude::*;

use crate::constants::*;
use crate::error::EscrowError;
use crate::program::KartelEscrow;
use crate::state::Config;

#[derive(Accounts)]
pub struct Initialize<'info> {
    #[account(mut)]
    pub admin: Signer<'info>,
    #[account(init, payer = admin, space = 8 + Config::INIT_SPACE, seeds = [CONFIG_SEED], bump)]
    pub config: Account<'info, Config>,
    /// Only the upgrade authority may create the config, so nobody can claim it between deploy and setup.
    #[account(constraint = program.programdata_address()? == Some(program_data.key()) @ EscrowError::NotUpgradeAuthority)]
    pub program: Program<'info, KartelEscrow>,
    #[account(constraint = program_data.upgrade_authority_address == Some(admin.key()) @ EscrowError::NotUpgradeAuthority)]
    pub program_data: Account<'info, ProgramData>,
    pub system_program: Program<'info, System>,
}

pub fn handle_initialize(ctx: Context<Initialize>, referee: Pubkey, treasury: Pubkey, max_cut_bps: u16) -> Result<()> {
    require!(max_cut_bps <= MAX_CUT_BPS, EscrowError::CutTooHigh);
    ctx.accounts.config.set_inner(Config {
        admin: ctx.accounts.admin.key(),
        referee,
        treasury,
        max_cut_bps,
        paused: false,
        bump: ctx.bumps.config,
    });
    Ok(())
}
