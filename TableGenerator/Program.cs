//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.IO.Compression;

namespace TableGenerator;

internal static class Program {
    
    // update when table layout changes - it probably won't
    private const int    TableLayoutVersion = 2;
    private const string TableFileName      = "slider_tables.bin";
    
    internal static void Main() {
        Console.WriteLine("Please gimme a sec mate...");
        LookupTables.Init();

        // compose output path
        string outputFile = Path.GetFullPath(TableFileName);
        using (FileStream fs = File.Create(outputFile))
            
        using (var brotli = new BrotliStream(fs, CompressionLevel.Optimal))
        using (var writer = new BinaryWriter(brotli)) {
            
            // header - magic (Kreveta Runtime Tables) and version
            writer.Write("KRT\0"u8.ToArray());
            writer.Write(TableLayoutVersion);

            // write small meta - masks, offsets, bits
            for (int i = 0; i < 64; i++) writer.Write(LookupTables.BishopMask[i]);
            for (int i = 0; i < 64; i++) writer.Write(LookupTables.RookMask[i]);

            for (int i = 0; i < 64; i++) writer.Write(LookupTables.BishopOffset[i]);
            for (int i = 0; i < 64; i++) writer.Write(LookupTables.RookOffset[i]);

            // then write flat tables - lengths then raw data
            writer.Write(LookupTables.FlatBishopTable.Length);
            foreach (ulong v in LookupTables.FlatBishopTable) 
                writer.Write(v);

            writer.Write(LookupTables.FlatRookTable.Length);
            foreach (ulong v in LookupTables.FlatRookTable) 
                writer.Write(v);
        }

        Console.WriteLine($"Slider tables have been compressed and stored to: {outputFile}");
    }
}