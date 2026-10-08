//! Fair shares: how a finished room's stakes are divided.
//!
//! Players may stake different amounts, so the pot is cut into layers, the way poker splits side pots. Each layer
//! runs from one stake level to the next and holds that slice of every stake that reaches it. A layer is contested
//! only by the players who paid into it, so nobody can win more from a rival than they risked themselves:
//!
//! * a layer with one contributor is the unmatched top of the biggest stake and goes straight back, uncut;
//! * otherwise the house takes `cut_bps` of the layer and its contributors share the rest by finishing order:
//!   two contributors, the better one takes it all; three or more, 50 / 30 / 20 to the best three of them.
//!
//! With equal stakes there is one layer and this is the plain "pool split 1st / 2nd / 3rd". Integer division
//! leaves at most a few base units per layer; they go to the best-placed contributor of that layer, so the payouts
//! and the house always add up to exactly the stakes.
//!
//! The Worker carries a TypeScript copy (`worker/src/fairshares.ts`) for previews; both are checked against
//! `escrow/vectors/fair-shares.json`.

/// Seats in a room. Every track has six grid slots, and the phone is measured racing six karts.
pub const MAX_SEATS: usize = 6;

const BPS: u128 = 10_000;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SplitError {
    /// Fewer than two or more than `MAX_SEATS` stakes, or a ranking of another length.
    Seats,
    /// The ranking is not a permutation of the seats.
    Ranking,
    /// A cut above 100%.
    Cut,
    /// The stakes do not fit in a u64 when added up.
    Overflow,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Split {
    /// What each seat receives, in seat order; seats past the room's size are zero.
    pub payouts: [u64; MAX_SEATS],
    /// The house's cut, summed over the contested layers.
    pub house: u64,
}

/// Percent of a contested layer's net that each place among its contributors takes, best first.
pub fn places(contributors: usize) -> &'static [u128] {
    match contributors {
        0 | 1 => &[],
        2 => &[100],
        _ => &[50, 30, 20],
    }
}

/// Divides `stakes` (one per seat) by `rank` (each seat's finishing place, 0 = the winner; a permutation).
pub fn split(stakes: &[u64], rank: &[u8], cut_bps: u16) -> Result<Split, SplitError> {
    let n = stakes.len();
    if !(2..=MAX_SEATS).contains(&n) || rank.len() != n {
        return Err(SplitError::Seats);
    }
    if cut_bps as u128 > BPS {
        return Err(SplitError::Cut);
    }
    stakes.iter().try_fold(0u64, |sum, &s| sum.checked_add(s)).ok_or(SplitError::Overflow)?;

    // Seats from the winner down.
    let mut order = [usize::MAX; MAX_SEATS];
    for (seat, &place) in rank.iter().enumerate() {
        let place = place as usize;
        if place >= n || order[place] != usize::MAX {
            return Err(SplitError::Ranking);
        }
        order[place] = seat;
    }

    let mut payouts = [0u128; MAX_SEATS];
    let mut house = 0u128;
    let mut floor = 0u64;
    // One layer per distinct stake level, lowest first.
    while let Some(level) = stakes.iter().copied().filter(|&s| s > floor).min() {
        let mut layer = [0usize; MAX_SEATS];
        let mut contributors = 0;
        for &seat in &order[..n] {
            if stakes[seat] >= level {
                layer[contributors] = seat;
                contributors += 1;
            }
        }
        let pot = (level - floor) as u128 * contributors as u128;
        if contributors == 1 {
            payouts[layer[0]] += pot;
        } else {
            let cut = pot * cut_bps as u128 / BPS;
            let net = pot - cut;
            house += cut;
            let mut given = 0;
            for (k, &percent) in places(contributors).iter().enumerate() {
                let share = net * percent / 100;
                payouts[layer[k]] += share;
                given += share;
            }
            payouts[layer[0]] += net - given;
        }
        floor = level;
    }

    // Every part is a share of the stakes' sum, which fits in a u64.
    let mut out = [0u64; MAX_SEATS];
    for (o, p) in out.iter_mut().zip(payouts) {
        *o = p as u64;
    }
    Ok(Split { payouts: out, house: house as u64 })
}

#[cfg(test)]
mod tests {
    use super::*;

    fn total(s: &Split) -> u128 {
        s.payouts.iter().map(|&p| p as u128).sum::<u128>() + s.house as u128
    }

    #[test]
    fn shared_vectors() {
        let raw: serde_json::Value =
            serde_json::from_str(include_str!("../../../vectors/fair-shares.json")).unwrap();
        let num = |v: &serde_json::Value| v.as_str().unwrap().parse::<u64>().unwrap();
        for case in raw.as_array().unwrap() {
            let name = case["name"].as_str().unwrap();
            let stakes: Vec<u64> = case["stakes"].as_array().unwrap().iter().map(num).collect();
            let rank: Vec<u8> = case["rank"].as_array().unwrap().iter().map(|v| v.as_u64().unwrap() as u8).collect();
            let cut = case["cutBps"].as_u64().unwrap() as u16;
            let got = split(&stakes, &rank, cut);
            if let Some(err) = case.get("error") {
                let want = match err.as_str().unwrap() {
                    "seats" => SplitError::Seats,
                    "ranking" => SplitError::Ranking,
                    "cut" => SplitError::Cut,
                    "overflow" => SplitError::Overflow,
                    other => panic!("{name}: unknown error {other}"),
                };
                assert_eq!(got, Err(want), "{name}");
                continue;
            }
            let got = got.unwrap_or_else(|e| panic!("{name}: {e:?}"));
            let want: Vec<u64> = case["payouts"].as_array().unwrap().iter().map(num).collect();
            assert_eq!(&got.payouts[..stakes.len()], &want[..], "{name}: payouts");
            assert_eq!(got.house, num(&case["house"]), "{name}: house");
            assert_eq!(total(&got), stakes.iter().map(|&s| s as u128).sum::<u128>(), "{name}: conservation");
        }
    }

    /// A small deterministic generator: the property test needs variety, not a dependency.
    struct XorShift(u64);
    impl XorShift {
        fn next(&mut self) -> u64 {
            self.0 ^= self.0 << 13;
            self.0 ^= self.0 >> 7;
            self.0 ^= self.0 << 17;
            self.0
        }
    }

    #[test]
    fn properties_hold_for_random_rooms() {
        let mut rng = XorShift(0x9E37_79B9_7F4A_7C15);
        for _ in 0..20_000 {
            let n = 2 + (rng.next() % 5) as usize;
            let stakes: Vec<u64> = (0..n)
                .map(|_| match rng.next() % 3 {
                    0 => 10_000_000,                              // shared tiers collide often
                    1 => 1 + rng.next() % 1_000_000_000,
                    _ => [10_000_000u64, 50_000_000, 100_000_000, 500_000_000][(rng.next() % 4) as usize],
                })
                .collect();
            let mut rank: Vec<u8> = (0..n as u8).collect();
            for i in (1..n).rev() {
                rank.swap(i, (rng.next() % (i as u64 + 1)) as usize);
            }
            let cut = (rng.next() % 2_001) as u16;
            let s = split(&stakes, &rank, cut).unwrap();
            let sum: u128 = stakes.iter().map(|&x| x as u128).sum();
            assert_eq!(total(&s), sum, "conservation");
            assert!(s.payouts[n..].iter().all(|&p| p == 0), "no payout past the seats");
            // Winning never loses money while the cut stays at or under a third: the winner takes at least half of
            // every contested layer it paid into, and every contested layer holds at least twice its share.
            let winner = rank.iter().position(|&r| r == 0).unwrap();
            assert!(s.payouts[winner] >= stakes[winner], "the winner gets at least its stake back");
            // Nobody wins more from a rival than it risked: a seat's payout never exceeds its own stake times the
            // number of seats.
            for seat in 0..n {
                assert!(s.payouts[seat] as u128 <= stakes[seat] as u128 * n as u128, "capped by own stake");
            }
            // The house only cuts contested money.
            assert!(s.house as u128 * BPS <= sum * cut as u128, "the cut never exceeds its rate");
        }
    }

    #[test]
    fn equal_stakes_are_one_pool() {
        let s = split(&[100, 100, 100, 100], &[2, 0, 3, 1], 1_000).unwrap();
        assert_eq!(s.house, 40);
        assert_eq!(&s.payouts[..4], &[72, 180, 0, 108]);
    }
}
