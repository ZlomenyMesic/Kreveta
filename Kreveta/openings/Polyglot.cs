#pragma warning disable CA5394

using Kreveta.consts;
using Kreveta.movegen;

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Kreveta.openings;

internal static class Polyglot {
    
    [StructLayout(LayoutKind.Explicit, Size = EntrySize)]
    private record struct PolyglotEntry {
        [FieldOffset(0)]  internal ulong  Key;
        [FieldOffset(8)]  internal ushort Move;
        [FieldOffset(10)] internal float  Weight;
    }

    private const int EntrySize = 16;

    // the risk percentage is inverted
    private static float Risk => (float)Options.PolyglotRisk / 100;
    
    internal static Move GetBookMove(Board board) {
        ulong hash = PolyglotZobristHash.Hash(board);
        var book = LoadBook();

        var possible = GetMovesForPosition(hash, book);

        // no possible moves exist
        if (possible.Length == 0)
            return default;
        
        PolyglotEntry selection = SelectMove(possible);
            
        int end   = selection.Move       & 0x3F;
        int start = selection.Move >> 6  & 0x3F;
        int prom  = selection.Move >> 12 & 0xF;
        
        start = (start & 7) + 8 * (7 - (start >> 3));
        end   = (end   & 7) + 8 * (7 - (end   >> 3));
            
        PType piece = board.PieceAt(start);
        PType capt  = board.PieceAt(end);

        UCI.Log($"info string selected move's Polyglot weight: {selection.Weight}", UCI.LogLevel.INFO);
        return new Move(start, end, piece, capt, (PType)prom);
    }

    private static PolyglotEntry SelectMove(PolyglotEntry[] possibleMoves) {
        var normalized = NormalizeWeights(possibleMoves, out int max).Where(i => i.Weight >= 1 - Risk)
            .OrderByDescending(i => i.Weight).ToArray();
        
        float sum        = normalized.Select(i => i.Weight).Sum();
        float random     = new Random().NextSingle() * sum;
        float sumCounter = 0f;

        for (int i = 0; i < normalized.Length; i++) {
            sumCounter += normalized[i].Weight;
            
            if (sumCounter > random)
                return normalized[i] 
                    // un-normalize the selected move's weight
                    with {Weight = max * normalized[i].Weight};
        }
        
        return default;
    }
    
    private static PolyglotEntry[] NormalizeWeights(PolyglotEntry[] possible, out int max) {
        float fmax = possible.Select(entry => entry.Weight).Prepend(0).Max();
        max = (int)fmax;

        return possible.Select(i => i 
            with {Weight = i.Weight / fmax}).ToArray();
    }
    
    private static PolyglotEntry[] GetMovesForPosition(ulong hash, PolyglotEntry[] book)
        => book.Where(entry => entry.Key == hash).ToArray();

    private static PolyglotEntry[] LoadBook() {
        
        if (!File.Exists(Options.PolyglotBook)) {
            UCI.Log($"Polyglot file not found: {Options.PolyglotBook}", UCI.LogLevel.ERROR);
            return [];
        }
        
        byte[] data    = File.ReadAllBytes(Options.PolyglotBook);
        int entryCount = data.Length / EntrySize;
        var entries    = new PolyglotEntry[entryCount];

        for (int i = 0; i < entryCount; i++) {
            int offset = i * EntrySize;
            
            entries[i].Key    = BitConverter.ToUInt64(data.Skip(offset).Take(8).Reverse().ToArray(), 0);
            entries[i].Move   = BitConverter.ToUInt16(data.Skip(offset + 8).Take(2).Reverse().ToArray(), 0);
            entries[i].Weight = BitConverter.ToUInt16(data.Skip(offset + 10).Take(2).Reverse().ToArray(), 0);
        }

        return entries;
    }
}

#pragma warning restore CA5394