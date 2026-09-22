using System;
using DortGok.Core;
using DortGok.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DortGok.Presentation
{
    /// <summary>
    /// Gri kutu (GDD yol haritası Faz 1) giriş noktası: kural motorunu, yerel aktarımı, sunum kuyruğunu,
    /// tahtayı ve arayüzü kurar. Aynı ekranda sırayla oynanır; insan olmayan koltuklara bot oturur.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Maç")]
        [Tooltip("0 = her maçta rastgele tohum")]
        public int Seed = 0;
        [Tooltip("Opsiyonel 2v2: çapraz oyuncular takım (Ay + Bulut, Güneş + Yıldız)")]
        public bool TeamMode = false;

        [Header("Koltuklar: işaretli = insan (0 Ay, 1 Güneş, 2 Bulut, 3 Yıldız)")]
        public bool[] HumanSeats = { true, false, false, false };

        [Header("Sunum")]
        public float EventSpeed = 1.5f;
        public float BotThinkDelay = 0.35f;
        [Tooltip("GDD online kuralı: tur süresi 90 sn dolunca tur biter")]
        public bool TurnTimer = false;

        /// <summary>Boş sahnede Play'e basınca oyunu otomatik kurar.</summary>
        public static bool AutoStartInEmptyScenes = true;

        public RulesEngine Engine { get; private set; }
        public ICommandTransport Transport { get; private set; }
        public PresentationQueue Queue { get; private set; }
        public BoardView Board { get; private set; }
        public HudView Hud { get; private set; }
        public float TurnTimeLeft { get; private set; }
        public int CurrentSeed { get; private set; }

        public event Action MatchStarted;

        private SimpleBot[] _bots;
        private float _botTimer;
        private int _lastTurnIndex = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            if (!AutoStartInEmptyScenes) return;
            if (FindFirstObjectByType<GameBootstrap>() != null) return;
            if (SceneManager.GetActiveScene().rootCount > 3) return;
            new GameObject("Dört Gök").AddComponent<GameBootstrap>();
        }

        private void Awake()
        {
            SetupCameraAndLight();
            Queue = new PresentationQueue();
            Board = new BoardView(transform);
            Hud = GetComponent<HudView>();
            if (Hud == null) Hud = gameObject.AddComponent<HudView>();
            Hud.Bind(this);
        }

        private void Start() => NewMatch(Seed);

        public void NewMatch(int seed)
        {
            if (Transport is IDisposable old) old.Dispose();
            Queue.Clear();
            Board.Clear();
            CurrentSeed = seed != 0 ? seed : UnityEngine.Random.Range(1, int.MaxValue);
            var setup = new GameSetup { Seed = CurrentSeed, Rules = new RulesConfig { TeamMode = TeamMode } };
            Engine = RulesEngine.CreateMatch(setup, Queue.Enqueue);
            Transport = new LocalTransport(Engine);
            _bots = new SimpleBot[Core.Board.Seats];
            for (int i = 0; i < _bots.Length; i++) _bots[i] = new SimpleBot(Engine, i);
            _lastTurnIndex = -1;
            MatchStarted?.Invoke();
        }

        public bool IsHuman(int seat) => HumanSeats != null && seat >= 0 && seat < HumanSeats.Length && HumanSeats[seat];

        public void SetHuman(int seat, bool human)
        {
            if (HumanSeats == null || HumanSeats.Length < 4) HumanSeats = new bool[4];
            HumanSeats[seat] = human;
        }

        /// <summary>Arayüzden gelen komutlar: sıradaki oyuncu adına aktarıma gider.</summary>
        public CommandResult Submit(Command command)
        {
            if (Engine == null) return CommandResult.Fail("Maç yok.");
            command.Player = Engine.State.Current;
            return Transport.Submit(command);
        }

        private void Update()
        {
            if (Engine == null) return;
            float dt = Time.deltaTime;
            Queue.Speed = EventSpeed;
            Queue.Tick(dt);

            var s = Engine.State;
            if (s.TurnIndex != _lastTurnIndex)
            {
                _lastTurnIndex = s.TurnIndex;
                TurnTimeLeft = Engine.Rules.TurnSeconds;
                _botTimer = BotThinkDelay;
            }
            if (s.IsOver) return;

            if (IsHuman(s.Current))
            {
                if (TurnTimer && s.Phase == Phase.AnaFaz)
                {
                    TurnTimeLeft -= dt;
                    if (TurnTimeLeft <= 0f) Submit(new EndTurnCommand());
                }
            }
            else if (!Queue.Busy)
            {
                _botTimer -= dt;
                if (_botTimer <= 0f) _bots[s.Current].PlayTurn();
            }
        }

        private void LateUpdate()
        {
            if (Engine == null) return;
            Hud.FillOverlay(Board);
            Board.Sync(Engine, Time.deltaTime);
        }

        private void SetupCameraAndLight()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.transform.position = new Vector3(0f, 13.5f, -10.5f);
            cam.transform.LookAt(new Vector3(0f, 0f, 0.6f));
            cam.fieldOfView = 50f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.06f);
            if (FindFirstObjectByType<Light>() == null)
            {
                var light = new GameObject("Işık").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            }
        }
    }

}
