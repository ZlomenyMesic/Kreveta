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

using BenchmarkDotNet.Attributes;
using System.Diagnostics;

namespace Kreveta;

#if DEBUG

[MemoryDiagnoser]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class Benchmarks {

    [Conditional("DEBUG")]
    [GlobalSetup]
    public void Setup() {

    }
}

#endif

#pragma warning restore CA1822
#pragma warning restore CA1515

#pragma warning restore IDE0079