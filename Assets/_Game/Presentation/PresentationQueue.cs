using System;
using System.Collections.Generic;
using DortGok.Core;

namespace DortGok.Presentation
{
    /// <summary>
    /// Sunum Kuyruğu (GDD Bölüm 5): kural motorunun olaylarını sırayla oynatır. Motor animasyonu beklemez;
    /// kuyruk her olaya kısa bir süre tanıyarak hasar sayıları, parlamalar ve günlük satırlarını tempoyla gösterir.
    /// </summary>
    public sealed class PresentationQueue
    {
        private readonly Queue<GameEvent> _queue = new Queue<GameEvent>();
        private float _wait;

        public float Speed = 1f;

        /// <summary>Olay oynatıldığında (görünümler burada tepki verir).</summary>
        public event Action<GameEvent> Played;

        public bool Busy => _queue.Count > 0 || _wait > 0f;

        public int Pending => _queue.Count;

        public void Enqueue(GameEvent e) => _queue.Enqueue(e);

        public void Clear()
        {
            _queue.Clear();
            _wait = 0f;
        }

        public void Tick(float dt)
        {
            if (_wait > 0f)
            {
                _wait -= dt;
                if (_wait > 0f) return;
            }
            int guard = 0;
            while (_queue.Count > 0 && _wait <= 0f && guard++ < 256)
            {
                var e = _queue.Dequeue();
                Played?.Invoke(e);
                _wait = DurationOf(e) / Math.Max(0.1f, Speed);
            }
        }

        private static float DurationOf(GameEvent e)
        {
            switch (e.Type)
            {
                case GameEventType.TurnStarted: return 0.45f;
                case GameEventType.UltiCast: return 1.0f;
                case GameEventType.MeteorMarked: return 0.6f;
                case GameEventType.UnitMoved: return 0.10f * ((e.Tiles?.Count ?? 1) + 1);
                case GameEventType.UnitForcedMoved:
                case GameEventType.UnitTeleported: return 0.12f;
                case GameEventType.AttackPerformed: return 0.28f;
                case GameEventType.Damaged: return 0.14f;
                case GameEventType.EntityDestroyed: return 0.18f;
                case GameEventType.UnitSummoned:
                case GameEventType.StructureBuilt: return 0.18f;
                case GameEventType.PlayerEliminated: return 1.2f;
                case GameEventType.GameOver: return 0.5f;
                case GameEventType.FloopUsed: return 0.25f;
                case GameEventType.EffectAdded: return 0.2f;
                default: return 0f;
            }
        }
    }
}
