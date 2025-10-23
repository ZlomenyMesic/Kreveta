using Kreveta.consts;
using Kreveta.movegen;

using System;
using System.IO;
using System.Linq;

namespace Kreveta.openings;

internal static class Polyglot {
    private record struct PolyglotEntry {
        internal ulong  Key;
        internal ushort Move;
        internal ushort Weight;
    }

    private const int EntrySize = 16;
    
    static string path = @"C:\Users\michn\Downloads\Titans\Titans.bin";
    
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

        return new Move(start, end, piece, capt, (PType)prom);
    }

    private static PolyglotEntry SelectMove(PolyglotEntry[] possibleMoves) {
        return possibleMoves.OrderByDescending(m => m.Weight).First();
    }
    
    private static PolyglotEntry[] GetMovesForPosition(ulong hash, PolyglotEntry[] book)
        => book.Where(entry => entry.Key == hash).ToArray();

    private static PolyglotEntry[] LoadBook() {
        byte[] data    = File.ReadAllBytes(path);
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