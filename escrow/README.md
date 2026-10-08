# Kartel prize escrow

An Anchor program that holds the stakes of Kartel's paid multiplayer rooms and pays them out. The game server never
holds a player's money: it can open a room, lock it when the race starts and report who finished where. **What that
order pays is decided by the program alone**, and money can only leave a room to the wallet that staked it, to a
winner by the rules below, or as the house cut to the treasury fixed when the room was created.

Program id: `KartpVhCHvs2xSBe59LrPYUJ8qUu1PT4DegfFa1nkhv` (the keypair is kept outside the repo).

## How a paid room works

1. A host creates a room in the app with a currency (SOL or SKR) and a minimum stake. The Worker's referee key
   creates the room's account on-chain (`create_room`). The referee pays the rent and gets it back when the room is
   closed.
2. Each player picks their own stake, from the room's minimum up to its maximum, and signs `join` in their wallet.
   Stakes are whole numbers of the room's unit (0.01 SOL or 5 SKR). The stake goes into the room's account: lamports
   for SOL, or for SKR the room's associated token account (the vault), taken from the player's own associated token
   account. A room seats at most 6 players. Every track has 6 grid slots, and the phone is measured racing 6 karts.
3. Until the race starts, a player can take the stake back (`leave`). The referee also hands stakes back for seats
   that left the lobby, were removed by the host, or never reached the grid (`remove_entry`). A refund can only go to
   the wallet that staked.
4. When the race starts, the referee locks the room (`lock`) with the players who race. Any other stake (one that
   arrived on the way, or a seat that never reached the grid) is left out of the race and can be claimed back in full
   straight away, so nobody can hold the lock up by staking. Once locked, no stake can enter or leave.
5. When the race (or the 1–4 race cup) ends, the referee reports the finishing order of every staked wallet
   (`settle`). The program works out each payout. If there was no contest, the referee cancels instead (`cancel`) and
   every stake becomes claimable in full.
6. Every payout is delivered by `claim`, once per seat, always to the entry's own wallet. The cut goes to the treasury
   by `collect_cut`. Then the room's accounts are closed (`close_room`). All three are permissionless: the Worker
   sends them straight away, and anyone else can too.

**If the game server disappears:** after a room's `expires_at` (3 hours for Kartel's rooms), anyone can cancel it
and every stake can be claimed back. A player in an open room can always `leave`.

## Fair shares (the payout rule)

Players can stake different amounts, so the pot is cut into layers, the way poker splits side pots. Nobody wins more
from a rival than they risked themselves.

- Each layer runs from one stake level to the next, and holds that slice of every stake that reaches it.
- A layer only one player paid into is the unmatched top of the biggest stake. It goes straight back to that player,
  with no cut.
- Every other layer gives `cut_bps` (10% in Kartel) to the house. The rest is shared by finishing order among the
  players who paid into that layer: all of it to the better one if two did, 50 / 30 / 20 to the best three if three
  or more did.
- Rounding remainders go to the best-placed player of the layer, so payouts plus the cut always equal the stakes.

With equal stakes this is the plain "pool split 1st / 2nd / 3rd". Example, with a 10% cut: A stakes 1 SOL, B and C
stake 0.1 SOL each.

| Finish | A gets | B gets | C gets | House |
|---|---|---|---|---|
| A, B, C | 1.035 | 0.081 | 0.054 | 0.03 |
| C, B, A | 0.954 | 0.081 | 0.135 | 0.03 |

The rule lives in `programs/kartel_escrow/src/sidepot.rs`. The Worker (`worker/src/fairshares.ts`) and the phone
(`Assets/_Game/Scripts/Net/FairShares.cs`) carry copies used only for previews ("if you win"). All three are tested
against `vectors/fair-shares.json`.

## Accounts and instructions

| Account | Seeds | Holds |
|---|---|---|
| `Config` | `["config"]` | admin, referee, treasury, the cut ceiling (≤ 20%), pause switch |
| `Room` | `["room", code, round as u32 LE]` | terms, state (Open → Locked → Settled / Cancelled), up to 6 entries `{player, stake, payout, paid}`, the cut owed; SOL stakes as its own lamports |
| vault | the room's associated token account | SKR stakes |

| Instruction | Who signs | Does |
|---|---|---|
| `initialize` | the upgrade authority | creates the config |
| `update_config` | admin | rotates the referee or treasury, changes the cut ceiling, pauses or resumes |
| `create_room` | referee | opens a room (the vault is created beside it in the same transaction) |
| `join(stake)` | player | puts a stake in |
| `leave` | player | takes the stake back while the room is open |
| `remove_entry` | referee | hands a seat's stake back while the room is open |
| `lock(players)` | referee | closes the room to stakes; any stake not in `players` is left out and owed back in full |
| `settle(order)` | referee | fixes the payouts from the finishing order |
| `cancel` | referee anytime, anyone after expiry | makes every stake claimable |
| `claim` | anyone | delivers one seat's payout to its wallet (a stake left out at the lock, straight away) |
| `collect_cut` | anyone | sends a settled room's cut to its treasury |
| `close_room` | anyone | closes a fully paid room; rent back to its payer, stray vault tokens to the treasury |

Pausing stops new rooms and new stakes. Leaving, cancelling, claiming and closing always work.

## Trust model and limits

- **The referee key** (a Worker secret) decides finishing orders, which can't be verified on-chain: races are
  refereed by the Worker from each kart's positions. A stolen referee key can't move anyone's stake to an address of
  its choosing. The worst it can do is settle a locked room in a wrong order among its own entrants, or cancel
  rooms, which refunds everyone. It holds only enough SOL for rent and fees. Rotate it with `update_config`.
- **The admin key** is the program's upgrade authority. Keep it offline. Upgrades can change the rules for future
  transactions, so the authority could be moved to a multisig, or the program made immutable, once it is settled.
- **No dust.** Stakes are whole units (0.01 SOL), so the smallest SOL payout is about 0.005 SOL. That is far above
  the 0.0009 SOL an empty wallet needs before it can receive anything.
- **A token payout to a closed token account** waits until the player has an account again. The Worker retries it on
  its own clock, and every other payout and the cut go through meanwhile.
- **Not audited.** It has 22 LiteSVM tests of every instruction and refusal, a property test of the payout maths over
  20,000 random rooms, an end-to-end run on a local validator (below), and one adversarial review whose findings are
  fixed (stray stakes no longer delay the lock, refunds and payouts back off one wallet at a time, no dust payouts).

## Build and test

Requires Rust, the Agave CLI (3.1+) and Anchor 1.2.

```sh
anchor build --arch v0     # v0, not Anchor 1.2's default v3: no cluster accepts sBPFv3 programs before Agave 4.4
cargo test                 # sidepot unit/property tests + the LiteSVM suite (loads target/deploy/kartel_escrow.so)
node ../worker/scripts/sync-idl.mjs   # copy the IDL to the Worker after any interface change
```

End to end on a local chain: the Worker, a local validator running this program, and racers that stake from their
own keys and are paid by the program (balances checked to the lamport):

```sh
cd ../worker
npm run escrow:localnet -- validator     # prints the solana-test-validator command; run it in its own terminal
npm run escrow:localnet                  # config, a stand-in SKR mint, and the wrangler flags
npx wrangler dev --port 8787 $(cat .escrow-local/wrangler-vars.txt)
npm run smoke:escrow
```

## Deploying to mainnet

Costs, measured 5 Oct 2026: the program's data account holds 395,509 bytes, so its rent is **2.01 SOL**. You get
that back if the program is ever closed. A first deploy also writes through a buffer of the same size, refunded at
the end of the deploy, so the deploying wallet needs about **4.1 SOL** for a moment.

1. Fund the deploy key (`~/keys/kartel-escrow-deployer.json`, the upgrade authority and admin) with ~4.2 SOL, then:
   ```sh
   solana program deploy target/deploy/kartel_escrow.so --program-id ~/keys/kartel-escrow-program.json \
     --keypair ~/keys/kartel-escrow-deployer.json --url <mainnet rpc> --with-compute-unit-price 50000 --use-rpc
   ```
2. Fund a referee key (`~/keys/kartel-referee.json`) with ~0.05 SOL for room rent and fees. Initialize the config with
   the treasury that receives the cut:
   ```sh
   cd ../worker && npm run escrow:admin -- init --url <mainnet rpc> --admin ~/keys/kartel-escrow-deployer.json \
     --referee <referee pubkey> --treasury <treasury owner>
   ```
3. Point the Worker at it: set `ESCROW_PROGRAM` in `wrangler.toml`, run `wrangler secret put REFEREE_SECRET` (the
   referee's secret key, base58), then `wrangler deploy`. `GET /v1/config` then shows `escrowProgram` and
   `paidRooms: true` (outside the blocked jurisdictions). `TREASURY_SECRET` is no longer used by anything that pays
   out and can be deleted.
