//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.uci;

using System;
using System.IO;
using System.Reflection;
// ReSharper disable InconsistentNaming

// Non-nullable field must contain a non-null value when exiting constructor.
#pragma warning disable CS8618

namespace Kreveta.nnue;

internal static class NNUEWeights {

    // integer NNUE weights
    internal static short[] Embedding;
    
    private static short[] S1_H1Kernel;
    private static short[] S1_H1Bias;
    private static short[] S1_H2Kernel;
    private static short[] S1_H2Bias;

    private static short[] S2_H1Kernel;
    private static short[] S2_H1Bias;
    private static short[] S2_H2Kernel;
    private static short[] S2_H2Bias;

    private static short[] S3_H1Kernel;
    private static short[] S3_H1Bias;
    private static short[] S3_H2Kernel;
    private static short[] S3_H2Bias;

    private static short[] S4_H1Kernel;
    private static short[] S4_H1Bias;
    private static short[] S4_H2Kernel;
    private static short[] S4_H2Bias;
    
    internal static short[] OutputKernel;
    internal static short   OutputBias;

    internal static short[][] H1Kernels;
    internal static short[][] H2Kernels;
    internal static short[][] H1Biases;
    internal static short[][] H2Biases;

    private  const int FeatCount = 40960;
    internal const int EmbedDims = 128;
    internal const int H1Neurons = 16;
    internal const int H2Neurons = 16;
    
    internal static void Load(string binFile) {
        var asm = Assembly.GetExecutingAssembly();
        using Stream? stream = asm.GetManifestResourceStream(binFile);

        if (stream is null) {
            UCI.Log("Embedded NNUE weights not found", UCI.LogLevel.ERROR);
            return;
        }

        // stream alone doesn't read everything when the binary weight file
        // is stored as an embedded resource. i have no clue why that is the
        // case, but using memorystream fixes it
        using var ms = new MemoryStream();
        stream.CopyTo(ms);

        byte[]  rawBytes = ms.ToArray();
        short[] all      = new short[rawBytes.Length / 2];
        
        Buffer.BlockCopy(rawBytes, 0, all, 0, rawBytes.Length);

        int offset = 0;

        // === 1. EMBEDDING ===
        const int embedLen = FeatCount * EmbedDims;
        Embedding = new short[embedLen];
        for (int i = 0; i < embedLen; i++)
            Embedding[i] = all[offset++];

        // allocate subnet buffers
        S1_H1Kernel = new short[H1Neurons * EmbedDims * 2];
        S1_H1Bias   = new short[H1Neurons];
        S1_H2Kernel = new short[H2Neurons * H1Neurons];
        S1_H2Bias   = new short[H2Neurons];

        S2_H1Kernel = new short[S1_H1Kernel.Length];
        S2_H1Bias   = new short[S1_H1Bias.Length];
        S2_H2Kernel = new short[S1_H2Kernel.Length];
        S2_H2Bias   = new short[S1_H2Bias.Length];

        S3_H1Kernel = new short[S1_H1Kernel.Length];
        S3_H1Bias   = new short[S1_H1Bias.Length];
        S3_H2Kernel = new short[S1_H2Kernel.Length];
        S3_H2Bias   = new short[S1_H2Bias.Length];

        S4_H1Kernel = new short[S1_H1Kernel.Length];
        S4_H1Bias   = new short[S1_H1Bias.Length];
        S4_H2Kernel = new short[S1_H2Kernel.Length];
        S4_H2Bias   = new short[S1_H2Bias.Length];

        short[][] H1K = [ S1_H1Kernel, S2_H1Kernel, S3_H1Kernel, S4_H1Kernel ];
        short[][]   H1B = [ S1_H1Bias,   S2_H1Bias,   S3_H1Bias,   S4_H1Bias ];
        short[][] H2K = [ S1_H2Kernel, S2_H2Kernel, S3_H2Kernel, S4_H2Kernel ]; 
        short[][]   H2B = [ S1_H2Bias,   S2_H2Bias,   S3_H2Bias,   S4_H2Bias ];

        // === 2. LOAD SUBNETS IN REAL KERAS ORDER ===
        for (int subnet = 0; subnet < 4; subnet++) {
            LoadH1Kernel(all, H1K[subnet], ref offset);
            LoadH1Bias(all, H1B[subnet], ref offset);
        }

        for (int subnet = 0; subnet < 4; subnet++) {
            LoadH2Kernel(all, H2K[subnet], ref offset);
            LoadH2Bias(all, H2B[subnet], ref offset);
        }

        H1Kernels = H1K;
        H2Kernels = H2K;
        H1Biases  = H1B;
        H2Biases  = H2B;

        // === 3. OUTPUT LAYER ===
        OutputKernel = new short[H2Neurons];
        for (int i = 0; i < H2Neurons; i++)
            OutputKernel[i] = all[offset++];

        OutputBias = all[offset];
    }
    
    private static void LoadH1Kernel(short[] all, short[] dest, ref int offset) {
        const int rows = EmbedDims * 2;

        for (int r = 0; r < rows; r++) {
            int baseIdx = offset + r * H1Neurons;
            for (int c = 0; c < H1Neurons; c++) {
                int dst = c * rows + r;
                dest[dst] = all[baseIdx + c];
            }
        }

        offset += rows * H1Neurons;
    }

    private static void LoadH1Bias(short[] all, short[] dest, ref int offset) {
        for (int i = 0; i < dest.Length; i++)
            dest[i] = all[offset++];
    }

    private static void LoadH2Kernel(short[] all, short[] dest, ref int offset) {
        for (int r = 0; r < H1Neurons; r++) {
            int baseIdx = offset + r * H2Neurons;
            for (int c = 0; c < H2Neurons; c++) {
                int dst = c * H1Neurons + r;
                dest[dst] = all[baseIdx + c];
            }
        }

        offset += H1Neurons * H2Neurons;
    }

    private static void LoadH2Bias(short[] all, short[] dest, ref int offset) {
        for (int i = 0; i < dest.Length; i++)
            dest[i] = all[offset++];
    }
}