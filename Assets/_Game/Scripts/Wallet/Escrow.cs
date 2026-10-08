using System;
using System.Collections.Generic;
using System.Text;
using Solana.Unity.Programs;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Wallet;

namespace KartCup
{
    /// The prize escrow program (escrow/programs/kartel_escrow) from the phone's side: the one instruction a player
    /// signs, `join`, and the addresses it needs. The layout follows the program's IDL (worker/src/kartel_escrow.json);
    /// EscrowCheck sends this exact instruction to a local chain running the program.
    public static class Escrow
    {
        static readonly byte[] JoinDiscriminator = { 206, 55, 2, 106, 113, 220, 17, 163 };

        public static PublicKey ConfigAddress(PublicKey program)
        {
            PublicKey.TryFindProgramAddress(new[] { Encoding.ASCII.GetBytes("config") }, program, out var address, out _);
            return address;
        }

        /// One account per room and round: seeds "room", the five-letter code, the round as a little-endian u32.
        public static PublicKey RoomAddress(PublicKey program, string code, int round)
        {
            var seeds = new[] { Encoding.ASCII.GetBytes("room"), Encoding.ASCII.GetBytes(code), BitConverter.GetBytes((uint)round) };
            if (!BitConverter.IsLittleEndian) Array.Reverse(seeds[2]);
            PublicKey.TryFindProgramAddress(seeds, program, out var address, out _);
            return address;
        }

        /// The player's stake into the room. A token room adds the mint, the room's vault, the player's token account
        /// and the token program; a SOL room leaves those four out, which Anchor reads from the program's own id in
        /// their place.
        public static TransactionInstruction Join(PublicKey program, PublicKey room, PublicKey player, PublicKey mint, ulong stake)
        {
            AccountMeta None() => AccountMeta.ReadOnly(program, false);
            var keys = new List<AccountMeta>
            {
                AccountMeta.Writable(player, true),
                AccountMeta.ReadOnly(ConfigAddress(program), false),
                AccountMeta.Writable(room, false),
                mint == null ? None() : AccountMeta.ReadOnly(mint, false),
                mint == null ? None() : AccountMeta.Writable(AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(room, mint), false),
                mint == null ? None() : AccountMeta.Writable(AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(player, mint), false),
                mint == null ? None() : AccountMeta.ReadOnly(TokenProgram.ProgramIdKey, false),
                AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),
            };
            var data = new byte[16];
            JoinDiscriminator.CopyTo(data, 0);
            for (int i = 0; i < 8; i++) data[8 + i] = (byte)(stake >> (8 * i));
            return new TransactionInstruction { ProgramId = program.KeyBytes, Keys = keys, Data = data };
        }
    }
}
