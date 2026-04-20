//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

#pragma warning disable CA1810

using Kreveta.uci;

using System;
using System.IO;
using System.Reflection;

// ReSharper disable InconsistentNaming

#pragma warning disable CS8618

namespace Kreveta.nnue;

internal static class NNUEWeights {
    internal static short[]   Embedding;
    internal static short[][] H1Kernels;
    internal static short[][] H2Kernels;
    internal static short[][] H1Biases;
    internal static short[][] H2Biases;
    internal static short[][] OutputKernels;
    internal static short[]   OutputBiases;

    private  const int FeatCount = 40960;
    internal const int EmbedDims = 128;
    internal const int H1Neurons = 16;
    internal const int H2Neurons = 16;

    internal static void Load() {
        var asm = Assembly.GetExecutingAssembly();
        using Stream? stream = asm.GetManifestResourceStream($"{Program.Name}.{Program.Network}");

        if (stream is null) {
            UCI.Log("Embedded NNUE weights not found");
            return;
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);

        byte[]  rawBytes = ms.ToArray();
        short[] all      = new short[rawBytes.Length / 2];

        Buffer.BlockCopy(rawBytes, 0, all, 0, rawBytes.Length);

        int offset = 0;

        // embeddings
        const int embedLen = FeatCount * EmbedDims;
        Embedding = new short[embedLen];
        for (int i = 0; i < embedLen; i++)
            Embedding[i] = all[offset++];

        // allocate subnet buffers
        const int subnetCount = 8;

        short[][] H1K = new short[subnetCount][];
        short[][] H1B = new short[subnetCount][];
        short[][] H2K = new short[subnetCount][];
        short[][] H2B = new short[subnetCount][];
        short[][] OK  = new short[subnetCount][];

        const int h1KernelSize = H1Neurons * EmbedDims * 2;
        const int h2KernelSize = H2Neurons * H1Neurons;

        for (int s = 0; s < subnetCount; s++) {
            H1K[s] = new short[h1KernelSize];
            H1B[s] = new short[H1Neurons];
            H2K[s] = new short[h2KernelSize];
            H2B[s] = new short[H2Neurons];
            OK[s]  = new short[H2Neurons];
        }

        // load subnets
        for (int subnet = 0; subnet < subnetCount; subnet++) {
            LoadH1Kernel(all, H1K[subnet], ref offset);
            LoadH1Bias  (all, H1B[subnet], ref offset);
        }

        for (int subnet = 0; subnet < subnetCount; subnet++) {
            LoadH2Kernel(all, H2K[subnet], ref offset);
            LoadH2Bias  (all, H2B[subnet], ref offset);
        }

        short[] OB = new short[subnetCount];

        // load outputs per subnet
        for (int subnet = 0; subnet < subnetCount; subnet++) {
            for (int i = 0; i < H2Neurons; i++)
                OK[subnet][i] = all[offset++];

            OB[subnet] = all[offset++];
        }

        H1Kernels = H1K;
        H2Kernels = H2K;
        H1Biases  = H1B;
        H2Biases  = H2B;

        OutputKernels = OK;
        OutputBiases  = OB;
        
        UCI.Log($"Using NNUE file: {Program.Network}");
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

#pragma warning restore CA1810