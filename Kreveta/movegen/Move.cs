//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// Remove unnecessary suppression
#pragma warning disable IDE0079

using Kreveta.consts;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Kreveta.movegen;

// Equals not implemented
#pragma warning disable CS0660 

// GetHashCode not implemented
#pragma warning disable CS0661

[StructLayout(LayoutKind.Explicit, Size = 4)]
internal readonly struct Move : IEquatable<Move> {

#pragma warning restore CS0660
#pragma warning restore CS0661

    /*
    although it is possible to encode a move into 16 bits, we are using 32 bits (int)
    this is the format:

                          | prom. | capt. | piece | end         | start              
    0 0 0 0 0 0 0 0 0 0 0 | 0 0 0 | 0 0 0 | 0 0 0 | 0 0 0 0 0 0 | 0 0 0 0 0 0

    */
    [field: FieldOffset(0)] 
    private readonly int _flags = 0;

    // constants for proper information lookups
    private const int EndOffset   = 6;
    private const int PieceOffset = 12;
    private const int CaptOffset  = 15;
    private const int PromOffset  = 18;

    private const int StartMask = 0x0000003F;
    private const int EndMask   = 0x00000FC0;
    private const int PieceMask = 0x00007000;
    private const int CaptMask  = 0x00038000;
    private const int PromMask  = 0x001C0000;
    
    // we define the move as readonly, so values can only be set
    // during initialization, and then the move becomes immutable
    internal Move(int start, int end, PType piece, PType capture, PType promotion) {

        // flags are only set when the constructor is called, after that they cannot be changed
        _flags |= start;
        _flags |= end             << EndOffset;
        _flags |= (byte)piece     << PieceOffset;
        _flags |= (byte)capture   << CaptOffset;
        _flags |= (byte)promotion << PromOffset;
    }

    // here we just define or override a bunch of operators and
    // methods allowing us to compare moves quickly by their flags
    public static bool operator ==(Move a, Move b)
        => a._flags == b._flags;

    public static bool operator !=(Move a, Move b)
        => a._flags != b._flags;
    
    public bool Equals(Move other)
        => _flags == other._flags;

    public override bool Equals(object? obj)
        => obj is Move other && Equals(other);

    public override int GetHashCode()
        => _flags;

    // and now we get to the readonly properties
    // (they aren't marked as readonly, since the
    // whole struct is already readonly)
    
    // index of the starting sqaure
    internal int Start
        => _flags & StartMask;

    // index of the ending sqaure
    internal int End
        => (_flags & EndMask) >> EndOffset;

    // the moved piece type
    internal PType Piece
        => (PType)((_flags & PieceMask) >> PieceOffset);

    // captured piece
    internal PType Capture
        => (PType)((_flags & CaptMask) >> CaptOffset);

    // piece promoted to
    // (pawn for en passant or king for castling)
    internal PType Promotion
        => (PType)((_flags & PromMask) >> PromOffset);


    // taken from my previous engine
    internal static bool IsCorrectFormat(string str) {

        // to prevent index out of range
        if (str.Length is not 4 and not 5)
            return false;

        // checks if the move from the user input makes sense (doesn't check legality)
        // caution: don't try to understand this mess
        return Consts.Files.Contains(str[0], StringComparison.Ordinal) && char.IsDigit(str[1]) 
            && Consts.Files.Contains(str[2], StringComparison.Ordinal) && char.IsDigit(str[3])
            
            // a regular move (from-to squares)
            && (str.Length == 4
                
            // a promotion (from-to squares + promotion piece)    
            || (str.Length == 5 && Consts.Pieces.Contains(str[4], StringComparison.Ordinal)));
    }
}

// we convert the moves from or into strings back and forth,
// so i implemented this separate extension class to handle it
internal static class MoveExtenstions {
    
    // converts a move back to the long algebraic notation
    // format, see the next method for more information
    internal static string ToLongAlgNotation(this Move move) {

        int   start = move.Start;
        int   end   = move.End;
        PType prom  = move.Promotion;

        StringBuilder sb = new();
        
// Specify IFormatProvider        
#pragma warning disable CA1305

        // convert starting and ending squares to the standard format, e.g. "e4"

        sb.Append(Consts.Files[start & 7] + (8 - (start >> 3)).ToString());
        sb.Append(Consts.Files[end   & 7] + (8 - (end   >> 3)).ToString());

#pragma warning restore CA1305 

        // if no promotion => empty string
        if (prom != PType.PAWN && prom != PType.KING && prom != PType.NONE)
            sb.Append(prom.ToChar());

        return sb.ToString();
    }
    
    // converts a string to a move object
    internal static Move ToMove(this string str, [In, ReadOnly(true)] in Board board) {

        // the move in the string is stored using a form of Long Algebraic Notation (LAN),
        // which is used by UCI. there is no information about the piece moved, only the starting square
        // and the destination (e.g. "e2e4"), or an additional character for the promotion (e.g. "e7e8q").

        // indices of starting and ending squares
        int start = (8 - (str[1] - '0')) * 8 + Consts.Files.IndexOf(str[0], StringComparison.Ordinal);
        int end   = (8 - (str[3] - '0')) * 8 + Consts.Files.IndexOf(str[2], StringComparison.Ordinal);

        // find the piece types
        PType piece = board.PieceAt(start);
        PType capt  = board.PieceAt(end);

        // potential promotion?
        PType prom = str.Length == 5 
            ? str[4].ToPType() 
            : PType.NONE;

        // overriding promotion:
        // castling (prom = king)
        if (piece == PType.KING && str is "e8c8" or "e8g8" or "e1c1" or "e1g1") 
            prom = PType.KING;

        // en passant (prom = pawn)
        if (piece == PType.PAWN && capt == PType.NONE && str[0] != str[2]) 
            prom = PType.PAWN;

        return new Move(start, end, piece, capt, prom);
    }
}

#pragma warning restore IDE0079