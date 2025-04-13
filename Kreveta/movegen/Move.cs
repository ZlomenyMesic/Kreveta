//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Kreveta.movegen;

// Equals not implemented
#pragma warning disable CS0660 

// GetHashCode not implemented
#pragma warning disable CS0661

[StructLayout(LayoutKind.Explicit, Size = 4)]
internal readonly struct Move {

#pragma warning restore CS0660
#pragma warning restore CS0661

    /*
    although it is possible to encode a move into 16 bits, we are using 32 bits (int)
    this is the format:

                          | prom. | capt. | piece | end         | start              
    0 0 0 0 0 0 0 0 0 0 0 | 0 0 0 | 0 0 0 | 0 0 0 | 0 0 0 0 0 0 | 0 0 0 0 0 0

    */
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    [FieldOffset(0)] 
    private readonly int _flags = 0;

    private const int EndOffset   = 6;
    private const int PieceOffset = 12;
    private const int CaptOffset  = 15;
    private const int PromOffset  = 18;

    private const int StartMask = 0x0000003F;
    private const int EndMask   = 0x00000FC0;
    private const int PieceMask = 0x00007000;
    private const int CaptMask  = 0x00038000;
    private const int PromMask  = 0x001C0000;

    internal Move(int start, int end, PType piece, PType capture, PType promotion) {

        // flags are only set when the constructor is called, after that they cannot be changed
        _flags |= start;
        _flags |= end             << EndOffset;
        _flags |= (byte)piece     << PieceOffset;
        _flags |= (byte)capture   << CaptOffset;
        _flags |= (byte)promotion << PromOffset;
    }

    public static bool operator ==(Move a, Move b)
        => a._flags == b._flags;

    public static bool operator !=(Move a, Move b)
        => !(a._flags == b._flags);

    // index of the starting sqaure
    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly int Start
        => _flags & StartMask;

    // index of the ending sqaure
    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly int End
        => (_flags & EndMask) >> EndOffset;

    // the moved piece type
    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly PType Piece
        => (PType)((_flags & PieceMask) >> PieceOffset);

    // captured piece (6 if no capture)
    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly PType Capture
        => (PType)((_flags & CaptMask) >> CaptOffset);

    // piece promoted to (6 if no promotion)
    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly PType Promotion
        => (PType)((_flags & PromMask) >> PromOffset);


    // taken from my previous engine
    internal static bool IsCorrectFormat(string str) {

        // checks if the move from the user input makes sense (doesn't check legality)
        // caution: don't try to understand this mess
        return str.Length >= 4 && Consts.Files.Contains(str[0]) && char.IsDigit(str[1]) && Consts.Files.Contains(str[2]) && char.IsDigit(str[3])
            && (str.Length == 4 || (str.Length == 5 && Consts.Pieces.Contains(str[4])));
    }

    // converts a move back to a string, see the method below for more information
    public override string ToString() {

        int   start = Start;
        int   end   = End;
        PType prom  = Promotion;
        
        // convert starting and ending squares to standard format, e.g. "e4"
        string str_start = Consts.Files[start % 8] + (8 - start / 8).ToString();
        string str_end   = Consts.Files[end   % 8] + (8 - end   / 8).ToString();

        // if no promotion => empty string
        string promotion = (prom != PType.PAWN && prom != PType.KING && prom != PType.NONE) 
            ? Consts.Pieces[(byte)prom].ToString() : "";

        return $"{str_start}{str_end}{promotion}";
    }

    // converts a string to a move object
    internal static Move FromString(Board board, string str) {

        // the move in the string is stored using a form of Long Algebraic Notation (LAN),
        // which is used by UCI. there is no information about the piece moved, only the starting square
        // and the destination (e.g. "e2e4"), or an additional character for the promotion (e.g. "e7e8q").

        // indices of starting and ending squares
        int start = ((8 - (str[1] - '0')) * 8) + Consts.Files.IndexOf(str[0]);
        int end   = ((8 - (str[3] - '0')) * 8) + Consts.Files.IndexOf(str[2]);

        // find the piece types
        (_, PType piece) = board.PieceAt(start);
        (_, PType capt)  = board.PieceAt(end);

        // potential promotion?
        PType prom = str.Length == 5 
            ? (PType)Consts.Pieces.IndexOf(str[4]) 
            : PType.NONE;

        // overriding promotion:
        // castling (prom = king)
        if (piece == PType.KING && (str == "e8c8" || str == "e8g8" || str == "e1c1" || str == "e1g1")) 
            prom = PType.KING;

        // en passant (prom = pawn)
        if (piece == PType.PAWN && capt == PType.NONE && str[0] != str[2]) 
            prom = PType.PAWN;

        return new Move(start, end, piece, capt, prom);
    }
}