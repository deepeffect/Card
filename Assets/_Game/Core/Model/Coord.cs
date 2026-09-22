using System;

namespace DortGok.Core
{
    /// <summary>Tahtadaki bir kare (satır, sütun). Aynı yapı yön vektörü olarak da kullanılır.</summary>
    public readonly struct Coord : IEquatable<Coord>
    {
        public readonly int Row;
        public readonly int Col;

        public Coord(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public static readonly Coord None = new Coord(-1, -1);
        public static readonly Coord Up = new Coord(-1, 0);
        public static readonly Coord Down = new Coord(1, 0);
        public static readonly Coord Left = new Coord(0, -1);
        public static readonly Coord Right = new Coord(0, 1);

        /// <summary>Hareket yönleri (GDD: birimler 4 yönde ilerler).</summary>
        public static readonly Coord[] Dirs4 = { Up, Down, Left, Right };

        /// <summary>Menzil ve bitişiklik yönleri (GDD: menzil 8 yönde sayılır).</summary>
        public static readonly Coord[] Dirs8 =
        {
            new Coord(-1, -1), Up, new Coord(-1, 1),
            Left, Right,
            new Coord(1, -1), Down, new Coord(1, 1)
        };

        public bool IsOnBoard => Row >= 0 && Col >= 0 && Row < Board.Size && Col < Board.Size;

        /// <summary>8 yönlü mesafe: çapraz komşu da 1 kare.</summary>
        public int Chebyshev(Coord other) => Math.Max(Math.Abs(Row - other.Row), Math.Abs(Col - other.Col));

        /// <summary>4 yönlü mesafe.</summary>
        public int Manhattan(Coord other) => Math.Abs(Row - other.Row) + Math.Abs(Col - other.Col);

        public Coord Sign() => new Coord(Math.Sign(Row), Math.Sign(Col));

        public static Coord operator +(Coord a, Coord b) => new Coord(a.Row + b.Row, a.Col + b.Col);
        public static Coord operator -(Coord a, Coord b) => new Coord(a.Row - b.Row, a.Col - b.Col);
        public static Coord operator -(Coord a) => new Coord(-a.Row, -a.Col);
        public static bool operator ==(Coord a, Coord b) => a.Row == b.Row && a.Col == b.Col;
        public static bool operator !=(Coord a, Coord b) => !(a == b);

        public bool Equals(Coord other) => this == other;
        public override bool Equals(object obj) => obj is Coord other && this == other;
        public override int GetHashCode() => Row * 397 ^ Col;
        public override string ToString() => $"({Row},{Col})";
    }
}
