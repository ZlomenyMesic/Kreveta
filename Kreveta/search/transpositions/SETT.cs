//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming

namespace Kreveta.search.transpositions;

// Static Evaluation Transposition Table (SETT) is a data structure kept separate
// from regular TT. it has a constant size, and is used purely for storing static
// evaluations for different hashes. although static eval is not nearly as much
// expensive as regular search, there still are performance benefits on top of TT.
internal static unsafe class SETT {
    
    // a small 8-byte entry consists of hash and the static eval
    [StructLayout(LayoutKind.Explicit, Size = EntrySize)]
    private struct Entry {
        // since we need 48 bits of hash, two numbers are used
        [field: FieldOffset(0)] internal uint   Hash_A;
        [field: FieldOffset(4)] internal ushort Hash_B;

        [field: FieldOffset(6)] internal short  Eval;
    }
    
    private const int EntrySize  = 8;       // 8 bytes per entry
    private const int EntryCount = 524_288; // exactly 4 MiB of memory

    private static Entry* Table = null;

    internal static void Clear() {
        if (Table != null)
            NativeMemory.AlignedFree(Table);

        Table = null;
    }
    
    internal static void Realloc() {
        Clear();
        Table = (Entry*)NativeMemory.AlignedAlloc(EntryCount * EntrySize, 64);
        NativeMemory.Clear(Table, EntryCount * EntrySize);
    }

    // store a new static evaluation
    internal static void Store(ulong hash, short eval) {
        int index = (int)(hash & EntryCount - 1);
        var entry = Table + index;

        // the previous entry is always overwritten no matter what
        entry->Hash_A = (uint)  (hash       & 0x00000000FFFFFFFF);
        entry->Hash_B = (ushort)(hash >> 32 & 0x000000000000FFFF);
        entry->Eval   = eval;
    }

    // try to find a stored static evaluation for a position. the eval
    // is always set, but we must rely on the returned flag being okay
    internal static bool TryGetEval(ulong hash, out short eval) {
        int index = (int)(hash & EntryCount - 1);
        var entry = Table + index;
        
        eval = entry->Eval;
        
        // ensure the hashes actually match
        return entry->Hash_A == (hash       & 0x00000000FFFFFFFF)
            && entry->Hash_B == (hash >> 32 & 0x000000000000FFFF);
    }
}