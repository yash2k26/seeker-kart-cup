use anchor_lang::prelude::*;

#[error_code]
pub enum EscrowError {
    #[msg("Only the program's upgrade authority may initialize it")]
    NotUpgradeAuthority,
    #[msg("Only the admin may change the config")]
    NotAdmin,
    #[msg("Only the referee may do this")]
    NotReferee,
    #[msg("New rooms and stakes are paused")]
    Paused,
    #[msg("Room codes are five characters A-Z / 2-9 without O or I")]
    BadCode,
    #[msg("A room seats 2 to 6 players")]
    BadMaxPlayers,
    #[msg("Stakes must be above zero, with the minimum at or under the maximum")]
    BadStakeRange,
    #[msg("The cut is above the allowed maximum")]
    CutTooHigh,
    #[msg("A room lives between 10 minutes and 24 hours")]
    BadTtl,
    #[msg("The room is not open")]
    NotOpen,
    #[msg("The room is not locked")]
    NotLocked,
    #[msg("The room is not finished")]
    NotFinished,
    #[msg("The room is already settled or cancelled")]
    AlreadyFinished,
    #[msg("The room has expired")]
    Expired,
    #[msg("Every seat is taken")]
    RoomFull,
    #[msg("This wallet already has a stake in the room")]
    AlreadyJoined,
    #[msg("The stake is outside the room's range or not a whole number of its units")]
    StakeOutOfRange,
    #[msg("This wallet has no stake in the room")]
    NoEntry,
    #[msg("A race needs at least two players")]
    TooFewPlayers,
    #[msg("The finishing order must name every staked wallet exactly once")]
    BadRanking,
    #[msg("The field must name staked wallets, each once")]
    BadField,
    #[msg("Only the referee may cancel a room before it expires")]
    CannotCancelYet,
    #[msg("Already paid out")]
    AlreadyPaid,
    #[msg("There is no cut to collect")]
    NothingToCollect,
    #[msg("Payouts are still owed")]
    PayoutsOwed,
    #[msg("These token accounts do not match the room's currency")]
    WrongCurrencyAccounts,
    #[msg("That account is not the one owed")]
    WrongRecipient,
    #[msg("Arithmetic overflow")]
    Overflow,
}
