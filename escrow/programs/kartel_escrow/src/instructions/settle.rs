use anchor_lang::prelude::*;

use crate::error::EscrowError;
use crate::events::RoomSettled;
use crate::instructions::lock::RefereeRoom;
use crate::sidepot::{self, SplitError};
use crate::state::RoomState;

/// The referee reports the finishing order: `order` names every wallet in the race once, the winner first. The
/// payouts follow from the stakes and the rules in `sidepot`; the referee cannot choose them. Stakes left out at the
/// lock keep their full refund.
pub fn handle_settle(ctx: Context<RefereeRoom>, order: Vec<Pubkey>) -> Result<()> {
    let room = &mut ctx.accounts.room;
    require!(room.state == RoomState::Locked, EscrowError::NotLocked);
    let racing: Vec<usize> = (0..room.entries.len()).filter(|&i| !room.entries[i].refund).collect();
    require!(order.len() == racing.len(), EscrowError::BadRanking);
    // Each racer's place; a wallet named twice leaves another racer unplaced, which `split` refuses.
    let mut ranking = vec![u8::MAX; racing.len()];
    for (place, player) in order.iter().enumerate() {
        let entry = room.find(player).ok_or(EscrowError::BadRanking)?;
        let seat = racing.iter().position(|&i| i == entry).ok_or(EscrowError::BadRanking)?;
        require!(ranking[seat] == u8::MAX, EscrowError::BadRanking);
        ranking[seat] = place as u8;
    }
    let stakes: Vec<u64> = racing.iter().map(|&i| room.entries[i].stake).collect();
    let split = sidepot::split(&stakes, &ranking, room.cut_bps).map_err(|e| match e {
        SplitError::Seats | SplitError::Ranking => error!(EscrowError::BadRanking),
        SplitError::Cut => error!(EscrowError::CutTooHigh),
        SplitError::Overflow => error!(EscrowError::Overflow),
    })?;
    for (&i, payout) in racing.iter().zip(split.payouts) {
        room.entries[i].payout = payout;
    }
    room.house_cut = split.house;
    room.state = RoomState::Settled;
    emit!(RoomSettled {
        room: room.key(),
        order,
        payouts: room.entries.iter().map(|e| e.payout).collect(),
        house_cut: split.house,
    });
    Ok(())
}
