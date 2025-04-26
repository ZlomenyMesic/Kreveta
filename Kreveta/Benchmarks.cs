//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// Remove unnecessary suppression
#pragma warning disable IDE0079

// Mark members as static
#pragma warning disable CA1822

using BenchmarkDotNet.Attributes;

namespace Kreveta;

[MemoryDiagnoser]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class Benchmarks {

    [GlobalSetup]
    public void Setup() {
    }

    static int K() => 10;

    [Benchmark]
    public void INT() {
        int x = K();

        x += 128;
        x /= 2;
        x -= K() * 4;
        x *= 12 - K();
    }

    [Benchmark]
    public void BYTE() {
        byte x = (byte)K();

        x += 128;
        x /= 2;
        x -= (byte)(K() * 4);
        x *= (byte)(12 - K());
    }
}

#pragma warning restore CA1822
#pragma warning restore IDE0079