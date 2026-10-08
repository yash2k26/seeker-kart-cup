use anchor_lang::prelude::*;
use anchor_spl::token::Mint;

use crate::constants::*;
use crate::error::EscrowError;
use crate::events::RoomCreated;
use crate::state::{Config, Room, RoomState};

#[derive(AnchorSerialize, AnchorDeserialize, Clone, Copy, Debug)]
pub struct RoomParams {
    pub min_stake: u64,
    pub max_stake: u64,
    pub stake_unit: u64,
    pub max_players: u8,
    pub cut_bps: u16,
    pub ttl_secs: i64,
}

#[derive(Accounts)]
#[instruction(code: [u8; 5], round: u32, params: RoomParams)]
pub struct CreateRoom<'info> {
    #[account(mut)]
    pub referee: Signer<'info>,
    #[account(
        seeds = [CONFIG_SEED],
        bump = config.bump,
        has_one = referee @ EscrowError::NotReferee,
        constraint = !config.paused @ EscrowError::Paused,
    )]
    pub config: Account<'info, Config>,
    #[account(
        init,
        payer = referee,
        space = 8 + Room::INIT_SPACE,
        seeds = [ROOM_SEED, code.as_ref(), round.to_le_bytes().as_ref()],
        bump,
    )]
    pub room: Account<'info, Room>,
    /// The token staked in this room; omitted for SOL. Its vault (the room's associated token account) is created by
    /// an instruction beside this one, in the same transaction.
    pub mint: Option<Account<'info, Mint>>,
    pub system_program: Program<'info, System>,
}

pub fn handle_create_room(ctx: Context<CreateRoom>, code: [u8; 5], round: u32, params: RoomParams) -> Result<()> {
    require!(code.iter().all(|c| CODE_ALPHABET.contains(c)), EscrowError::BadCode);
    require!((2..=MAX_PLAYERS).contains(&params.max_players), EscrowError::BadMaxPlayers);
    require!(
        params.stake_unit > 0
            && params.min_stake > 0
            && params.min_stake <= params.max_stake
            && params.min_stake % params.stake_unit == 0
            && params.max_stake % params.stake_unit == 0
            && params.max_stake.checked_mul(MAX_PLAYERS as u64).is_some(),
        EscrowError::BadStakeRange
    );
    require!(params.cut_bps <= ctx.accounts.config.max_cut_bps, EscrowError::CutTooHigh);
    require!((MIN_TTL_SECS..=MAX_TTL_SECS).contains(&params.ttl_secs), EscrowError::BadTtl);

    let now = Clock::get()?.unix_timestamp;
    let expires_at = now.checked_add(params.ttl_secs).ok_or(EscrowError::Overflow)?;
    let mint = ctx.accounts.mint.as_ref().map(|m| m.key());
    let room = &mut ctx.accounts.room;
    room.set_inner(Room {
        code,
        round,
        mint,
        min_stake: params.min_stake,
        max_stake: params.max_stake,
        stake_unit: params.stake_unit,
        cut_bps: params.cut_bps,
        max_players: params.max_players,
        state: RoomState::Open,
        created_at: now,
        expires_at,
        payer: ctx.accounts.referee.key(),
        treasury: ctx.accounts.config.treasury,
        house_cut: 0,
        cut_paid: false,
        bump: ctx.bumps.room,
        entries: Vec::new(),
    });
    emit!(RoomCreated {
        room: room.key(),
        code,
        round,
        mint,
        min_stake: params.min_stake,
        max_stake: params.max_stake,
        max_players: params.max_players,
        cut_bps: params.cut_bps,
        expires_at,
    });
    Ok(())
}
