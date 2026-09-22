using System;

namespace DortGok.Core
{
    /// <summary>Etkinin varsayılan şekli (GDD: tek kare, N×N kare, çizgi, çeyrek...).</summary>
    [Serializable]
    public struct ShapeDef
    {
        public ShapeKind Kind;
        public int Height;
        public int Width;

        public static ShapeDef Square(int n) => new ShapeDef { Kind = ShapeKind.Square, Height = n, Width = n };
        public static ShapeDef Line(int max) => new ShapeDef { Kind = ShapeKind.Line, Height = 1, Width = max };
        public static ShapeDef Quadrant() => new ShapeDef { Kind = ShapeKind.Quadrant, Height = 6, Width = 6 };
        public static ShapeDef Single() => new ShapeDef { Kind = ShapeKind.Single, Height = 1, Width = 1 };
        public static ShapeDef Pair() => new ShapeDef { Kind = ShapeKind.Pair, Height = 1, Width = 1 };
    }

    /// <summary>İstatistik değiştirici: saklanmaz, StatResolver her sorguda hesaplar.</summary>
    [Serializable]
    public struct StatModifier
    {
        public Stat Stat;
        public ModOp Op;
        public int Value;
        public Affects Affects;

        public static StatModifier Set(Stat s, int v, Affects a) => new StatModifier { Stat = s, Op = ModOp.Set, Value = v, Affects = a };
        public static StatModifier Add(Stat s, int v, Affects a) => new StatModifier { Stat = s, Op = ModOp.Add, Value = v, Affects = a };
    }

    /// <summary>"Tetik → eylem" kaydı. Düz yapıdadır ki Unity de serileştirebilsin.</summary>
    [Serializable]
    public struct TriggeredAction
    {
        public Trigger Trigger;
        public ActionKind Kind;
        public int UnitAmount;
        public int StructureAmount;
        public Affects Affects;
        public bool OncePerTurn;
    }

    /// <summary>
    /// GDD Bölüm 5'teki AreaEffectDef: katman, şekil, bölge, süre, etiketler,
    /// değiştiriciler ve tetik → eylem listesi. Yeni bir büyü çoğu zaman yeni bir veri kaydıdır.
    /// </summary>
    [Serializable]
    public class AreaEffectDef
    {
        public string Id;
        public string Name;
        public EffectLayer Layer;
        public ShapeDef Shape;
        public TargetZone Zone;
        public int DurationTurns;        // yapanın turlarıyla sayılır
        public EffectTag Tags;
        public StatModifier[] Modifiers = Array.Empty<StatModifier>();
        public TriggeredAction[] Actions = Array.Empty<TriggeredAction>();

        public bool Has(EffectTag tag) => (Tags & tag) != 0;
    }
}
