using System.Collections.Immutable;

namespace Wires.Core.Sim;

record TruthTableData(ImmutableArray<string> Headers, bool[][] Rows)
{
    public static readonly TruthTableData NOT = new(
        [ "A", "Y" ],
        [
            [ true, false ],
            [ false, true ]
        ]);


    public static readonly TruthTableData AND = new(
        [ "A", "B", "Y" ],
        [
            [ true, true, true ],
            [ true, false, false ],
            [ false, true, false ],
            [ false, false, false ]
        ]);


    public static readonly TruthTableData NAND = new(
        [ "A", "B", "Y" ],
        [
            [ true, true, false ],
            [ true, false, true ],
            [ false, true, true ],
            [ false, false, true ]
        ]);


    public static readonly TruthTableData OR = new(
        [ "A", "B", "Y" ],
        [
            [ true, true, true ],
            [ true, false, true ],
            [ false, true, true ],
            [ false, false, false ]
        ]);


    public static readonly TruthTableData NOR = new(
        ["A", "B", "Y"],
        [
            [ true, true, false ],
            [ true, false, false ],
            [ false, true, false ],
            [ false, false, true ]
        ]);

    public static readonly TruthTableData XOR = new(
        ["A", "B", "Y"],
        [
            [ true, true, false ],
                [ true, false, true ],
                [ false, true, true ],
                [ false, false, false]
        ]);
    public static readonly TruthTableData XNOR = new(
        ["A", "B", "Y"],
        [
            [ true, true, true ],
                [ true, false, false ],
                [ false, true, false ],
                [ false, false, true ]
        ]);
    public static readonly TruthTableData HALF_ADDER = new(
    ["A", "B", "S", "C"],
    [
        [ false, false, false, false ],
            [ false, true,  true,  false ],
            [ true,  false, true,  false ],
            [ true,  true,  false, true  ]
    ]);

    public static readonly TruthTableData FULL_ADDER = new(
        ["A", "B", "C", "S", "O"],
        [
            [ false, false, false, false, false ],
            [ false, false, true,  true,  false ],
            [ false, true,  false, true,  false ],
            [ false, true,  true,  false, true  ],
            [ true,  false, false, true,  false ],
            [ true,  false, true,  false, true  ],
            [ true,  true,  false, false, true  ],
            [ true,  true,  true,  true,  true  ]
        ]);
}
