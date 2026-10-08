use anchor_lang::prelude::*;

use crate::constants::*;
use crate::error::EscrowError;
use crate::state::Config;

#[derive(Accounts)]
pub struct UpdateConfig<'info> {
    pub admin: Signer<'info>,
    #[account(mut, seeds = [CONFIG_SEED], bump = config.bump, has_one = admin @ EscrowError::NotAdmin)]
    pub config: Account<'info, Config>,
}

/// Each field left `None` stays as it is. Rooms already created keep the treasury and cut they were made with.
#[derive(AnchorSerialize, AnchorDeserialize, Clone, Default)]
pub struct ConfigUpdate {
    pub admin: Option<Pubkey>,
    pub referee: Option<Pubkey>,
    pub treasury: Option<Pubkey>,
    pub max_cut_bps: Option<u16>,
    pub paused: Option<bool>,
}

pub fn handle_update_config(ctx: Context<UpdateConfig>, update: ConfigUpdate) -> Result<()> {
    let config = &mut ctx.accounts.config;
    if let Some(cut) = update.max_cut_bps {
        require!(cut <= MAX_CUT_BPS, EscrowError::CutTooHigh);
        config.max_cut_bps = cut;
    }
    if let Some(admin) = update.admin {
        config.admin = admin;
    }
    if let Some(referee) = update.referee {
        config.referee = referee;
    }
    if let Some(treasury) = update.treasury {
        config.treasury = treasury;
    }
    if let Some(paused) = update.paused {
        config.paused = paused;
    }
    Ok(())
}
