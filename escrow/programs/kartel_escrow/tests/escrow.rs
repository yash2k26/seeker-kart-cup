//! The escrow program end to end on LiteSVM, running the compiled `kartel_escrow.so`: every instruction, the exact
//! money each one moves, and every way it refuses. Build first (`anchor build`), then `cargo test`.

use anchor_lang::{
    prelude::{Clock, Pubkey},
    solana_program::{
        instruction::Instruction,
        program_option::COption,
        program_pack::Pack,
        system_program,
    },
    AccountDeserialize, InstructionData, ToAccountMetas,
};
use anchor_spl::{
    associated_token::{get_associated_token_address, spl_associated_token_account},
    token::spl_token,
};
use kartel_escrow::{
    error::EscrowError, instructions::ConfigUpdate, instructions::RoomParams, state::Room, state::RoomState,
};
use litesvm::LiteSVM;
use solana_account::Account;
use solana_keypair::Keypair;
use solana_message::{Message, VersionedMessage};
use solana_signer::Signer;
use solana_transaction::versioned::VersionedTransaction;

const SOL: u64 = 1_000_000_000;
const FEE: u64 = 5_000; // per signature
const CODE: [u8; 5] = *b"K7P2Q";
const UPGRADEABLE_LOADER: Pubkey = Pubkey::from_str_const("BPFLoaderUpgradeab1e11111111111111111111111");

fn custom(e: EscrowError) -> String {
    format!("Custom({})", e as u32 + 6000)
}

#[track_caller]
fn expect_err(res: Result<(), String>, e: EscrowError) {
    let err = res.expect_err("the program should have refused");
    assert!(err.contains(&custom(e)), "expected {e:?} ({}), got {err}", custom(e));
}

#[derive(Clone, Copy)]
struct Params {
    min: u64,
    max: u64,
    unit: u64,
    players: u8,
    cut: u16,
    ttl: i64,
}

impl Default for Params {
    fn default() -> Self {
        Params { min: SOL / 100, max: SOL, unit: SOL / 100, players: 6, cut: 1_000, ttl: 3_600 }
    }
}

/// A token room's terms: whole tokens of a 6-decimal mint.
fn token_params(min: u64, max: u64) -> Params {
    Params { min, max, unit: 1_000_000, ..Params::default() }
}

struct World {
    svm: LiteSVM,
    program: Pubkey,
    config: Pubkey,
    admin: Keypair,
    referee: Keypair,
    treasury: Keypair,
}

impl World {
    /// The program loaded as an upgradeable program whose upgrade authority is `admin`, and its config initialized.
    fn new() -> Self {
        let w = Self::bare();
        let mut w = w;
        let init = w.initialize_ix(&w.admin.pubkey());
        w.send(&[init], &[&w.admin.insecure_clone()]).unwrap();
        w
    }

    fn bare() -> Self {
        let mut svm = LiteSVM::new();
        let program = kartel_escrow::id();
        let bytes = include_bytes!(concat!(env!("CARGO_TARGET_TMPDIR"), "/../deploy/kartel_escrow.so"));
        svm.add_program(program, bytes).unwrap();
        let (admin, referee, treasury) = (Keypair::new(), Keypair::new(), Keypair::new());
        for k in [&admin, &referee, &treasury] {
            svm.airdrop(&k.pubkey(), 100 * SOL).unwrap();
        }
        // LiteSVM writes ProgramData with no upgrade authority: bincode layout is u32 tag, u64 slot, Option<Pubkey>.
        let (programdata, _) = Pubkey::find_program_address(&[program.as_ref()], &UPGRADEABLE_LOADER);
        let mut acc = svm.get_account(&programdata).unwrap();
        acc.data[12] = 1;
        acc.data[13..45].copy_from_slice(admin.pubkey().as_ref());
        svm.set_account(programdata, acc).unwrap();
        let config = Pubkey::find_program_address(&[kartel_escrow::CONFIG_SEED], &program).0;
        World { svm, program, config, admin, referee, treasury }
    }

    fn initialize_ix(&self, signer: &Pubkey) -> Instruction {
        let programdata = Pubkey::find_program_address(&[self.program.as_ref()], &UPGRADEABLE_LOADER).0;
        self.ix(
            kartel_escrow::instruction::Initialize {
                referee: self.referee.pubkey(),
                treasury: self.treasury.pubkey(),
                max_cut_bps: 1_500,
            },
            kartel_escrow::accounts::Initialize {
                admin: *signer,
                config: self.config,
                program: self.program,
                program_data: programdata,
                system_program: system_program::ID,
            },
        )
    }

    fn ix(&self, data: impl InstructionData, accounts: impl ToAccountMetas) -> Instruction {
        Instruction::new_with_bytes(self.program, &data.data(), accounts.to_account_metas(None))
    }

    /// Sends with a fresh blockhash, so the same instruction can be sent twice to test that the second is refused.
    fn send(&mut self, ixs: &[Instruction], signers: &[&Keypair]) -> Result<(), String> {
        self.svm.expire_blockhash();
        let msg = Message::new_with_blockhash(ixs, Some(&signers[0].pubkey()), &self.svm.latest_blockhash());
        let tx = VersionedTransaction::try_new(VersionedMessage::Legacy(msg), signers).unwrap();
        self.svm.send_transaction(tx).map(|_| ()).map_err(|e| format!("{:?} {:?}", e.err, e.meta.logs))
    }

    fn player(&mut self) -> Keypair {
        let k = Keypair::new();
        self.svm.airdrop(&k.pubkey(), 10 * SOL).unwrap();
        k
    }

    fn balance(&self, key: &Pubkey) -> u64 {
        self.svm.get_account(key).map(|a| a.lamports).unwrap_or(0)
    }

    fn room_key(&self, round: u32) -> Pubkey {
        Pubkey::find_program_address(&[kartel_escrow::ROOM_SEED, &CODE, &round.to_le_bytes()], &self.program).0
    }

    fn room(&self, key: &Pubkey) -> Room {
        let acc = self.svm.get_account(key).expect("room account");
        Room::try_deserialize(&mut acc.data.as_slice()).unwrap()
    }

    fn warp(&mut self, secs: i64) {
        let mut clock: Clock = self.svm.get_sysvar();
        clock.unix_timestamp += secs;
        self.svm.set_sysvar(&clock);
    }

    // ---- tokens ----
    fn mint(&mut self, decimals: u8) -> Pubkey {
        let mint = Pubkey::new_unique();
        let mut data = vec![0u8; spl_token::state::Mint::LEN];
        spl_token::state::Mint {
            mint_authority: COption::None,
            supply: u64::MAX / 2,
            decimals,
            is_initialized: true,
            freeze_authority: COption::None,
        }
        .pack_into_slice(&mut data);
        self.put(mint, data, spl_token::ID);
        mint
    }

    /// `owner`'s associated token account for `mint`, holding `amount`.
    fn token_account(&mut self, owner: &Pubkey, mint: &Pubkey, amount: u64) -> Pubkey {
        let ata = get_associated_token_address(owner, mint);
        let mut data = vec![0u8; spl_token::state::Account::LEN];
        spl_token::state::Account {
            mint: *mint,
            owner: *owner,
            amount,
            delegate: COption::None,
            state: spl_token::state::AccountState::Initialized,
            is_native: COption::None,
            delegated_amount: 0,
            close_authority: COption::None,
        }
        .pack_into_slice(&mut data);
        self.put(ata, data, spl_token::ID);
        ata
    }

    fn put(&mut self, key: Pubkey, data: Vec<u8>, owner: Pubkey) {
        let lamports = self.svm.minimum_balance_for_rent_exemption(data.len());
        self.svm.set_account(key, Account { lamports, data, owner, executable: false, rent_epoch: 0 }).unwrap();
    }

    fn tokens(&self, account: &Pubkey) -> u64 {
        let acc = self.svm.get_account(account).expect("token account");
        spl_token::state::Account::unpack(&acc.data).unwrap().amount
    }

    /// (mint, vault, holder's account, token program) for a token room; all `None` for SOL.
    fn legs(&self, room: &Pubkey, holder: &Pubkey, mint: Option<Pubkey>) -> [Option<Pubkey>; 4] {
        match mint {
            None => [None; 4],
            Some(m) => [
                Some(m),
                Some(get_associated_token_address(room, &m)),
                Some(get_associated_token_address(holder, &m)),
                Some(spl_token::ID),
            ],
        }
    }

    // ---- the program's instructions ----
    fn create_room_ixs(&self, round: u32, p: Params, mint: Option<Pubkey>) -> Vec<Instruction> {
        let room = self.room_key(round);
        let mut ixs = vec![self.ix(
            kartel_escrow::instruction::CreateRoom {
                code: CODE,
                round,
                params: RoomParams {
                    min_stake: p.min,
                    max_stake: p.max,
                    stake_unit: p.unit,
                    max_players: p.players,
                    cut_bps: p.cut,
                    ttl_secs: p.ttl,
                },
            },
            kartel_escrow::accounts::CreateRoom {
                referee: self.referee.pubkey(),
                config: self.config,
                room,
                mint,
                system_program: system_program::ID,
            },
        )];
        if let Some(m) = mint {
            ixs.push(spl_associated_token_account::instruction::create_associated_token_account_idempotent(
                &self.referee.pubkey(),
                &room,
                &m,
                &spl_token::ID,
            ));
        }
        ixs
    }

    fn create_room(&mut self, round: u32, p: Params, mint: Option<Pubkey>) -> Pubkey {
        let ixs = self.create_room_ixs(round, p, mint);
        let referee = self.referee.insecure_clone();
        self.send(&ixs, &[&referee]).unwrap();
        self.room_key(round)
    }

    fn join_ix(&self, room: Pubkey, player: &Pubkey, stake: u64, mint: Option<Pubkey>) -> Instruction {
        let [mint, vault, player_token, token_program] = self.legs(&room, player, mint);
        self.ix(
            kartel_escrow::instruction::Join { stake },
            kartel_escrow::accounts::Join {
                player: *player,
                config: self.config,
                room,
                mint,
                vault,
                player_token,
                token_program,
                system_program: system_program::ID,
            },
        )
    }

    fn join(&mut self, room: Pubkey, player: &Keypair, stake: u64, mint: Option<Pubkey>) -> Result<(), String> {
        let ix = self.join_ix(room, &player.pubkey(), stake, mint);
        self.send(&[ix], &[player])
    }

    fn leave(&mut self, room: Pubkey, player: &Keypair, mint: Option<Pubkey>) -> Result<(), String> {
        let [mint, vault, player_token, token_program] = self.legs(&room, &player.pubkey(), mint);
        let ix = self.ix(
            kartel_escrow::instruction::Leave {},
            kartel_escrow::accounts::Leave { player: player.pubkey(), room, mint, vault, player_token, token_program },
        );
        self.send(&[ix], &[player])
    }

    fn remove(&mut self, room: Pubkey, signer: &Keypair, player: &Pubkey, mint: Option<Pubkey>) -> Result<(), String> {
        let [mint, vault, player_token, token_program] = self.legs(&room, player, mint);
        let ix = self.ix(
            kartel_escrow::instruction::RemoveEntry {},
            kartel_escrow::accounts::RemoveEntry {
                referee: signer.pubkey(),
                config: self.config,
                room,
                player: *player,
                mint,
                vault,
                player_token,
                token_program,
            },
        );
        self.send(&[ix], &[signer])
    }

    fn referee_room(&self, room: Pubkey, signer: &Pubkey) -> kartel_escrow::accounts::RefereeRoom {
        kartel_escrow::accounts::RefereeRoom { referee: *signer, config: self.config, room }
    }

    /// Locks with the field as it stands on-chain.
    fn lock(&mut self, room: Pubkey, signer: &Keypair) -> Result<(), String> {
        let players = self.svm.get_account(&room).map_or(vec![], |_| self.room(&room).entries.iter().map(|e| e.player).collect());
        self.lock_with(room, signer, players)
    }

    fn lock_with(&mut self, room: Pubkey, signer: &Keypair, players: Vec<Pubkey>) -> Result<(), String> {
        let ix = self.ix(kartel_escrow::instruction::Lock { players }, self.referee_room(room, &signer.pubkey()));
        self.send(&[ix], &[signer])
    }

    /// `ranking[i]` is entry i's place (0 = winner), turned into the wallets in finishing order.
    fn settle(&mut self, room: Pubkey, signer: &Keypair, ranking: Vec<u8>) -> Result<(), String> {
        let entries = self.room(&room).entries;
        let mut order = vec![Pubkey::default(); ranking.len()];
        for (seat, &place) in ranking.iter().enumerate() {
            order[place as usize] = entries[seat].player;
        }
        self.settle_order(room, signer, order)
    }

    fn settle_order(&mut self, room: Pubkey, signer: &Keypair, order: Vec<Pubkey>) -> Result<(), String> {
        let ix = self.ix(kartel_escrow::instruction::Settle { order }, self.referee_room(room, &signer.pubkey()));
        self.send(&[ix], &[signer])
    }

    fn cancel(&mut self, room: Pubkey, signer: &Keypair) -> Result<(), String> {
        let ix = self.ix(
            kartel_escrow::instruction::Cancel {},
            kartel_escrow::accounts::Cancel { caller: signer.pubkey(), config: self.config, room },
        );
        self.send(&[ix], &[signer])
    }

    /// Sent by `cranker` (anyone), paid to `player`.
    fn claim(&mut self, room: Pubkey, cranker: &Keypair, player: &Pubkey, mint: Option<Pubkey>) -> Result<(), String> {
        let [mint, vault, player_token, token_program] = self.legs(&room, player, mint);
        let ix = self.ix(
            kartel_escrow::instruction::Claim {},
            kartel_escrow::accounts::Claim { room, player: *player, mint, vault, player_token, token_program },
        );
        self.send(&[ix], &[cranker])
    }

    fn collect(&mut self, room: Pubkey, cranker: &Keypair, mint: Option<Pubkey>) -> Result<(), String> {
        let treasury = self.treasury.pubkey();
        let [mint, vault, treasury_token, token_program] = self.legs(&room, &treasury, mint);
        let ix = self.ix(
            kartel_escrow::instruction::CollectCut {},
            kartel_escrow::accounts::CollectCut { room, treasury, mint, vault, treasury_token, token_program },
        );
        self.send(&[ix], &[cranker])
    }

    fn close(&mut self, room: Pubkey, cranker: &Keypair, mint: Option<Pubkey>) -> Result<(), String> {
        let treasury = self.treasury.pubkey();
        let [mint, vault, treasury_token, token_program] = self.legs(&room, &treasury, mint);
        let ix = self.ix(
            kartel_escrow::instruction::CloseRoom {},
            kartel_escrow::accounts::CloseRoom {
                room,
                payer: self.referee.pubkey(),
                mint,
                vault,
                treasury_token,
                token_program,
            },
        );
        self.send(&[ix], &[cranker])
    }

    fn update(&mut self, signer: &Keypair, update: ConfigUpdate) -> Result<(), String> {
        let ix = self.ix(
            kartel_escrow::instruction::UpdateConfig { update },
            kartel_escrow::accounts::UpdateConfig { admin: signer.pubkey(), config: self.config },
        );
        self.send(&[ix], &[signer])
    }

    fn referee(&self) -> Keypair {
        self.referee.insecure_clone()
    }
}

#[test]
fn sol_room_pays_fair_shares_and_closes() {
    let mut w = World::new();
    let referee = w.referee();
    let referee_start = w.balance(&referee.pubkey());
    let treasury_start = w.balance(&w.treasury.pubkey());
    let room = w.create_room(1, Params::default(), None);
    let (a, b, c) = (w.player(), w.player(), w.player());
    let start = [a.pubkey(), b.pubkey(), c.pubkey()].map(|k| w.balance(&k));

    w.join(room, &a, SOL, None).unwrap();
    w.join(room, &b, SOL / 10, None).unwrap();
    w.join(room, &c, SOL / 10, None).unwrap();
    let rent = w.svm.minimum_balance_for_rent_exemption(8 + <Room as anchor_lang::Space>::INIT_SPACE);
    assert_eq!(w.balance(&room), rent + 1_200_000_000, "the room holds every stake");

    w.lock(room, &referee).unwrap();
    w.settle(room, &referee, vec![0, 1, 2]).unwrap();
    let r = w.room(&room);
    assert_eq!(r.state, RoomState::Settled);
    assert_eq!(r.entries.iter().map(|e| e.payout).collect::<Vec<_>>(), vec![1_035_000_000, 81_000_000, 54_000_000]);
    assert_eq!(r.house_cut, 30_000_000);

    for p in [&a, &b, &c] {
        w.claim(room, &referee, &p.pubkey(), None).unwrap();
    }
    w.collect(room, &referee, None).unwrap();
    w.close(room, &referee, None).unwrap();

    assert_eq!(w.balance(&a.pubkey()), start[0] - FEE - SOL + 1_035_000_000);
    assert_eq!(w.balance(&b.pubkey()), start[1] - FEE - SOL / 10 + 81_000_000);
    assert_eq!(w.balance(&c.pubkey()), start[2] - FEE - SOL / 10 + 54_000_000);
    assert_eq!(w.balance(&w.treasury.pubkey()), treasury_start + 30_000_000);
    assert_eq!(w.balance(&room), 0, "closed");
    // The referee paid the rent and every fee (create, lock, settle, 3 claims, collect, close) and got the rent back.
    assert_eq!(w.balance(&referee.pubkey()), referee_start - 8 * FEE);
}

#[test]
fn token_room_pays_fair_shares_and_closes() {
    let mut w = World::new();
    let referee = w.referee();
    let mint = w.mint(6);
    let treasury_ata = w.token_account(&w.treasury.pubkey(), &mint, 0);
    let p = token_params(10_000_000, 500_000_000);
    let room = w.create_room(1, p, Some(mint));
    let vault = get_associated_token_address(&room, &mint);
    let players: Vec<Keypair> = (0..4).map(|_| w.player()).collect();
    let stakes = [50_000_000u64, 200_000_000, 100_000_000, 200_000_000];
    let accounts: Vec<Pubkey> = players.iter().map(|k| w.token_account(&k.pubkey(), &mint, 1_000_000_000)).collect();
    for (k, &s) in players.iter().zip(&stakes) {
        w.join(room, k, s, Some(mint)).unwrap();
    }
    assert_eq!(w.tokens(&vault), 550_000_000);

    w.lock(room, &referee).unwrap();
    w.settle(room, &referee, vec![3, 0, 1, 2]).unwrap();
    for k in &players {
        w.claim(room, &referee, &k.pubkey(), Some(mint)).unwrap();
    }
    w.collect(room, &referee, Some(mint)).unwrap();
    w.close(room, &referee, Some(mint)).unwrap();

    // The "four players, three levels" vector at a million times the scale: no rounding left at this size.
    let want = [0u64, 337_500_000, 94_500_000, 63_000_000];
    for ((acc, &s), &got) in accounts.iter().zip(&stakes).zip(&want) {
        assert_eq!(w.tokens(acc), 1_000_000_000 - s + got);
    }
    assert_eq!(w.tokens(&treasury_ata), 55_000_000);
    assert!(w.svm.get_account(&vault).map_or(true, |a| a.lamports == 0), "the vault is closed");
    assert_eq!(w.balance(&room), 0);
}

#[test]
fn seats_cap_at_six() {
    let mut w = World::new();
    let referee = w.referee();
    let room = w.create_room(1, Params::default(), None);
    for _ in 0..6 {
        let p = w.player();
        w.join(room, &p, SOL / 100, None).unwrap();
    }
    let seventh = w.player();
    expect_err(w.join(room, &seventh, SOL / 100, None), EscrowError::RoomFull);

    for players in [0u8, 1, 7] {
        let ixs = w.create_room_ixs(2 + players as u32, Params { players, ..Params::default() }, None);
        expect_err(w.send(&ixs, &[&referee]), EscrowError::BadMaxPlayers);
    }
    // A smaller room fills at its own size.
    let small = w.create_room(20, Params { players: 2, ..Params::default() }, None);
    let (a, b, c) = (w.player(), w.player(), w.player());
    w.join(small, &a, SOL / 100, None).unwrap();
    w.join(small, &b, SOL / 100, None).unwrap();
    expect_err(w.join(small, &c, SOL / 100, None), EscrowError::RoomFull);
}

#[test]
fn stakes_must_fit_the_room() {
    let mut w = World::new();
    let room = w.create_room(1, Params::default(), None);
    let p = w.player();
    expect_err(w.join(room, &p, SOL / 100 - 1, None), EscrowError::StakeOutOfRange);
    expect_err(w.join(room, &p, SOL + 1, None), EscrowError::StakeOutOfRange);
    expect_err(w.join(room, &p, SOL / 100 + 1, None), EscrowError::StakeOutOfRange);   // not a whole number of units
    w.join(room, &p, SOL / 2, None).unwrap();
    expect_err(w.join(room, &p, SOL / 2, None), EscrowError::AlreadyJoined);
}

#[test]
fn players_can_leave_an_open_room_but_not_a_locked_one() {
    let mut w = World::new();
    let referee = w.referee();
    let room = w.create_room(1, Params::default(), None);
    let (a, b) = (w.player(), w.player());
    let start = w.balance(&a.pubkey());
    w.join(room, &a, SOL / 2, None).unwrap();
    w.leave(room, &a, None).unwrap();
    assert_eq!(w.balance(&a.pubkey()), start - 2 * FEE, "the whole stake came back");
    assert!(w.room(&room).entries.is_empty());
    expect_err(w.leave(room, &a, None), EscrowError::NoEntry);

    w.join(room, &a, SOL / 2, None).unwrap();
    w.join(room, &b, SOL / 2, None).unwrap();
    w.lock(room, &referee).unwrap();
    expect_err(w.leave(room, &a, None), EscrowError::NotOpen);
}

#[test]
fn only_the_referee_removes_seats_and_only_to_the_staker() {
    let mut w = World::new();
    let referee = w.referee();
    let mint = w.mint(6);
    let p = token_params(1_000_000, 100_000_000);
    let room = w.create_room(1, p, Some(mint));
    let a = w.player();
    let a_tokens = w.token_account(&a.pubkey(), &mint, 50_000_000);
    w.join(room, &a, 20_000_000, Some(mint)).unwrap();

    let stranger = w.player();
    expect_err(w.remove(room, &stranger, &a.pubkey(), Some(mint)), EscrowError::NotReferee);
    // The refund can only go to the staker: naming someone else finds no entry.
    let other = w.player();
    w.token_account(&other.pubkey(), &mint, 0);
    expect_err(w.remove(room, &referee, &other.pubkey(), Some(mint)), EscrowError::NoEntry);

    w.remove(room, &referee, &a.pubkey(), Some(mint)).unwrap();
    assert_eq!(w.tokens(&a_tokens), 50_000_000);
    assert!(w.room(&room).entries.is_empty());
}

#[test]
fn lock_needs_two_and_closes_the_door() {
    let mut w = World::new();
    let referee = w.referee();
    let room = w.create_room(1, Params::default(), None);
    let (a, b, c) = (w.player(), w.player(), w.player());
    w.join(room, &a, SOL / 10, None).unwrap();
    expect_err(w.lock(room, &referee), EscrowError::TooFewPlayers);
    w.join(room, &b, SOL / 10, None).unwrap();
    let stranger = w.player();
    expect_err(w.lock(room, &stranger), EscrowError::NotReferee);
    w.lock(room, &referee).unwrap();
    expect_err(w.join(room, &c, SOL / 10, None), EscrowError::NotOpen);
    expect_err(w.lock(room, &referee), EscrowError::NotOpen);
}

#[test]
fn a_stake_left_out_of_the_lock_goes_back_whole_and_at_once() {
    let mut w = World::new();
    let referee = w.referee();
    let room = w.create_room(1, Params::default(), None);
    let (a, b, c) = (w.player(), w.player(), w.player());
    let c_start = w.balance(&c.pubkey());
    for p in [&a, &b, &c] {
        w.join(room, p, SOL / 10, None).unwrap();
    }
    let stranger = w.player();
    expect_err(w.lock_with(room, &referee, vec![a.pubkey()]), EscrowError::TooFewPlayers);
    expect_err(w.lock_with(room, &referee, vec![a.pubkey(), stranger.pubkey()]), EscrowError::BadField);
    expect_err(w.lock_with(room, &referee, vec![a.pubkey(), a.pubkey()]), EscrowError::BadField);
    // C staked on the way to the lock (or never reached the grid): it is left out.
    w.lock_with(room, &referee, vec![b.pubkey(), a.pubkey()]).unwrap();
    let r = w.room(&room);
    assert_eq!(r.state, RoomState::Locked);
    assert!(r.entries[2].refund && r.entries[2].payout == SOL / 10 && !r.entries[0].refund);
    expect_err(w.claim(room, &referee, &a.pubkey(), None), EscrowError::NotFinished);
    w.claim(room, &referee, &c.pubkey(), None).unwrap();
    assert_eq!(w.balance(&c.pubkey()), c_start - FEE, "C's whole stake is back before the race is run");
    // The race is between A and B only: C cannot be ranked, and B's win is the two stakes in it less the cut.
    expect_err(w.settle_order(room, &referee, vec![b.pubkey(), a.pubkey(), c.pubkey()]), EscrowError::BadRanking);
    expect_err(w.settle_order(room, &referee, vec![c.pubkey(), a.pubkey()]), EscrowError::BadRanking);
    w.settle_order(room, &referee, vec![b.pubkey(), a.pubkey()]).unwrap();
    let r = w.room(&room);
    assert_eq!(r.entries.iter().map(|e| e.payout).collect::<Vec<_>>(), vec![0, 180_000_000, 100_000_000]);
    assert_eq!(r.house_cut, 20_000_000);
    w.claim(room, &referee, &b.pubkey(), None).unwrap();
    w.claim(room, &referee, &a.pubkey(), None).unwrap();
    w.collect(room, &referee, None).unwrap();
    w.close(room, &referee, None).unwrap();
}

#[test]
fn stakes_come_from_the_players_own_token_account() {
    let mut w = World::new();
    let mint = w.mint(6);
    let room = w.create_room(1, token_params(1_000_000, 100_000_000), Some(mint));
    let a = w.player();
    // A token account A owns that is not its associated one: refunds and payouts could never find it.
    let other = Pubkey::new_unique();
    let mut data = vec![0u8; spl_token::state::Account::LEN];
    spl_token::state::Account {
        mint,
        owner: a.pubkey(),
        amount: 50_000_000,
        delegate: COption::None,
        state: spl_token::state::AccountState::Initialized,
        is_native: COption::None,
        delegated_amount: 0,
        close_authority: COption::None,
    }
    .pack_into_slice(&mut data);
    w.put(other, data, spl_token::ID);
    let mut ix = w.join_ix(room, &a.pubkey(), 10_000_000, Some(mint));
    ix.accounts[5].pubkey = other;
    assert!(w.send(&[ix], &[&a]).is_err());
    assert!(w.room(&room).entries.is_empty());
}

#[test]
fn settle_takes_a_full_ranking_from_the_referee_once() {
    let mut w = World::new();
    let referee = w.referee();
    let room = w.create_room(1, Params::default(), None);
    let (a, b, c) = (w.player(), w.player(), w.player());
    for p in [&a, &b, &c] {
        w.join(room, p, SOL / 10, None).unwrap();
    }
    let (pa, pb, pc) = (a.pubkey(), b.pubkey(), c.pubkey());
    expect_err(w.settle_order(room, &referee, vec![pa, pb, pc]), EscrowError::NotLocked);
    w.lock(room, &referee).unwrap();
    let stranger = w.player();
    expect_err(w.settle_order(room, &stranger, vec![pa, pb, pc]), EscrowError::NotReferee);
    expect_err(w.settle_order(room, &referee, vec![pa, pa, pb]), EscrowError::BadRanking);
    expect_err(w.settle_order(room, &referee, vec![pa, pb]), EscrowError::BadRanking);
    expect_err(w.settle_order(room, &referee, vec![pa, pb, stranger.pubkey()]), EscrowError::BadRanking);
    w.settle_order(room, &referee, vec![pb, pc, pa]).unwrap();
    expect_err(w.settle(room, &referee, vec![0, 1, 2]), EscrowError::NotLocked);
    let r = w.room(&room);
    assert_eq!(r.entries.iter().map(|e| e.payout).collect::<Vec<_>>(), vec![54_000_000, 135_000_000, 81_000_000]);
}

#[test]
fn anyone_can_cancel_after_expiry_and_everyone_is_refunded() {
    let mut w = World::new();
    let referee = w.referee();
    let room = w.create_room(1, Params { ttl: 600, ..Params::default() }, None);
    let (a, b) = (w.player(), w.player());
    let start = [a.pubkey(), b.pubkey()].map(|k| w.balance(&k));
    w.join(room, &a, SOL / 4, None).unwrap();
    w.join(room, &b, SOL / 2, None).unwrap();
    w.lock(room, &referee).unwrap();

    let stranger = w.player();
    expect_err(w.cancel(room, &stranger), EscrowError::CannotCancelYet);
    w.warp(600);
    w.cancel(room, &stranger).unwrap();
    expect_err(w.cancel(room, &referee), EscrowError::AlreadyFinished);
    expect_err(w.collect(room, &stranger, None), EscrowError::NotFinished);

    // The stranger delivers both refunds and closes the room; the rent still goes to the referee who paid it.
    let referee_before = w.balance(&referee.pubkey());
    w.claim(room, &stranger, &a.pubkey(), None).unwrap();
    w.claim(room, &stranger, &b.pubkey(), None).unwrap();
    w.close(room, &stranger, None).unwrap();
    assert_eq!(w.balance(&a.pubkey()), start[0] - FEE);
    assert_eq!(w.balance(&b.pubkey()), start[1] - FEE);
    assert!(w.balance(&referee.pubkey()) > referee_before, "rent back to the payer");
}

#[test]
fn joining_or_locking_an_expired_room_is_refused() {
    let mut w = World::new();
    let referee = w.referee();
    let room = w.create_room(1, Params { ttl: 600, ..Params::default() }, None);
    let (a, b) = (w.player(), w.player());
    w.join(room, &a, SOL / 10, None).unwrap();
    w.warp(601);
    expect_err(w.join(room, &b, SOL / 10, None), EscrowError::Expired);
    expect_err(w.lock(room, &referee), EscrowError::Expired);
    // ...but the player can still leave with their stake.
    w.leave(room, &a, None).unwrap();
}

#[test]
fn the_referee_can_cancel_an_open_room() {
    let mut w = World::new();
    let referee = w.referee();
    let room = w.create_room(1, Params::default(), None);
    let a = w.player();
    w.join(room, &a, SOL / 10, None).unwrap();
    w.cancel(room, &referee).unwrap();
    assert_eq!(w.room(&room).state, RoomState::Cancelled);
    expect_err(w.join(room, &a, SOL / 10, None), EscrowError::NotOpen);
    w.claim(room, &referee, &a.pubkey(), None).unwrap();
    w.close(room, &referee, None).unwrap();
}

#[test]
fn claims_are_once_and_only_to_the_staker() {
    let mut w = World::new();
    let referee = w.referee();
    let room = w.create_room(1, Params::default(), None);
    let (a, b) = (w.player(), w.player());
    w.join(room, &a, SOL / 10, None).unwrap();
    w.join(room, &b, SOL / 10, None).unwrap();
    expect_err(w.claim(room, &referee, &a.pubkey(), None), EscrowError::NotFinished);
    w.lock(room, &referee).unwrap();
    w.settle(room, &referee, vec![1, 0]).unwrap();
    let stranger = w.player();
    expect_err(w.claim(room, &referee, &stranger.pubkey(), None), EscrowError::NoEntry);
    w.claim(room, &referee, &b.pubkey(), None).unwrap();
    expect_err(w.claim(room, &referee, &b.pubkey(), None), EscrowError::AlreadyPaid);
    // The loser's zero payout can be marked delivered too; it moves nothing.
    let before = w.balance(&a.pubkey());
    w.claim(room, &referee, &a.pubkey(), None).unwrap();
    assert_eq!(w.balance(&a.pubkey()), before);
}

#[test]
fn close_waits_until_nothing_is_owed() {
    let mut w = World::new();
    let referee = w.referee();
    let room = w.create_room(1, Params::default(), None);
    let (a, b) = (w.player(), w.player());
    w.join(room, &a, SOL / 10, None).unwrap();
    w.join(room, &b, SOL / 10, None).unwrap();
    expect_err(w.close(room, &referee, None), EscrowError::NotFinished);
    w.lock(room, &referee).unwrap();
    w.settle(room, &referee, vec![0, 1]).unwrap();
    expect_err(w.close(room, &referee, None), EscrowError::PayoutsOwed);
    w.claim(room, &referee, &a.pubkey(), None).unwrap();
    expect_err(w.close(room, &referee, None), EscrowError::PayoutsOwed);
    w.collect(room, &referee, None).unwrap();
    expect_err(w.collect(room, &referee, None), EscrowError::NothingToCollect);
    w.close(room, &referee, None).unwrap();
}

#[test]
fn only_the_upgrade_authority_initializes() {
    let mut w = World::bare();
    let impostor = Keypair::new();
    w.svm.airdrop(&impostor.pubkey(), SOL).unwrap();
    let ix = w.initialize_ix(&impostor.pubkey());
    expect_err(w.send(&[ix], &[&impostor]), EscrowError::NotUpgradeAuthority);
    let admin = w.admin.insecure_clone();
    let ix = w.initialize_ix(&admin.pubkey());
    w.send(&[ix], &[&admin]).unwrap();
    let ix = w.initialize_ix(&admin.pubkey());
    assert!(w.send(&[ix], &[&admin]).is_err(), "initialized once");
}

#[test]
fn pausing_stops_new_money_but_never_refunds() {
    let mut w = World::new();
    let referee = w.referee();
    let admin = w.admin.insecure_clone();
    let room = w.create_room(1, Params::default(), None);
    let (a, b) = (w.player(), w.player());
    w.join(room, &a, SOL / 10, None).unwrap();

    w.update(&admin, ConfigUpdate { paused: Some(true), ..Default::default() }).unwrap();
    let ixs = w.create_room_ixs(2, Params::default(), None);
    expect_err(w.send(&ixs, &[&referee]), EscrowError::Paused);
    expect_err(w.join(room, &b, SOL / 10, None), EscrowError::Paused);
    w.leave(room, &a, None).unwrap();

    w.update(&admin, ConfigUpdate { paused: Some(false), ..Default::default() }).unwrap();
    w.join(room, &b, SOL / 10, None).unwrap();
}

#[test]
fn config_changes_are_the_admins_and_stay_in_bounds() {
    let mut w = World::new();
    let admin = w.admin.insecure_clone();
    let stranger = w.player();
    expect_err(w.update(&stranger, ConfigUpdate { paused: Some(true), ..Default::default() }), EscrowError::NotAdmin);
    expect_err(w.update(&admin, ConfigUpdate { max_cut_bps: Some(2_001), ..Default::default() }), EscrowError::CutTooHigh);
    // A new referee takes over at once; the old one can no longer create rooms.
    let old = w.referee();
    let new = w.player();
    w.update(&admin, ConfigUpdate { referee: Some(new.pubkey()), ..Default::default() }).unwrap();
    let ixs = w.create_room_ixs(1, Params::default(), None);
    expect_err(w.send(&ixs, &[&old]), EscrowError::NotReferee);
}

#[test]
fn create_room_checks_every_parameter() {
    let mut w = World::new();
    let referee = w.referee();
    let bad = [
        (Params { min: 0, ..Params::default() }, EscrowError::BadStakeRange),
        (Params { min: 2, max: 1, unit: 1, ..Params::default() }, EscrowError::BadStakeRange),
        (Params { max: u64::MAX / 5, unit: 1, ..Params::default() }, EscrowError::BadStakeRange),
        (Params { unit: 0, ..Params::default() }, EscrowError::BadStakeRange),
        (Params { min: SOL / 100 + 1, ..Params::default() }, EscrowError::BadStakeRange),
        (Params { max: SOL + 1, ..Params::default() }, EscrowError::BadStakeRange),
        (Params { cut: 1_501, ..Params::default() }, EscrowError::CutTooHigh),
        (Params { ttl: 599, ..Params::default() }, EscrowError::BadTtl),
        (Params { ttl: 24 * 3_600 + 1, ..Params::default() }, EscrowError::BadTtl),
    ];
    for (i, (p, e)) in bad.into_iter().enumerate() {
        let ixs = w.create_room_ixs(100 + i as u32, p, None);
        expect_err(w.send(&ixs, &[&referee]), e);
    }
    // A code outside the alphabet ("0" and "O" are left out on purpose).
    let mut ixs = w.create_room_ixs(1, Params::default(), None);
    let code_at = 8; // after the discriminator
    ixs[0].data[code_at..code_at + 5].copy_from_slice(b"K0P2Q");
    ixs[0].accounts[2].pubkey =
        Pubkey::find_program_address(&[kartel_escrow::ROOM_SEED, b"K0P2Q", &1u32.to_le_bytes()], &w.program).0;
    expect_err(w.send(&ixs, &[&referee]), EscrowError::BadCode);

    let stranger = w.player();
    let mut ixs = w.create_room_ixs(1, Params::default(), None);
    ixs[0].accounts[0].pubkey = stranger.pubkey();
    expect_err(w.send(&ixs, &[&stranger]), EscrowError::NotReferee);

    w.create_room(1, Params::default(), None);
    let ixs = w.create_room_ixs(1, Params::default(), None);
    assert!(w.send(&ixs, &[&referee]).is_err(), "one room per code and round");
}

#[test]
fn currency_accounts_must_match_the_room() {
    let mut w = World::new();
    let mint = w.mint(6);
    let other_mint = w.mint(6);
    let sol_room = w.create_room(1, Params::default(), None);
    let p = token_params(1_000_000, 100_000_000);
    let token_room = w.create_room(2, p, Some(mint));
    let a = w.player();
    w.token_account(&a.pubkey(), &mint, 50_000_000);
    w.token_account(&a.pubkey(), &other_mint, 50_000_000);

    // Token accounts on a SOL room, none on a token room, another mint on a token room.
    let ix = w.join_ix(sol_room, &a.pubkey(), SOL / 10, Some(mint));
    assert!(w.send(&[ix], &[&a]).is_err());
    expect_err(w.join(token_room, &a, 10_000_000, None), EscrowError::WrongCurrencyAccounts);
    let ix = w.join_ix(token_room, &a.pubkey(), 10_000_000, Some(other_mint));
    assert!(w.send(&[ix], &[&a]).is_err());
    assert!(w.room(&token_room).entries.is_empty() && w.room(&sol_room).entries.is_empty());
    w.join(token_room, &a, 10_000_000, Some(mint)).unwrap();
}

#[test]
fn tokens_sent_straight_to_the_vault_go_to_the_treasury_on_close() {
    let mut w = World::new();
    let referee = w.referee();
    let mint = w.mint(6);
    let treasury_ata = w.token_account(&w.treasury.pubkey(), &mint, 0);
    let p = token_params(1_000_000, 100_000_000);
    let room = w.create_room(1, p, Some(mint));
    let (a, b) = (w.player(), w.player());
    w.token_account(&a.pubkey(), &mint, 50_000_000);
    w.token_account(&b.pubkey(), &mint, 50_000_000);
    w.join(room, &a, 10_000_000, Some(mint)).unwrap();
    w.join(room, &b, 10_000_000, Some(mint)).unwrap();
    // Someone drops 7 tokens straight into the vault.
    let vault = get_associated_token_address(&room, &mint);
    let gift = spl_token::instruction::transfer_checked(
        &spl_token::ID,
        &get_associated_token_address(&a.pubkey(), &mint),
        &mint,
        &vault,
        &a.pubkey(),
        &[],
        7,
        6,
    )
    .unwrap();
    w.send(&[gift], &[&a]).unwrap();

    w.lock(room, &referee).unwrap();
    w.settle(room, &referee, vec![0, 1]).unwrap();
    w.claim(room, &referee, &a.pubkey(), Some(mint)).unwrap();
    w.claim(room, &referee, &b.pubkey(), Some(mint)).unwrap();
    w.collect(room, &referee, Some(mint)).unwrap();
    w.close(room, &referee, Some(mint)).unwrap();
    assert_eq!(w.tokens(&treasury_ata), 2_000_000 + 7);
}

#[test]
fn rematch_rounds_are_separate_rooms() {
    let mut w = World::new();
    let referee = w.referee();
    let one = w.create_room(1, Params::default(), None);
    let two = w.create_room(2, Params::default(), None);
    assert_ne!(one, two);
    let a = w.player();
    w.join(one, &a, SOL / 10, None).unwrap();
    w.join(two, &a, SOL / 10, None).unwrap();
    w.cancel(one, &referee).unwrap();
    assert_eq!(w.room(&two).state, RoomState::Open);
}
