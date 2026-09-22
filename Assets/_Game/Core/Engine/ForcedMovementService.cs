using System.Collections.Generic;

namespace DortGok.Core
{
    /// <summary>
    /// İtme, çekme, savurma, ışınlama (GDD Bölüm 5). Zorla hareket EN harcatmaz ama
    /// giriş tetikli zemin etkilerini tetikler. Uçan birimler yalnızca vardıkları karenin etkisini alır.
    /// </summary>
    public sealed class ForcedMovementService
    {
        private readonly RulesEngine _e;

        public ForcedMovementService(RulesEngine engine) { _e = engine; }

        private bool Blocked(Coord c) => !_e.Grid.IsEmpty(c);

        /// <summary>Birimi bir yönde en fazla "distance" kare iter. Engele çarparsa collisionDamage kadar hasar alır.</summary>
        public int Push(Unit u, Coord dir, int distance, DamageSource collisionSource, int collisionDamage)
        {
            int moved = 0;
            for (int i = 0; i < distance; i++)
            {
                if (u.IsDead || u.Hp <= 0) break;
                var next = u.Pos + dir;
                if (Blocked(next))
                {
                    if (collisionDamage > 0)
                    {
                        _e.Say($"{u.Def.Name} engele çarptı.", collisionSource.Player);
                        _e.Damage.DealToUnit(u, collisionDamage, collisionSource);
                    }
                    break;
                }
                Step(u, next);
                moved++;
            }
            FinishForFlyer(u, moved);
            return moved;
        }

        /// <summary>Birimi hedef kareye doğru çeker; hedef karenin kendisine giremez.</summary>
        public int PullToward(Unit u, Coord target, int distance)
        {
            int moved = 0;
            for (int i = 0; i < distance; i++)
            {
                if (u.IsDead || u.Hp <= 0) break;
                var dir = (target - u.Pos).Sign();
                if (dir.Row == 0 && dir.Col == 0) break;
                var next = u.Pos + dir;
                if (next == target || Blocked(next)) break;
                Step(u, next);
                moved++;
            }
            FinishForFlyer(u, moved);
            return moved;
        }

        /// <summary>Birimi "radius" kare içindeki rastgele boş bir kareye savurur (tohumlu RNG).</summary>
        public bool Scatter(Unit u, int radius, ICollection<Coord> excluded)
        {
            var candidates = new List<Coord>();
            foreach (var c in Board.Around(u.Pos, radius, false))
                if (_e.Grid.IsEmpty(c) && (excluded == null || !excluded.Contains(c))) candidates.Add(c);
            if (candidates.Count == 0) return false;
            var dest = candidates[_e.State.Rng.Next(candidates.Count)];
            Teleport(u, dest, "savruldu");
            return true;
        }

        /// <summary>Birimi doğrudan bir kareye taşır (Kayan Yıldız, Bulut Durağı, savrulma).</summary>
        public void Teleport(Unit u, Coord dest, string verb)
        {
            var from = u.Pos;
            _e.Grid.ClearOccupant(from, u.Id);
            u.Pos = dest;
            _e.Grid.SetOccupant(dest, u.Id);
            _e.Emit(new GameEvent { Type = GameEventType.UnitTeleported, Player = u.Owner, EntityId = u.Id, From = from, To = dest, Message = $"{u.Def.Name} {verb}: {from} → {dest}." });
            _e.Effects.RunOnEnter(u, dest);
        }

        /// <summary>Tek adımlık zorla hareket (Kara Delik çekimi); çağıran boş kareyi doğrular.</summary>
        public void StepUnit(Unit u, Coord next) => Step(u, next);

        /// <summary>Uçan birim zorla hareketin sonunda vardığı karenin etkisini alır.</summary>
        public void SettleFlyer(Unit u) => FinishForFlyer(u, 1);

        private void Step(Unit u, Coord next)
        {
            var from = u.Pos;
            _e.Grid.ClearOccupant(from, u.Id);
            u.Pos = next;
            _e.Grid.SetOccupant(next, u.Id);
            _e.Emit(new GameEvent { Type = GameEventType.UnitForcedMoved, Player = u.Owner, EntityId = u.Id, From = from, To = next });
            if (!u.Flying) _e.Effects.RunOnEnter(u, next);
        }

        private void FinishForFlyer(Unit u, int moved)
        {
            if (moved > 0 && u.Flying && !u.IsDead && u.Hp > 0) _e.Effects.RunOnEnter(u, u.Pos);
        }
    }
}
