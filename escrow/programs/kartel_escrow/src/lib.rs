//! Kartel's prize escrow.
//!
//! Each paid room (and each rematch round) gets one account that holds every player's stake. The game server's
//! referee key opens the room, locks it when the race starts, and reports the finishing order; the program alone
//! decides what that order pays (`sidepot`: fair shares, 50/30/20, the house cut) and pays it only to the wallets that
//! staked. Before the lock a player can always take their stake back; after the room expires anyone can cancel it,
//! and every payout or refund can be delivered by anyone, so no stake depends on the server staying up.
//!
//! Flow: `create_room` → `join`… (`leave` / `remove_entry`) → `lock` → `settle` (or `cancel`) → `claim` per seat and
//! `collect_cut` → `close_room`.

pub mod constants;
pub mod error;
pub mod events;
pub mod instructions;
pub mod sidepot;
pub mod state;
pub mod vault;

use anchor_lang::prelude::*;

pub use constants::*;
pub use instructions::*;
pub use state::*;

declare_id!("KartpVhCHvs2xSBe59LrPYUJ8qUu1PT4DegfFa1nkhv");

#[program]
pub mod kartel_escrow {
    use super::*;

    pub fn initialize(ctx: Context<Initialize>, referee: Pubkey, treasury: Pubkey, max_cut_bps: u16) -> Result<()> {
        instructions::initialize::handle_initialize(ctx, referee, treasury, max_cut_bps)
    }

    pub fn update_config(ctx: Context<UpdateConfig>, update: ConfigUpdate) -> Result<()> {
        instructions::update_config::handle_update_config(ctx, update)
    }

    pub fn create_room(ctx: Context<CreateRoom>, code: [u8; 5], round: u32, params: RoomParams) -> Result<()> {
        instructions::create_room::handle_create_room(ctx, code, round, params)
    }

    pub fn join(ctx: Context<Join>, stake: u64) -> Result<()> {
        instructions::join::handle_join(ctx, stake)
    }

    pub fn leave(ctx: Context<Leave>) -> Result<()> {
        instructions::leave::handle_leave(ctx)
    }

    pub fn remove_entry(ctx: Context<RemoveEntry>) -> Result<()> {
        instructions::remove_entry::handle_remove_entry(ctx)
    }

    pub fn lock(ctx: Context<RefereeRoom>, players: Vec<Pubkey>) -> Result<()> {
        instructions::lock::handle_lock(ctx, players)
    }

    pub fn settle(ctx: Context<RefereeRoom>, order: Vec<Pubkey>) -> Result<()> {
        instructions::settle::handle_settle(ctx, order)
    }

    pub fn cancel(ctx: Context<Cancel>) -> Result<()> {
        instructions::cancel::handle_cancel(ctx)
    }

    pub fn claim(ctx: Context<Claim>) -> Result<()> {
        instructions::claim::handle_claim(ctx)
    }

    pub fn collect_cut(ctx: Context<CollectCut>) -> Result<()> {
        instructions::collect_cut::handle_collect_cut(ctx)
    }

    pub fn close_room(ctx: Context<CloseRoom>) -> Result<()> {
        instructions::close_room::handle_close_room(ctx)
    }
}
