using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

namespace Pgn2Obk
{
    internal class Settings
    {
        public byte    ply; //Maximum number of half-moves.
        public uint    minGamesN; //Minimum number of games in which the move must have been played to be included in the book.
        private byte   maxGoodAlternativesExpectedN; /*Statistically, there are about 4 really important moves in any opening position.
        Rather than assigning a priority based solely on the ratio of the number of games in which a particular move has been played to the number of games in the opening (which would penalize secondary variations too much), this and the next parameter are used to assign a more balanced priority to the first N variations of each position.*/
        private double tolleranceFromExpectedValue; 
        public byte   GetMaxGoodAlternativesExpectedN()           { return maxGoodAlternativesExpectedN; }
        public void   SetMaxGoodAlternativesExpectedN(byte value) { if (value > 0) maxGoodAlternativesExpectedN = value; }
        public double GetTolleranceFromExpectedValue()            { return tolleranceFromExpectedValue; }
        public void SetTolleranceFromExpectedValue(double value)  { if (value > 0 && value <= 1) tolleranceFromExpectedValue = value; }
        public Settings()
        {
            ply = 40;
            minGamesN = 2;
            maxGoodAlternativesExpectedN = 4;
            tolleranceFromExpectedValue = 0.7;
        }
    }
    //The algebraic notation does not indicate what the starting square is when there is only one piece that can legally make that move. To determine the starting square of the moves, classes representing the pieces and the board are needed.
    internal abstract class Piece
    {
        protected bool  colour; //white = true
        protected char? symbol; //The symbol it has in algebraic notation.
        protected char  sketch; //Symbol for the representation on the chessboard.
        public Piece (bool isWhite) { colour = isWhite; }
        public bool  GetColour() { return colour; }
        public char? GetSymbol() { return symbol; }
        public char  GetSketch() { return sketch; }
        public abstract bool CanGoThere(((byte row, byte col) from, (byte row, byte col) to) move, ChessBoard board, bool isATake); //Indicates whether the piece can legally move to a given square.
    }
    internal class Pawn : Piece
    {
        public Pawn(bool isWhite) : base(isWhite) { symbol = null; sketch = '♟'; }
        public override bool CanGoThere  (((byte row, byte col) from, (byte row, byte col) to) move, ChessBoard board, bool isATake) { return (isATake ? CanTakeThat(move) : CanMoveThere(move)) && board.isMoveFreeFromPin(this, move); }
        private bool         CanMoveThere(((byte row, byte col) from, (byte row, byte col) to) move) //for pawn movement.
        {
            sbyte versor = (sbyte)(colour? 1 : -1);
            return move.from.col == move.to.col && (move.from.row + versor == move.to.row || (move.from.row == (sbyte)(colour ? 1 : 6) && move.to.row == move.from.row + versor * 2));
        }
        private bool         CanTakeThat (((byte row, byte col) from, (byte row, byte col) to) move) { return move.to.row == move.from.row + (colour ? 1 : -1) && Math.Abs(move.from.col - move.to.col) == 1; } //for pawn captures.
    }
    internal class Rock : Piece
    {
        public Rock(bool isWhite) : base(isWhite)   { symbol = 'R'; sketch = '♜'; }
        public override bool CanGoThere(((byte row, byte col) from, (byte row, byte col) to) move, ChessBoard board, bool isATake) { return board.SquareAttacker(!colour, move) == this && board.isMoveFreeFromPin(this, move); }
    }
    internal class Knight : Piece
    {
        public Knight(bool isWhite) : base(isWhite) { symbol = 'N'; sketch = '♞'; }
        public override bool CanGoThere(((byte row, byte col) from, (byte row, byte col) to) move, ChessBoard board, bool isATake) { return (Math.Abs(move.from.col -move.to.col) == 2 && Math.Abs(move.from.row - move.to.row) == 1) || (Math.Abs(move.from.col - move.to.col) == 1 && Math.Abs(move.from.row - move.to.row) == 2) && board.isMoveFreeFromPin(this, move); }
    }
    internal class Bishop : Piece
    {
        public Bishop(bool isWhite) : base(isWhite) { symbol = 'B'; sketch = '♝'; }
        public override bool CanGoThere(((byte row, byte col) from, (byte row, byte col) to) move, ChessBoard board, bool isATake) { return board.SquareAttacker(!colour, move) == this && board.isMoveFreeFromPin(this, move); }
    }
    internal class Queen : Piece
    {
        public Queen(bool isWhite) : base(isWhite)  { symbol = 'Q'; sketch = '♛'; }
        public override bool CanGoThere(((byte row, byte col) from, (byte row, byte col) to) move, ChessBoard board, bool isATake) { return board.SquareAttacker(!colour, move) == this && board.isMoveFreeFromPin(this, move); }
    }
    internal class King : Piece
    {
        public King(bool isWhite) : base(isWhite)  { symbol = 'K'; sketch = '♚'; }
        public override bool CanGoThere(((byte row, byte col) from, (byte row, byte col) to) move, ChessBoard board, bool isATake) { return Math.Abs(move.from.row - move.to.row) < 2 && Math.Abs(move.from.col - move.to.col) < 2 && (board.GetPiece(move.to) == null || board.GetPiece(move.to).GetColour() != colour) && !board.IsAttacked(move.to, !GetColour(), true); }
    }
    internal class ChessBoard
    {
        public List<Piece[,]> squares; //It's a list of 8x8 square matrices, each of which represents every position reached during the game. The last element is the current position.
        public ChessBoard() //Set up starting position.
        {
            squares = new List<Piece[,]> { new Piece[8, 8] };
            bool isWhite = true;
            for (byte row = 1; row < 7; row += 5)
            {
                for (byte col = 0; col < 8; col++) squares.Last()[row, col] = new Pawn(isWhite);
                isWhite = false;
            }
            for (byte row = 7; row < 8; row -= 7)
            {
                for (byte col = 0; col < 8; col++)
                    switch (col)
                    {
                        case 0: case 7: squares.Last()[row, col] = new Rock(isWhite); break;
                        case 1: case 6: squares.Last()[row, col] = new Knight(isWhite); break;
                        case 2: case 5: squares.Last()[row, col] = new Bishop(isWhite); break;
                        case 3: squares.Last()[row, col] = new Queen(isWhite); break;
                        case 4: squares.Last()[row, col] = new King(isWhite); break;
                    }
                isWhite = true;
            }
        }
        public Piece GetPiece((byte row, byte col) square) { return (square.row < 8 && square.col < 8 ? squares.Last()[square.row, square.col] : null); } //Returns the piece occupying a given square.
        public unsafe void PrintChessboard() //Print the board to screen.
        {
            for (byte row = 7; row < 8; row--)
            {
                for (byte col = 0; col < 8; col++)
                {
                    Console.BackgroundColor = (row + col) % 2 == 0 ? ConsoleColor.DarkGray : ConsoleColor.Gray; //Change the colors of the squares.
                    if (squares.Last()[row, col] != null)
                    {
                        Console.ForegroundColor = (squares.Last()[row, col].GetColour() ? ConsoleColor.White : ConsoleColor.Black); //Change the colors of the pieces.
                        Console.Write(" " + squares.Last()[row, col].GetSketch());
                    }
                    else Console.Write("  ");
                }
                Console.WriteLine();
            }
            Console.ResetColor();
            Console.WriteLine();
        }
        private string DisambiguationAlgNot(((byte row, byte col) from, (byte row, byte col) to) move, bool isATake) //Check if there is more than one piece that can legally make this move, if so, resolve the ambiguity by adding the starting column or row in algebraic notation.
        {
            string result = "";
            Piece moved = squares.Last()[move.from.row, move.from.col];
            for (byte row = 0; row < 8; row++)
                for (byte col = 0; col < 8; col++)
                {
                    Piece examined = squares.Last()[row, col];
                    if (examined != null && examined.GetColour() == moved.GetColour() && moved.GetSymbol() == examined.GetSymbol() && examined != moved && examined.CanGoThere(((row, col), move.to), this, isATake)) return ((char)(move.from.row == row ? move.from.col + 'a' : move.from.row + 1)).ToString();
                }
            return result;
        }
        private ((byte row, byte col) attacker, bool isDoubleCheck) Checker(((byte row, byte col) from, (byte row, byte col) to) move) //Determines if the move results in a check. Returns the coordinates of the possible attacker's square and a boolean indicating whether the check is a double.
        {
            Piece piece = squares.Last()[move.to.row, move.to.col];
            Piece backPiece = SquareAttacker(!piece.GetColour(), (move.from, FindKingPosition(!piece.GetColour())));
            (byte row, byte col) frontAttacker = piece.CanGoThere((move.to, FindKingPosition(!piece.GetColour())), this, true) ? GetPosition(piece) : ((byte)8, (byte)8);
            (byte row, byte col) backAttacker = backPiece != null ? GetPosition(backPiece) : ((byte)8, (byte)8);
            return (frontAttacker != (8, 8)? frontAttacker : backAttacker, frontAttacker != (8, 8) && backAttacker != (8, 8));
        }
        public bool IsAttacked((byte row, byte col) square, bool color, bool isATake) //Checks if a square is controlled by enemy pieces.
        {
            for (byte row = 0; row < 8; row++)
                for (byte col = 0; col < 8; col++)
                    if (squares.Last()[row, col] != null && squares.Last()[row, col].GetColour() == color && squares.Last()[row, col].CanGoThere(((row, col), square), this, isATake))
                        return true; 
            return false;
        }
        private bool IsMate(((byte row, byte col) position, bool isDoubleChek) checker) //Checks if a check is mate.
        {
            if (checker.position.row > 7 || checker.position.col > 7) return false;
            bool checkerColor = squares.Last()[checker.position.row, checker.position.col].GetColour();
            (byte row, byte col) king = FindKingPosition(!checkerColor);
            for (int row = king.row - 1; row < king.row + 2; row++) //Check if the king can move (the only legal option if the check is double).
                for (int col = king.col - 1; col < king.col + 2; col++)
                    if (row < 8 && col < 8 && row >= 0 && col >= 0 && squares.Last()[king.row, king.col].CanGoThere((king, ((byte)row, (byte)col)), this, true))
                        return false;
            if (!checker.isDoubleChek)
            {
                for (byte row = 0; row < 8; row++) //Check if it is possible to capture the attacker.
                    for (byte col = 0; col < 8; col++)
                        if (squares.Last()[row, col] != null && squares.Last()[row, col].GetColour() != checkerColor && king != (row, col) && squares.Last()[row, col].CanGoThere(((row, col), checker.position), this, true)) return false;
            (int row, int col) versors = Direction((checker.position, king)).versors; //Check if it is possible to block (if the attacker is a rook, queen or bishop).
                if (versors != (0, 0))
                    do
                    {
                        king.row = (byte)(king.row + versors.row);
                        king.col = (byte)(king.col + versors.col);
                        if (squares.Last()[king.row, king.col] == null && IsAttacked(king, !checkerColor, false)) return false;
                    } while (king.row < checker.position.row && king.col < checker.position.col);
            }
            return true;
        }
        private string CheckAlgNot(((byte row, byte col) from, (byte row, byte col) to) move) //Adds to the algebraic notation of a move whether it is a check or a checkmate.
        {
            string result = "";
            Move(move);
            ((byte row, byte col) attacker, bool isDoubleCheck) checker = Checker(move);
            if (checker.attacker != (8,8))
                if (IsMate(checker)) result = "#";
                else result = "+";
            squares.RemoveAt(squares.Count() - 1);
            return result;
        }
        private string MoveAlgNot(((byte row, byte col) from, (byte row, byte col) to) move) //Returns the algebraic notation of the move.
        {
            string result = squares.Last()[move.from.row, move.from.col].GetSymbol().ToString();
            bool isATake = squares.Last()[move.to.row, move.to.col] != null;
            if (result == "K")
            {
                if (move.from.col == 4 && (move.from.row == 0 || move.from.row == 7)) //Castle
                    if (move.to.col == 2)
                    {
                        squares.Last()[move.from.row, 3] = squares.Last()[move.from.row, 0];
                        squares.Last()[move.from.row, 0] = null;
                        return "O-O-O";
                    }
                    else if (move.to.col == 6)
                    {
                        squares.Last()[move.from.row, 5] = squares.Last()[move.from.row, 7];
                        squares.Last()[move.from.row, 7] = null;
                        return "O-O";
                    }
            }
            else result += DisambiguationAlgNot(move, isATake);
            result = result + (isATake ? "x" : "") + (char)(move.to.col + 'a') + (move.to.row + 1); //"x" indicates a capture.
            if (Char.IsLower(result[0]) && (move.from.row == 0 || move.from.row == 7)) result += "=Q"; //The promotion is forced to queen.
            return result + CheckAlgNot(move);
        }
        public int ChoiceCandidates(List<Move> moves, bool notFirstLevel) //The menu with the moves available from that position.
        {
            Console.WriteLine("Select an option:");
            if (notFirstLevel) Console.WriteLine("0 - To go back to the previous move");
            for (int i = 0; i < moves.Count; i++)
            {
                Console.Write((i + 1) + " - " + MoveAlgNot(moves[i].squares) + " ");
                for (int j = 0; j < moves[i].GetPriority(); j++) Console.Write("★");
                Console.WriteLine();
            }
            Console.WriteLine("Or any other text to exit.");
            if (int.TryParse(Console.ReadLine(), out int c) && c >= 0 && c <= moves.Count) return c -1;            
            return -2;
        }
        public ((byte row, byte col) from, (byte row, byte col) to) ReadMove(string move, bool colour) //Interpret algebraic notation.
        {

            ((byte row, byte col) from, (byte row, byte col) to) result;
            result.from = (8, 8);
            if (move.Contains("O")) result = Castle(colour, move.Equals("O-O"));
            else
            {
                result.to.row = (byte)(move[move.Length - 1] - '1');
                result.to.col = (byte)(move[move.Length - 2] - 'a');
                move = move.Substring(0, move.Length - 2);
                bool isATake = move.Contains("x");
                if (isATake) move = move.Remove(move.IndexOf("x"), 1);
                if (move.Length > 0)
                    if (Char.IsLower(move[move.Length - 1])) result.from.col = (byte)(move[move.Length - 1] - 'a');
                    else if (Char.IsDigit(move[move.Length - 1])) result.from.row = (byte)(move[move.Length - 1] - '1');
                char? symbol = null;
                if (move.Length > 0 && Char.IsUpper(move[0])) symbol = move[0];
                result.from = FindSquareFrom(symbol, colour, isATake, result);
            }            
            Move(result);
            return result;
        }
        public (byte row, byte col) GetPosition(Piece piece) //Returns the board coordinates of a given piece.
        {
            for (byte row = 0; row < 8; row++)
                for (byte col = 0; col < 8; col++)
                    if (squares.Last()[row, col] == piece) return (row, col);
            return (8, 8);
        }
        public void Move(((byte row, byte col) from, (byte row, byte col) to) move) //Makes the move on the board.
        {
            squares.Add((Piece[,])squares.Last().Clone());
            if ((move.to.row == 7 || move.to.row == 0) && squares.Last()[move.from.row, move.from.col].GetSymbol() == null) squares.Last()[move.to.row, move.to.col] = new Queen(move.to.row > 6); //Forced promotion to queen.
            else squares.Last()[move.to.row, move.to.col] = squares.Last()[move.from.row, move.from.col];
            squares.Last()[move.from.row, move.from.col] = null;

        }
        private (byte, byte) FindKingPosition(bool colour) //Find the coordinates of the king's square of a certain color.
        {
            for (byte row = 0; row < 8; row++)
                for (byte col = 0; col < 8; col++)
                    if (squares.Last()[row, col] != null && squares.Last()[row, col].GetColour() == colour && squares.Last()[row, col].GetSymbol() == 'K') return (row, col);
            return (8, 8);
        }
        public bool isMoveFreeFromPin(Piece piece, ((byte row, byte col) from, (byte row, byte col) to) move) //Checks if a given move leads to a discovery check of the own king.
        {
            Move(move);
            bool result = SquareAttacker(piece.GetColour(), (move.from, FindKingPosition(piece.GetColour()))) == null;
            squares.RemoveAt(squares.Count() - 1);
            return result;
        }

        private ((int row, int col) versors, char pieceSymbol) Direction(((byte row, byte col) from, (byte row, byte col) to) move) //Returns the unit vectors of the direction in which to move from one square to another and the symbol for the type of piece moving in that direction (rook if moving horizontally or vertically, bishop if moving diagonally).
        {
            (int row, int col) versors = (0, 0);
            char pieceSymbol = 'R';
            if (move.from.row == move.to.row)      versors.col = move.from.col > move.to.col ? 1 : -1;
            else if (move.from.col == move.to.col) versors.row = move.from.row > move.to.row ? 1 : -1;
            else if (move.from.row - move.from.col == move.to.row - move.to.col)
            {
                pieceSymbol = 'B';
                versors.row = move.from.row > move.to.row ? 1 : -1;
                versors.col = versors.row;
            }
            else if (move.from.row + move.from.col == move.to.row + move.to.col)
            {
                pieceSymbol = 'B';
                versors.row = move.from.row > move.to.row ? 1 : -1;
                versors.col = -1 * versors.row;
            }
            return (versors, pieceSymbol);
        }

        public Piece SquareAttacker(bool colour, ((byte row, byte col) from, (byte row, byte col) to) move) //Returns the piece (among those that can move in a line) that can attack a given square from a given direction. I use it both for the movements of the line pieces and to check if there is discovery check.
        {            
            ((int row, int col) versors, char attacker) direction = Direction(move);
            if (direction.versors == (0,0)) return null;                       
            List<char> attackers = new List<char> { 'Q', direction.attacker };
            do
            {
                move.to.row += (byte)direction.versors.row;
                move.to.col += (byte)direction.versors.col;
                if (move.to.row > 7 || move.to.col > 7) return null;
                if (squares.Last()[move.to.row, move.to.col] != null)
                {
                    if (squares.Last()[move.to.row, move.to.col].GetColour() != colour)
                        foreach (char attacker in attackers)
                            if (attacker == squares.Last()[move.to.row, move.to.col].GetSymbol()) return squares.Last()[move.to.row, move.to.col];
                    return null;
                }
            } while (true);
        }
        private (byte row, byte col) FindSquareFrom(char? symbol, bool colour, bool isATake, ((byte row, byte col) from, (byte row, byte col) to) move) //Find the coordinates of the starting square of a move. Essential for reading algebraic notation.
        {
            foreach (Piece piece in squares.Last())            
                if (piece != null)
                {                    
                    (byte row, byte col) result = GetPosition(piece);
                    if (piece.GetColour() == colour && piece.GetSymbol() == symbol && (move.from == (8, 8) || move.from.col == result.col || move.from.row == result.row) && piece.CanGoThere((result, move.to), this, isATake)) return result;
                }
            return (8, 8);
        }
        public ((byte row, byte col) from, (byte row, byte col) to) Castle(bool colour, bool type) //Color true = white, type true = short
        {
            ((byte row, byte col) from, (byte row, byte col) to) result;
            result.to   = ((byte)(colour ? 0 : 7), (byte)(type ? 6 : 2));
            result.from = (result.to.row, 4);
            Move(((result.to.row, (byte)(type ? 7 : 0)), (result.to.row, (byte)(type ? 5 : 3))));
            return result;
        }
    }
    internal class Level //The moves are stored in a tree structure. On the same level are the moves made from the same position. Each move has a pointer to the level with the following moves.
    {
        public List<Move> moves;
        public Level() { moves = new List<Move>(); }
        public void Store(List<List<byte>> buffer, Settings settings) //Converts moves to OBK format and adds them to a list.
        {
            moves = moves.OrderByDescending(move => move.GetGamesN()).ToList();
            uint gamesN = GetGamesNumber();
            int TotalMovesNumberInLevel = moves.Count();
            for (int i = 0; i < TotalMovesNumberInLevel; i++)
                moves[i].Store(gamesN, TotalMovesNumberInLevel, buffer, settings, i + 1 == TotalMovesNumberInLevel);
        }
        public Move AddMove(((byte row, byte col) from, (byte row, byte col) to) squares) //Adds a move to the level. If the move already exists, the number of games is increased.
        {
            foreach (Move m in moves)
                if (m.squares == squares)
                {
                    m.AddGame();
                    return m; 
                }
            Move move = new Move(squares);
            moves.Add(move);
            return move;
        }
        public void AddMove(Move move) { moves.Add(move); }
        public uint GetGamesNumber() //Returns the number of times a move has been played.
        {
            uint result = 0;
            foreach (Move move in moves) result += move.GetGamesN();
            return result;
        }
        public void CleanLesserVariants(uint minGamesN) //Remove branches from the tree with too few moves.
        {
            for (int i = 0; i < moves.Count; i++)
                if (moves[i].GetGamesN() >= minGamesN) moves[i].nextLevel.CleanLesserVariants(minGamesN);
                else
                {
                    moves.Remove(moves[i]);
                    i--;
                }
        }
    }
    internal class Move
    {
        public ((byte row, byte col) from, (byte row, byte col) to) squares; //Coordinates of the start and end box.
        private uint gamesN; //Number of games in which it was played.
        private byte priority;
        public Level nextLevel { get; }
        public Move(((byte row, byte col) from, (byte row, byte col) to) s)
        {
            squares = s;
            gamesN = 1;
            nextLevel = new Level();
        }
        public Move(byte vLFrom, byte priorityTo)
        {
            squares = (((byte)((vLFrom % 64) / 8), (byte)(vLFrom % 8)), ((byte)((priorityTo % 64) / 8), (byte)(priorityTo % 8)));
            priority = (byte)(priorityTo / 64);
            nextLevel = new Level();
        }
        public Move(uint pointer, BinaryReader br, Level level, Settings settings)  //Constructor used when reading moves from an ABK file.
        {
            nextLevel = new Level();
            uint bytesN = pointer * 28; //28 bytes per move
            br.BaseStream.Seek(bytesN + 4, SeekOrigin.Begin);
            gamesN = br.ReadUInt32();
            if (gamesN >= settings.minGamesN)
            {
                br.BaseStream.Seek(bytesN, SeekOrigin.Begin);
                squares.from = ConvertMoveFromAbk(br.ReadByte());
                squares.to =   ConvertMoveFromAbk(br.ReadByte());
                level.moves.Add(this);
                br.BaseStream.Seek(bytesN + 20, SeekOrigin.Begin);
                uint nextInVariationPointer = br.ReadUInt32();
                if (nextInVariationPointer < UInt32.MaxValue) new Move(nextInVariationPointer, br, nextLevel, settings); //In the ABK format there are two pointers: one to the next move and one to the next sibling (next alternative move at this level). The move pointer gives the effective move number within the file (i.e. if it contains 903 it indicates the 3rd move present in the file at the byte address 903 * 28 = 25284).
            }
            br.BaseStream.Seek(bytesN + 24, SeekOrigin.Begin);
            uint nextSiblingPointer = br.ReadUInt32();
            if (nextSiblingPointer < UInt32.MaxValue) new Move(nextSiblingPointer, br, level, settings);
        }
        private (byte, byte) ConvertMoveFromAbk(byte square) { return ((byte)(square / 8), (byte)(square % 8)); } //Reads the coordinates of a square in ABK format (which uses 0 for the a1 square and 63 for the h8 square).
        private byte ObkSquare((int row, int col) square) { return (byte)((square.row << 3) + square.col); }
        public uint  GetGamesN()                          { return gamesN; }
        public byte  GetPriority()                        { return priority; }
        public void  AddGame()                            { gamesN++; }
        public void Store(uint levelGamesN, int movesNInLevel, List<List<byte>> buffer, Settings settings, bool isLastInLevel) //Writes moves in OBK format and places them in a byte list ready to be written to the file.
        {
            SetPriority(levelGamesN, movesNInLevel, settings);
            if(buffer.Last().Count > 20479) buffer.Add(new List<byte>()); //Divides the data into 20 kilobyte blocks for faster writing.
            /*In OBK format, moves occupy two bytes each:
            1 bit (V) to indicate if there are subsequent moves.
            1 bit (L) to indicate if there are no alternatives in that position.
            3 bits for the starting square row.
            3 bits for the starting square column.
            2 bits for the priority (0 to 4).
            3 bits for the destination square row.
            3 bits for the destination square column.*/
            buffer.Last().Add(AddHeader(Flags(nextLevel.GetGamesNumber() > 0, isLastInLevel), ObkSquare(squares.from)));
            buffer.Last().Add(AddHeader(priority, ObkSquare(squares.to)));
            if (nextLevel.GetGamesNumber() > 0) nextLevel.Store(buffer, settings);
        }
        private byte AddHeader(byte header, byte body) { return (byte)((header << 6) + body); }
        private static byte Flags(bool istInBranch, bool isLastInLevel) { return (byte)((istInBranch ? 0 : 2) + (isLastInLevel ? 1 : 0)); }
        private static double Threshold(int levelMovesN, Settings settings)
        {
            if (levelMovesN > settings.GetMaxGoodAlternativesExpectedN()) levelMovesN = settings.GetMaxGoodAlternativesExpectedN();
            return 1 / (double)levelMovesN * settings.GetTolleranceFromExpectedValue();
        }        
        private void SetPriority(uint levelGamesN, int levelMovesN, Settings settings)
        {
            double threshold = Threshold(levelMovesN, settings);
            double p = (double)gamesN / levelGamesN;
            if      (p >= threshold)     priority = 3;
            else if (p >= threshold / 2) priority = 2;
            else if (p >= threshold / 4) priority = 1;
            else priority = 0;
        }
    }
    public static class ConsoleHelper //The sole purpose of this class is to change the font and size of the console characters (only a few fonts contain chess piece symbols). This is copied verbatim from Stackoverflow.
    {
        private const int FixedWidthTrueType = 54;
        private const int StandardOutputHandle = -11;
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GetStdHandle(int nStdHandle);
        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetCurrentConsoleFontEx(IntPtr hConsoleOutput, bool MaximumWindow, ref FontInfo ConsoleCurrentFontEx);
        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool GetCurrentConsoleFontEx(IntPtr hConsoleOutput, bool MaximumWindow, ref FontInfo ConsoleCurrentFontEx);
        private static readonly IntPtr ConsoleOutputHandle = GetStdHandle(StandardOutputHandle);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct FontInfo
        {
            internal int cbSize;
            internal int FontIndex;
            internal short FontWidth;
            public short FontSize;
            public int FontFamily;
            public int FontWeight;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string FontName;
        }
        public static FontInfo[] SetCurrentFont(string font, short fontSize = 0)
        {
            FontInfo before = new FontInfo
            {
                cbSize = Marshal.SizeOf<FontInfo>()
            };
            if (GetCurrentConsoleFontEx(ConsoleOutputHandle, false, ref before))
            {
                FontInfo set = new FontInfo
                {
                    cbSize = Marshal.SizeOf<FontInfo>(),
                    FontIndex = 0,
                    FontFamily = FixedWidthTrueType,
                    FontName = font,
                    FontWeight = 400,
                    FontSize = fontSize > 0 ? fontSize : before.FontSize
                };
                if (!SetCurrentConsoleFontEx(ConsoleOutputHandle, false, ref set))
                {
                    var ex = Marshal.GetLastWin32Error();
                    Console.WriteLine("Set error " + ex);
                    throw new System.ComponentModel.Win32Exception(ex);
                }
                FontInfo after = new FontInfo
                {
                    cbSize = Marshal.SizeOf<FontInfo>()
                };
                GetCurrentConsoleFontEx(ConsoleOutputHandle, false, ref after);
                return new[] { before, set, after };
            }
            else
            {
                var er = Marshal.GetLastWin32Error();
                Console.WriteLine("Get error " + er);
                throw new System.ComponentModel.Win32Exception(er);
            }
        }
    }
    internal class Program
    {
        static private void Help()
        {
            Console.WriteLine("[-p] [-a] [-g] [-t] filename[.PGN/.ABK/.OBK]");
            Console.WriteLine("Creates a chess opening book in .OBK format from a .PGN or .ABK file and allows to examine the result. It also allows to open and explore .OBK files.");
            Console.WriteLine("-p OPTIONAL: Followed by integer (no spacing): changes the maximum lenght of the the book in semi-moves number (default = 40);");
            Console.WriteLine("-a OPTIONAL: Followed by integer (no spacing): changes the maximum number of alternatives considered competitive for each position (default = 4);");
            Console.WriteLine("-g OPTIONAL: Followed by integer (no spacing): changes the minimum number of games in which a move must have been played to be considered competitive (default = 2);");
            Console.WriteLine("-t OPTIONAL: Followed by number > 0 and <= 1 (no spacing): changes the tolerance from the average frequency for which a variant is considered competitive (default = 0.7).");
            Environment.Exit(1);
        }
        static private string ReadArguments(string[] args, Settings settings)
        {
            foreach (string arg in args)
            {
                string argLower = arg.ToLower();
                if (argLower.Equals("help")) Help();
                byte alternativesN;
                double tollerance;
                if (argLower.StartsWith("-p"))                                                          byte.TryParse(arg.Substring(2), out settings.ply);
                else if (argLower.StartsWith("-a") && byte.TryParse(arg.Substring(2), out alternativesN)) settings.SetMaxGoodAlternativesExpectedN(alternativesN);
                else if (argLower.StartsWith("-g"))                                                       uint.TryParse(arg.Substring(2), out settings.minGamesN);
                else if (argLower.StartsWith("-t") && double.TryParse(arg.Substring(2), out tollerance))  settings.SetTolleranceFromExpectedValue(tollerance);
                else if (argLower.EndsWith(".pgn") || argLower.EndsWith(".abk") || argLower.EndsWith(".obk")) return arg;
            }
            Console.WriteLine("Wrong arguments.");
            Help();
            return "";
        }
        static private void ObkHeader(BinaryWriter bw, uint totalMovesN) //The header of an OBK file contains:
        {
            bw.BaseStream.Seek(0, SeekOrigin.Begin);
            bw.Write(new char[] { 'B', 'O', 'O', '!' });                //4 fixed characters
            bw.Write(totalMovesN);                                      //the number of moves
            bw.Write((uint)0);                                          //and the number of bytes used by comments (which are appended to the file)
        }
        static private string RemoveTextBetween(string text, char startTag, char endTag)//Returns the text without the part enclosed in parentheses.
        {
            for (int startTagI = text.IndexOf(startTag); startTagI >= 0; startTagI = text.IndexOf(startTag))
            {
                int openTagNumber = 1;
                for (int i = startTagI + 1; i < text.Length; i++)
                    if (text[i] == startTag) openTagNumber++;
                    else if (text[i] == endTag)
                    {
                        openTagNumber--;
                        if (openTagNumber == 0)
                        {
                            text = text.Remove(startTagI, i - startTagI + 1);
                            break;
                        }
                    }
            }
            return text;
        }
        static private string RemoveRepeatedSpaces(string text) //Converts repeated spaces to single spaces.
        {
            int startLenght;
            do
            {
                startLenght = text.Length;
                text = text.Replace("  ", " ");

            } while (text.Length < startLenght);
            return text;
        }
        static private string RemoveJunks(string pgn) //Removes everything that is not needed from the pgn file.
        {                                                                                                         //delete all:
            pgn = RemoveTextBetween(pgn, '{', '}');                                                               //comments
            pgn = RemoveTextBetween(pgn, '[', ']');                                                               //tags
            pgn = RemoveTextBetween(pgn, '(', ')');                                                               //variants
            char[] pgnReversed = RemoveTextBetween(pgn, '$', ' ')
            .Replace("!", "").Replace("?", "")                                                                    //evaluations
            .Replace("0-1", "").Replace("1-0", "").Replace("1/2-1/2", "").Replace("*", "")                        //results
            .Replace("+", "").Replace("#", "")                                                                    //checks
            .Replace("=Q", "").Replace("=R", "").Replace("=B", "").Replace("=N", "")                              //promotions
            .Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").ToCharArray();                              //wraps and tabs
            Array.Reverse(pgnReversed);
            pgnReversed = RemoveTextBetween(new string(pgnReversed).Replace("...", "~"), '~', ' ').ToCharArray(); //junks left over from variants
            Array.Reverse(pgnReversed);
            return RemoveRepeatedSpaces(new string(pgnReversed));                                                 //extra spaces
        }
        static private string ReadPgn(string fileName)
        {
            string pgn = "";
            using (StreamReader sr = new StreamReader(fileName)) pgn = sr.ReadToEnd();
            if (pgn == "")
            {
                Console.WriteLine("Empty or missed file.");
                Environment.Exit(1);
            }
            else Console.WriteLine("Reading PGN.");
            return pgn;
        }
        static string RemoveRestOfGame(string pgn)
        {
            int endI = pgn.IndexOf(" 1.");
            if (endI >= 0) return pgn.Substring(endI);
            return "";
        }
        static private Level CreateTreeFromPgn(string pgn, Settings settings)
        {
            Level firstLevel = new Level();
            while (pgn. Length > 1)
            {
                ChessBoard board = new ChessBoard();
                Level actualLevel = firstLevel;
                int ply = 0;
                do
                {
                    Move actualMove = actualLevel.AddMove(board.ReadMove(NextMoveInPgn(ref pgn), ply % 2 == 0));
                    actualLevel = actualMove.nextLevel;                    
                    ply++;
                    if (ply == settings.ply) pgn = RemoveRestOfGame(pgn);
                } while (pgn.Length > 2 && !pgn.Substring(0, 3).Equals(" 1.")) ;
            }
            return firstLevel;
        }
        static private string NextMoveInPgn(ref string pgn) //Reads the text received from the character to find the next move of the game.
        {
            for(int i = 0; i < pgn.Length; i++)
                if(Char.IsLetter(pgn[i]))
                {
                    pgn = pgn.Substring(i);
                    int endI = pgn.IndexOf(" ");
                    string result = "";
                    if (endI > 1)
                    {
                        result = pgn.Substring(0, endI);
                        pgn = pgn.Substring(endI);
                    }
                    else pgn = "";
                    return result;
                }
            pgn = "";
            return "";
        }
        static private void NavigateTree(Level currentLevel) //Explore OBK files (read or generated) with a minimalist interface on the console.
        {
            int selection;
            List<Level> previousLevels = new List<Level>();
            ChessBoard board = new ChessBoard();
            do
            {
                board.PrintChessboard();
                selection = board.ChoiceCandidates(currentLevel.moves, previousLevels.Count > 0);
                Console.Clear();
                if (selection > -2)
                {
                    if (selection >= 0) //Go to the move selected by the user.
                    {
                        board.Move(currentLevel.moves[selection].squares);
                        previousLevels.Add(currentLevel);
                        currentLevel = currentLevel.moves[selection].nextLevel;
                    }
                    else
                    {
                        if (previousLevels.Count > 0) //Return to the previous position.
                        {
                            board.squares.RemoveAt(board.squares.Count() - 1);
                            currentLevel = previousLevels.Last();
                            previousLevels.RemoveAt(previousLevels.Count() - 1);
                        }
                        else break; //Exit the program
                    }
                }                     
            }while (selection > -2);
        }
        static private Level CreateObk(string fileName, Level firstLevel, Settings settings)
        {
            Console.WriteLine("Writing OBK.");
            firstLevel.CleanLesserVariants(settings.minGamesN);
            List<List<byte>> buffer = new List<List<byte>> { new List<byte>() };
            firstLevel.Store(buffer, settings);
            using (var stream = File.Open(fileName.Substring(0, fileName.Length - 4) + ".obk", FileMode.Create))
            using (BinaryWriter bw = new BinaryWriter(stream))
            {
                bw.Seek(12, SeekOrigin.Begin);
                uint TotalNumberBytesWritten = 0;
                foreach (List<byte> chunk in buffer)
                {                    
                    bw.Write(chunk.ToArray());
                    TotalNumberBytesWritten += (uint)chunk.Count;
                }
                ObkHeader(bw, TotalNumberBytesWritten / 2); //Because there are two bytes per move.
                Console.WriteLine("Done!");                    
            }
            return firstLevel;
        }
        static private Level CreateTreeFromAbk(string fileName, Settings settings)
        {
            Level firstLevel = new Level();
            using (var stream = File.Open(fileName, FileMode.Open))
                using (var br = new BinaryReader(stream))
                {
                    Console.WriteLine("Reading Abk.");
                    firstLevel.AddMove(new Move(900, br, firstLevel, settings)); //In abk files the header ends after the 900th byte.
            }
            return firstLevel;
        }
        static Level ReadObk(string fileName)
        {
            Level firstLevel = new Level();
            using (var stream = File.Open(fileName, FileMode.Open))
            {
                using (var br = new BinaryReader(stream))
                {
                    Console.WriteLine("Reading OBK.");
                    br.BaseStream.Seek(4, SeekOrigin.Begin);
                    uint movesNum = br.ReadUInt32();
                    br.BaseStream.Seek(12, SeekOrigin.Begin);
                    List <Level> stack = new List<Level> { firstLevel }; //The reason why I include the first level of the tree in the list will be explained later.
                    Level current = firstLevel;
                    for (uint i = 0; i < movesNum; i++)
                    {
                        byte vLFrom = br.ReadByte();
                        Move move = new Move(vLFrom, br.ReadByte());
                        current.AddMove(move);
                        bool l =(byte)((vLFrom % 128) / 64) == 1;
                        if (!l) stack.Add(current);
                        if ((byte)(vLFrom / 128) == 1) //Since the file always ends with a move that has V = 1, if I did not add an extra (otherwise useless) level to the list, when it reaches the end of the file it will try to delete a level from the list when the list is already empty.
                        {
                            current = stack.Last();
                            stack.RemoveAt(stack.Count - 1);
                        }
                        else current = move.nextLevel;
                    }
                }
            }
            return firstLevel;
        }
        static Level CreateTree (string fileName, Settings settings)
        {
            if (fileName.ToLower().EndsWith(".pgn")) return CreateObk(fileName, CreateTreeFromPgn(RemoveJunks(ReadPgn(fileName)), settings), settings);
            if (fileName.ToLower().EndsWith(".abk")) return CreateObk(fileName, CreateTreeFromAbk(fileName, settings), settings); ;
            return ReadObk(fileName);
        }
        static void SetConsole() //Graphical changes in the console.
        {
            Console.SetWindowSize(70, 30);
            ConsoleHelper.SetCurrentFont("DejaVu Sans Mono", 36);            
            Console.OutputEncoding = Encoding.Unicode;
        }
        static void Main(string[] args)
        {            
            Settings settings = new Settings();
            SetConsole();          
            NavigateTree(CreateTree(ReadArguments(args, settings), settings));
        }
    }
}