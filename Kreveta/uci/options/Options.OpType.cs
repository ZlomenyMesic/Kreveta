//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// ReSharper disable InconsistentNaming
namespace Kreveta.uci.options;

internal static partial class Options {
    // the UCI Protocol defines a couple option types (we only
    // implement the ones we want). CHECK is essentially a boolean
    // value. SPIN is an integer value. BUTTON has no value but
    // should trigger an action, and STRING has a text value
    private enum OpType : byte {
        CHECK, SPIN, BUTTON, STRING
    }
}