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

using Kreveta.consts;
using Kreveta.evaluation;
using Kreveta.movegen;

namespace Kreveta;

/*
 * FASTEST PERFT 6:
 * 1.479 s
 */

/*
 * CURRENT BENCHMARKS:
 * Board.PlayMove             24.16 ns
 * Board.PlayReversibleMove   21.26 ns
 * 
 * Eval.StaticEval            97.13 ns
 * 
 * ZobristHash.GetHash        23.61 ns
 *
 * Movegen.GetLegalMoves      383.5 ns
 */

[MemoryDiagnoser]
[RankColumn, Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class Benchmarks {
    private Board _position;
    private Move  _move;
    
    [GlobalSetup]
    public void Setup() {
        _position = Board.CreateStartpos();
        _move     = new Move(53, 37, PType.PAWN, PType.NONE, PType.NONE);
    }

    [Benchmark]
    public void GetLegalMoves() {
        _ = Movegen.GetLegalMoves(ref _position, stackalloc Move[128]);
    }

    // [Benchmark]
    // public void ZobristGetHash() {
    //     ulong hash = ZobristHash.GetHash(_position);
    // }

    // [Benchmark]
    // public void StaticEval() {
    //     int x = Eval.StaticEval(_position);
    // }

    // [Benchmark]
    // public void PlayMove() {
    //     var clone = _position.Clone();
    //     clone.PlayMove(_move);
    // }

    // [Benchmark]
    // public void PlayMoveReversible() {
    //     var clone = _position.Clone();
    //     clone.PlayReversibleMove(_move, Color.WHITE);
    // }
}

#pragma warning restore CA1822
#pragma warning restore CA1515

#pragma warning restore IDE0079