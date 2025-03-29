/*
 * |============================|
 * |                            |
 * |    Kreveta chess engine    |
 * | engineered by ZlomenyMesic |
 * | -------------------------- |
 * |      started 4-3-2025      |
 * | -------------------------- |
 * |                            |
 * | read README for additional |
 * | information about the code |
 * |    and usage that isn't    |
 * |  included in the comments  |
 * |                            |
 * |============================|
 */

using System.Runtime.CompilerServices;
using System.Text;

namespace Stockshrimp_1;

internal static class Options {
    internal enum OptionType {
        CHECK, SPIN, BUTTON, COMBO, STRING
    }

    internal struct Option {
        internal string name;
        internal OptionType type;

        internal string min_value;
        internal string max_value;
        internal string[] combo_values;

        internal string def_value;
        internal string value;
    }

    internal static Option own_book = new() {
        name = "OwnBook",
        type = OptionType.CHECK,

        def_value = "true",
        value = "true"
    };

    internal static Option hash = new() {
        name = "Hash",
        type = OptionType.SPIN,

        min_value = "1",
        max_value = "2048",

        def_value = "100",
        value = "100"
    };

    private static readonly Option[] options = [own_book, hash];

    internal static void Print() {
        foreach (Option opt in options) {
            StringBuilder sb = new();

            sb.Append($"option name {opt.name}");

            string type = opt.type switch { 
                OptionType.CHECK  => "check",
                OptionType.SPIN   => "spin",
                OptionType.BUTTON => "button",
                OptionType.COMBO  => "combo",
                OptionType.STRING => "string",
                _ => ""
            };

            sb.Append($" type {type}");

            if (opt.type == OptionType.CHECK || opt.type == OptionType.STRING) {
                sb.Append($" default {opt.def_value}");
            }

            else if (opt.type == OptionType.SPIN) {
                sb.Append($" default {opt.def_value} min {opt.min_value} max {opt.max_value}");
            }

            else if (opt.type == OptionType.COMBO) {
                sb.Append($" default {opt.def_value}");
                foreach (string var in opt.combo_values)
                    sb.Append($" var {var}");
            }

            Console.WriteLine(sb.ToString());
        }
    }

    internal static void SetOption(string[] toks) {

    }
}