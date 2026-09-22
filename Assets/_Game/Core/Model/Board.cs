using System;
using System.Collections.Generic;

namespace DortGok.Core
{
    /// <summary>
    /// 12×12 tahta düzeni (GDD Bölüm 1). Koltuklar saat yönünde:
    /// 0 = Ay (sol üst), 1 = Güneş (sağ üst), 2 = Bulut (sağ alt), 3 = Yıldız (sol alt).
    /// </summary>
    public static class Board
    {
        public const int Size = 12;
        public const int QuadrantSize = 6;
        public const int Seats = 4;

        /// <summary>Gök Sunağı: merkezdeki 2×2 kare, her çeyrekten 1 kare.</summary>
        public static readonly Coord[] AltarTiles =
        {
            new Coord(5, 5), new Coord(5, 6), new Coord(6, 5), new Coord(6, 6)
        };

        public static readonly string[] SeatNames = { "Sol üst", "Sağ üst", "Sağ alt", "Sol alt" };

        public static Faction DefaultFaction(int seat) => (Faction)seat;

        public static int NextClockwise(int seat) => (seat + 1) % Seats;

        public static int QuadrantOf(Coord c)
        {
            if (!c.IsOnBoard) return -1;
            bool top = c.Row < QuadrantSize;
            bool left = c.Col < QuadrantSize;
            if (top) return left ? 0 : 1;
            return left ? 3 : 2;
        }

        /// <summary>Karargah, çeyreğin dış köşesindeki 2×2 kareyi kaplar.</summary>
        public static Coord HqOrigin(int seat)
        {
            switch (seat)
            {
                case 0: return new Coord(0, 0);
                case 1: return new Coord(0, 10);
                case 2: return new Coord(10, 10);
                case 3: return new Coord(10, 0);
                default: throw new ArgumentOutOfRangeException(nameof(seat));
            }
        }

        public static Coord[] HqTiles(int seat)
        {
            var o = HqOrigin(seat);
            return new[] { o, o + Coord.Right, o + Coord.Down, o + Coord.Down + Coord.Right };
        }

        public static bool IsAltar(Coord c)
        {
            for (int i = 0; i < AltarTiles.Length; i++)
                if (AltarTiles[i] == c) return true;
            return false;
        }

        public static IEnumerable<Coord> AllTiles()
        {
            for (int r = 0; r < Size; r++)
                for (int c = 0; c < Size; c++)
                    yield return new Coord(r, c);
        }

        public static IEnumerable<Coord> QuadrantTiles(int seat)
        {
            int r0 = seat == 0 || seat == 1 ? 0 : QuadrantSize;
            int c0 = seat == 0 || seat == 3 ? 0 : QuadrantSize;
            for (int r = r0; r < r0 + QuadrantSize; r++)
                for (int c = c0; c < c0 + QuadrantSize; c++)
                    yield return new Coord(r, c);
        }

        /// <summary>Sol üst köşesi verilen h×w dikdörtgen; tahta dışı kareler dahil edilir, çağıran doğrular.</summary>
        public static List<Coord> Rect(Coord topLeft, int height, int width)
        {
            var list = new List<Coord>(height * width);
            for (int r = 0; r < height; r++)
                for (int c = 0; c < width; c++)
                    list.Add(new Coord(topLeft.Row + r, topLeft.Col + c));
            return list;
        }

        /// <summary>Merkezin çevresindeki (2r+1)×(2r+1) kare, tahtaya kırpılmış.</summary>
        public static List<Coord> Around(Coord center, int radius, bool includeCenter)
        {
            var list = new List<Coord>();
            for (int dr = -radius; dr <= radius; dr++)
                for (int dc = -radius; dc <= radius; dc++)
                {
                    if (!includeCenter && dr == 0 && dc == 0) continue;
                    var c = new Coord(center.Row + dr, center.Col + dc);
                    if (c.IsOnBoard) list.Add(c);
                }
            return list;
        }

        public static bool AllOnBoard(IEnumerable<Coord> tiles)
        {
            foreach (var t in tiles)
                if (!t.IsOnBoard) return false;
            return true;
        }

        /// <summary>Karelerin hepsi tek bir çeyrekteyse o çeyreği, değilse -1 döner.</summary>
        public static int CommonQuadrant(IEnumerable<Coord> tiles)
        {
            int q = -2;
            foreach (var t in tiles)
            {
                int tq = QuadrantOf(t);
                if (tq < 0) return -1;
                if (q == -2) q = tq;
                else if (q != tq) return -1;
            }
            return q < 0 ? -1 : q;
        }
    }
}
