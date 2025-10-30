//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace Kreveta.movegen;

internal static class PextLookupTables {
    internal static ulong[]          FlatBishopTable    = null!;
    internal static ulong[]          FlatRookTable      = null!;
    internal static readonly ulong[] BishopMask         = new ulong[64];
    internal static readonly ulong[] RookMask           = new ulong[64];
    internal static readonly int[]   BishopOffset       = new int[64];
    internal static readonly int[]   RookOffset         = new int[64];

    private const int    ExpectedTableLayoutVersion = 2;
    private const string TableFileName              = "slider_tables.bin";

    internal static void Init() {
        if (TryLoadEmbeddedTables())
            return;

        // If the embedded resource is missing, throw — we won't create files at runtime.
        throw new InvalidOperationException("slider_tables.bin not found. Please check the resources.");
    }
    
    private static bool TryLoadEmbeddedTables() {
        var asm = Assembly.GetExecutingAssembly();
        using Stream? stream = asm.GetManifestResourceStream(TableFileName);
        
        if (stream is null) 
            return false;

        // decompress and read
        using var brotli = new BrotliStream(stream, CompressionMode.Decompress);
        using var reader = new BinaryReader(brotli);

        // header & version
        var magic = reader.ReadBytes(4);
        
        // Kreveta Runtime Tables - magic
        if (magic is not [(byte)'K', (byte)'R', (byte)'T', _]) 
            throw new InvalidDataException("Bad slider table resource magic.");

        int version = reader.ReadInt32();
        if (version != ExpectedTableLayoutVersion) 
            throw new InvalidDataException($"Slider table versions mismatch. (resource={version}, expected={ExpectedTableLayoutVersion})");

        // read masks
        for (int i = 0; i < 64; i++) BishopMask[i] = reader.ReadUInt64();
        for (int i = 0; i < 64; i++) RookMask[i]   = reader.ReadUInt64();

        // read offsets
        for (int i = 0; i < 64; i++) BishopOffset[i] = reader.ReadInt32();
        for (int i = 0; i < 64; i++) RookOffset[i]   = reader.ReadInt32();

        // read flat bishop table
        int bishopLength = reader.ReadInt32();
        FlatBishopTable = new ulong[bishopLength];
        for (int i = 0; i < bishopLength; i++) FlatBishopTable[i] = reader.ReadUInt64();

        // read flat rook table
        int rookLength = reader.ReadInt32();
        FlatRookTable = new ulong[rookLength];
        for (int i = 0; i < rookLength; i++) FlatRookTable[i] = reader.ReadUInt64();

        return true;
    }
}