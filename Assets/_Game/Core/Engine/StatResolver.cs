using System;

namespace DortGok.Core
{
    /// <summary>
    /// Nihai istatistik = temel + geliştirme + aura + zemin etkisi (GDD Bölüm 5).
    /// Değiştiriciler birime yazılmaz; her sorguda hesaplanır, etki kalkınca değer kendiliğinden düzelir.
    /// </summary>
    public sealed class StatResolver
    {
        private readonly RulesEngine _e;

        public StatResolver(RulesEngine engine) { _e = engine; }

        public int Get(Unit u, Stat stat)
        {
            switch (stat)
            {
                case Stat.Sal: return Atk(u);
                case Stat.Hiz: return Speed(u);
                case Stat.Mnz: return Range(u);
                case Stat.Zirh: return Armor(u);
                default: return 0;
            }
        }

        public int Atk(Unit u)
        {
            if (u.IsIllusion) return 0;
            int v = u.BaseAtk + Upgrade(u, d => d.AtkBonus) + PlayerAdd(u.Owner, Stat.Sal);
            return Math.Max(0, v);
        }

        public int MaxHp(Unit u) => u.BaseMaxHp + Upgrade(u, d => d.HpBonus);

        public int Armor(Unit u)
        {
            int v = Upgrade(u, d => d.ArmorBonus) + u.SumValue(StatusKind.GuardArmor);
            if (u.Has(StatusKind.Defending)) v += _e.Rules.DefendArmor;
            return v;
        }

        /// <summary>HIZ: eklemeler, sonra "en az 1", sonra zemin sabitlemesi (Çekim 0, Süper Hücre 1).</summary>
        public int Speed(Unit u)
        {
            int v = u.BaseSpeed + PlayerAdd(u.Owner, Stat.Hiz);
            if (u.Has(StatusKind.Charge)) v += 1;
            if (v < 1) v = 1;
            int set = TileSet(u, Stat.Hiz);
            if (set >= 0 && set < v) v = set;
            return v;
        }

        /// <summary>MNZ: eklemeler, "en az 1", sonra Sis / Süper Hücre gibi zemin sabitlemeleri.</summary>
        public int Range(Unit u)
        {
            int v = u.BaseRange + Upgrade(u, d => d.RangeBonus) + PlayerAdd(u.Owner, Stat.Mnz);
            if (v < 1) v = 1;
            int set = TileSet(u, Stat.Mnz);
            if (set >= 0 && set < v) v = set;
            return v;
        }

        /// <summary>Yapıların (kulelerin) menzili; Gece Yarısı "kulelerin MNZ'si −1" etkisini içerir.</summary>
        public int StructureRange(Structure s)
        {
            if (s.Def.Range <= 0) return 0;
            return Math.Max(1, s.Def.Range + PlayerAdd(s.Owner, Stat.YapiMnz));
        }

        public int HqArmor(int seat)
        {
            int v = _e.Rules.HqArmor;
            foreach (var s in _e.State.Structures.Values)
                if (s.Owner == seat && s.Def.Passive == CardPassive.KaragahSurlari) v++;
            return v;
        }

        private static int Upgrade(Unit u, Func<CardDef, int> pick)
        {
            int v = 0;
            if (u.WeaponCardId != null) v += pick(CardCatalog.Get(u.WeaponCardId));
            if (u.ArmorCardId != null) v += pick(CardCatalog.Get(u.ArmorCardId));
            return v;
        }

        public bool HasUpgradePassive(Unit u, CardPassive passive)
        {
            if (u.WeaponCardId != null && CardCatalog.Get(u.WeaponCardId).Passive == passive) return true;
            if (u.ArmorCardId != null && CardCatalog.Get(u.ArmorCardId).Passive == passive) return true;
            return false;
        }

        private int PlayerAdd(int seat, Stat stat)
        {
            int v = 0;
            foreach (var pe in _e.State.PlayerEffects)
            {
                if (pe.TargetPlayer != seat) continue;
                foreach (var m in pe.Modifiers)
                    if (m.Stat == stat && m.Op == ModOp.Add) v += m.Value;
            }
            return v;
        }

        /// <summary>Birimin karesindeki "Set" değiştiricilerinin en düşüğü; yoksa -1.</summary>
        private int TileSet(Unit u, Stat stat)
        {
            int best = -1;
            foreach (var fx in _e.Effects.At(u.Pos))
            {
                foreach (var m in fx.Def.Modifiers)
                {
                    if (m.Stat != stat || m.Op != ModOp.Set) continue;
                    if (!_e.AffectsOwner(m.Affects, fx.Caster, u.Owner)) continue;
                    if (best < 0 || m.Value < best) best = m.Value;
                }
            }
            return best;
        }
    }
}
