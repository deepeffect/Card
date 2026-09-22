using System;
using DortGok.Core;

namespace DortGok.Net
{
    /// <summary>
    /// Komut gönderimi ve olay yayını (GDD Bölüm 5: Ağ ve test). İstemci komut gönderir, yetkili taraf
    /// çekirdeği çalıştırıp olayları yayınlar. Prototipte aynı ekranda sırayla oynanır (LocalTransport);
    /// online sürümde aynı arayüzün arkasına sunucu yetkili bir aktarım gelir.
    /// </summary>
    public interface ICommandTransport
    {
        /// <summary>Komutu yetkili kural motoruna iletir.</summary>
        CommandResult Submit(Command command);

        /// <summary>Kural motorunun yayınladığı olaylar.</summary>
        event Action<GameEvent> EventReceived;

        /// <summary>Yerel görünüm için oyun durumu (online sürümde sunucudan gelen kopya).</summary>
        GameState State { get; }
    }

    /// <summary>Hot-seat: komutlar doğrudan yerel kural motorunda çalışır.</summary>
    public sealed class LocalTransport : ICommandTransport, IDisposable
    {
        private readonly RulesEngine _engine;

        public LocalTransport(RulesEngine engine)
        {
            _engine = engine;
            _engine.EventRaised += Forward;
        }

        public event Action<GameEvent> EventReceived;

        public GameState State => _engine.State;

        public CommandResult Submit(Command command) => _engine.Execute(command);

        private void Forward(GameEvent e) => EventReceived?.Invoke(e);

        public void Dispose() => _engine.EventRaised -= Forward;
    }
}
