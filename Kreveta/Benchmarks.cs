//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// Remove unnecessary suppression
#pragma warning disable IDE0079

// Consider making public types internal
#pragma warning disable CA1515

// Mark members as static
#pragma warning disable CA1822

using System;
using System.Numerics;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Kreveta;

//#if DEBUG

[MemoryDiagnoser]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class Benchmarks
{
    internal int x = -1;
    internal int c = 0;

    //[Conditional("DEBUG")]
    [GlobalSetup]
    public void Setup() {

    }
    
    [Benchmark]
    public void SimpleEqual()
    {
        if (x == -1)
            c = 10;
    }

    [Benchmark]
    public void BitShiftEqual()
    {
        if (x >>> 31 == 1)
            c = 10;
    }
}

//#endif

#pragma warning restore CA1822
#pragma warning restore CA1515

#pragma warning restore IDE0079