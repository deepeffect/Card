using System.Collections.Generic;
using System.Linq;
using DortGok.Core;
using DortGok.Data;
using UnityEngine;

namespace DortGok.Presentation
{
    /// <summary>
    /// Holografik masanın gri kutu sürümü. Tahta tek bir dokulu düzlemdir (GDD Bölüm 5: "144 ayrı parçacık
    /// sistemi yerine tek çizim"): her kare 16 piksel; Zemin etkileri kareyi doldurur, Hava etkileri çapraz
    /// şerit, Alan etkileri iç çerçeve olarak çizilir. Birim, yapı ve karargahlar ilkel şekillerdir.
    /// </summary>
    public sealed class BoardView
    {
        private const int Px = 16;
        private const int TexSize = Board.Size * Px;

        private readonly Transform _root;
        private readonly Texture2D _texture;
        private readonly Color32[] _pixels = new Color32[TexSize * TexSize];
        private readonly Color[] _tileColor = new Color[Board.Size * Board.Size];
        private readonly Dictionary<int, UnitView> _units = new Dictionary<int, UnitView>();
        private readonly Dictionary<int, StructureView> _structures = new Dictionary<int, StructureView>();
        private readonly GameObject[] _hqs = new GameObject[Board.Seats];
        private readonly VisualLibrary _library;

        /// <summary>Arayüzün her kare için verdiği vurgu (erişim, hedef, önizleme).</summary>
        public readonly Dictionary<Coord, Color> Overlay = new Dictionary<Coord, Color>();
        public Coord Hover = Coord.None;
        public int SelectedEntity = -1;
        /// <summary>İllüzyonların sahibine ipucu göstermek için o an ekrana bakan koltuk.</summary>
        public int Viewer = -1;

        public BoardView(Transform parent)
        {
            _library = VisualLibrary.Load();
            _root = new GameObject("Tahta").transform;
            _root.SetParent(parent, false);

            var table = GrayboxFactory.Primitive(PrimitiveType.Cube, _root, new Vector3(0f, -0.26f, 0f), new Vector3(15.5f, 0.5f, 15.5f), Palette.Table, "Masa");
            table.GetComponent<Renderer>().material.color = Palette.Table;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Hologram Tahta";
            Object.Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(_root, false);
            quad.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(Board.Size, Board.Size, 1f);
            _texture = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "TahtaDokusu"
            };
            var renderer = quad.GetComponent<Renderer>();
            var mat = GrayboxFactory.UnlitMaterial(renderer.sharedMaterial);
            mat.mainTexture = _texture;
            renderer.material = mat;
        }

        /// <summary>Yeni maçta eski görünümleri temizler.</summary>
        public void Clear()
        {
            foreach (var v in _units.Values) v.Destroy();
            foreach (var v in _structures.Values) v.Destroy();
            _units.Clear();
            _structures.Clear();
            for (int i = 0; i < _hqs.Length; i++)
            {
                if (_hqs[i] != null) Object.Destroy(_hqs[i]);
                _hqs[i] = null;
            }
            Overlay.Clear();
            SelectedEntity = -1;
        }

        public Vector3 WorldOfEntity(GameState s, int id)
        {
            if (_units.TryGetValue(id, out var uv) && uv.Root != null) return uv.Root.transform.position;
            if (_structures.TryGetValue(id, out var sv) && sv.Root != null) return sv.Root.transform.position;
            foreach (var hq in s.Hqs)
                if (hq != null && hq.Id == id) return BoardSpace.CenterOf(hq.Tiles, 1f);
            return Vector3.zero;
        }

        public void Sync(RulesEngine engine, float dt)
        {
            if (engine == null) return;
            var s = engine.State;
            SyncHqs(s);
            SyncStructures(s);
            SyncUnits(engine, dt);
            PaintBoard(engine);
        }

        // ---------------- Karargahlar ----------------

        private void SyncHqs(GameState s)
        {
            for (int seat = 0; seat < Board.Seats; seat++)
            {
                var hq = s.Hqs[seat];
                if (hq == null) continue;
                if (_hqs[seat] == null)
                {
                    _hqs[seat] = GrayboxFactory.Primitive(PrimitiveType.Cube, _root, BoardSpace.CenterOf(hq.Tiles), Vector3.one, Color.white, "Karargah " + seat);
                }
                var go = _hqs[seat];
                float h = hq.IsDead ? 0.15f : 0.35f + 1.25f * Mathf.Clamp01(hq.Hp / (float)hq.MaxHp);
                var target = new Vector3(1.8f, h, 1.8f);
                go.transform.localScale = Vector3.Lerp(go.transform.localScale, target, 0.2f);
                go.transform.localPosition = BoardSpace.CenterOf(hq.Tiles, go.transform.localScale.y * 0.5f);
                var color = hq.IsDead ? Palette.Ruin : Palette.Dark(Palette.Faction(s.Players[seat].Faction), hq.LastStandActive ? 0.95f : 0.7f);
                GrayboxFactory.SetColor(go, color);
            }
        }

        // ---------------- Yapılar ----------------

        private void SyncStructures(GameState s)
        {
            foreach (var id in _structures.Keys.Where(k => !s.Structures.ContainsKey(k)).ToList())
            {
                _structures[id].Destroy();
                _structures.Remove(id);
            }
            foreach (var st in s.Structures.Values)
            {
                if (!_structures.TryGetValue(st.Id, out var view))
                {
                    view = StructureView.Create(_root, st, s.Players[st.Owner].Faction, _library);
                    _structures[st.Id] = view;
                }
                view.Update(st, SelectedEntity == st.Id);
            }
        }

        // ---------------- Birimler ----------------

        private void SyncUnits(RulesEngine engine, float dt)
        {
            var s = engine.State;
            foreach (var id in _units.Keys.Where(k => !s.Units.ContainsKey(k)).ToList())
            {
                _units[id].Destroy();
                _units.Remove(id);
            }
            foreach (var u in s.Units.Values)
            {
                if (!_units.TryGetValue(u.Id, out var view))
                {
                    view = UnitView.Create(_root, u, _library);
                    _units[u.Id] = view;
                }
                view.Update(engine, u, dt, SelectedEntity == u.Id, Viewer);
            }
        }

        // ---------------- Doku ----------------

        private void PaintBoard(RulesEngine engine)
        {
            var s = engine.State;
            float t = Time.time;
            var meteorTiles = new HashSet<Coord>();
            foreach (var d in s.Delayed)
                foreach (var tile in d.Tiles) meteorTiles.Add(tile);

            for (int r = 0; r < Board.Size; r++)
            {
                for (int c = 0; c < Board.Size; c++)
                {
                    var co = new Coord(r, c);
                    int q = Board.QuadrantOf(co);
                    var faction = s.Players[q] != null ? s.Players[q].Faction : Faction.Notr;
                    bool alt = ((r + c) & 1) == 0;
                    var col = Color.Lerp(Palette.Table, Palette.Faction(faction), alt ? 0.30f : 0.24f);
                    if (s.Players[q] != null && !s.Players[q].Alive) col = Color.Lerp(col, Palette.Ruin, 0.6f);
                    if (Board.IsAltar(co)) col = Color.Lerp(col, Palette.Altar, 0.75f);
                    if (engine.Grid.IsRuin(co)) col = Palette.Ruin;
                    else if (engine.Grid.IsHqTile(co)) col = Palette.Dark(Palette.Faction(faction), 0.5f);
                    _tileColor[r * Board.Size + c] = col;
                }
            }

            // Zemin: dolgu
            foreach (var fx in s.Effects.Values)
            {
                if (fx.Def.Layer != EffectLayer.Zemin) continue;
                var ec = EffectColor(fx.Def.Id);
                float w = Palette.EffectWeight(fx.Def.Id);
                if (fx.Def.Id == EffectCatalog.Yanan || fx.Def.Id == EffectCatalog.Kor) w += 0.12f * Mathf.Sin(t * 7f + fx.Id);
                foreach (var tile in fx.Tiles)
                {
                    int i = tile.Row * Board.Size + tile.Col;
                    _tileColor[i] = Color.Lerp(_tileColor[i], ec, w);
                }
            }
            foreach (var tile in meteorTiles)
            {
                int i = tile.Row * Board.Size + tile.Col;
                _tileColor[i] = Color.Lerp(_tileColor[i], Palette.Meteor, 0.45f + 0.2f * Mathf.Sin(t * 6f));
            }
            foreach (var kv in Overlay)
            {
                if (!kv.Key.IsOnBoard) continue;
                int i = kv.Key.Row * Board.Size + kv.Key.Col;
                _tileColor[i] = Color.Lerp(_tileColor[i], kv.Value, 0.55f);
            }
            if (Hover.IsOnBoard)
            {
                int i = Hover.Row * Board.Size + Hover.Col;
                _tileColor[i] = Color.Lerp(_tileColor[i], Color.white, 0.18f);
            }

            // Pikseller: kare dolgusu + ızgara çizgisi
            for (int r = 0; r < Board.Size; r++)
            {
                for (int c = 0; c < Board.Size; c++)
                {
                    Color32 fill = _tileColor[r * Board.Size + c];
                    int py0 = (Board.Size - 1 - r) * Px;
                    int px0 = c * Px;
                    for (int y = 0; y < Px; y++)
                    {
                        int row = (py0 + y) * TexSize + px0;
                        for (int x = 0; x < Px; x++)
                            _pixels[row + x] = (x == 0 || y == 0) ? (Color32)Palette.GridLine : fill;
                    }
                }
            }

            // Hava: çapraz şerit; Alan: iç çerçeve
            foreach (var fx in s.Effects.Values)
            {
                if (fx.Def.Layer == EffectLayer.Zemin) continue;
                Color32 ec = EffectColor(fx.Def.Id);
                foreach (var tile in fx.Tiles)
                {
                    int py0 = (Board.Size - 1 - tile.Row) * Px;
                    int px0 = tile.Col * Px;
                    for (int y = 1; y < Px; y++)
                    {
                        for (int x = 1; x < Px; x++)
                        {
                            bool paint = fx.Def.Layer == EffectLayer.Hava
                                ? ((x + y + (int)(t * 6f)) % 6) < 2
                                : (x <= 2 || y <= 2 || x >= Px - 2 || y >= Px - 2);
                            if (paint) _pixels[(py0 + y) * TexSize + px0 + x] = ec;
                        }
                    }
                }
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
        }

        private Color EffectColor(string id)
        {
            var asset = _library != null ? _library.Effect(id) : null;
            return asset != null ? asset.TileColor : Palette.Effect(id);
        }
    }

    /// <summary>Birimin gri kutu modeli: gövde, halka, silah ve zırh soketleri, durum işaretleri.</summary>
    internal sealed class UnitView
    {
        public GameObject Root;
        private GameObject _body;
        private GameObject _ring;
        private GameObject _weaponSocket;
        private GameObject _armorSocket;
        private GameObject _weapon;
        private GameObject _armor;
        private GameObject _shield;
        private GameObject _marker;
        private string _weaponId = "";
        private string _armorId = "";
        private Color _baseColor;
        private Color _accent;
        private float _bodyHeight;

        public static UnitView Create(Transform parent, Unit u, VisualLibrary lib)
        {
            var v = new UnitView();
            v.Root = new GameObject(u.Def.Name + " #" + u.Id);
            v.Root.transform.SetParent(parent, false);
            v.Root.transform.localPosition = BoardSpace.ToWorld(u.Pos);
            float size = u.CardId == CardCatalog.KumulusDevi || u.CardId == CardCatalog.NovaGolemi ? 1.25f : u.IsToken ? 1.35f : 1f;
            v._bodyHeight = 0.42f * size;
            v._ring = GrayboxFactory.Primitive(PrimitiveType.Cylinder, v.Root.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.82f, 0.02f, 0.82f), Color.white, "Halka");
            var custom = lib != null ? lib.Card(u.CardId) : null;
            if (custom != null && custom.Prefab != null)
            {
                v._body = Object.Instantiate(custom.Prefab, v.Root.transform);
                v._body.transform.localPosition = new Vector3(0f, 0f, 0f);
            }
            else
            {
                var type = u.CardId == CardCatalog.NovaGolemi ? PrimitiveType.Cube : PrimitiveType.Capsule;
                v._body = GrayboxFactory.Primitive(type, v.Root.transform, new Vector3(0f, v._bodyHeight, 0f), new Vector3(0.42f * size, v._bodyHeight, 0.42f * size), Color.white, "Gövde");
            }
            v._weaponSocket = new GameObject("WeaponSocket");
            v._weaponSocket.transform.SetParent(v.Root.transform, false);
            v._weaponSocket.transform.localPosition = new Vector3(0.28f * size, v._bodyHeight * 1.05f, -0.05f);
            v._armorSocket = new GameObject("ArmorSocket");
            v._armorSocket.transform.SetParent(v.Root.transform, false);
            v._armorSocket.transform.localPosition = new Vector3(0f, v._bodyHeight * 1.1f, 0f);
            return v;
        }

        public void Update(RulesEngine engine, Unit u, float dt, bool selected, int viewer)
        {
            if (Root == null) return;
            var s = engine.State;
            var faction = s.Players[u.Owner].Faction;
            _baseColor = Palette.Faction(faction);
            _accent = Palette.FactionAccent(faction);
            if (u.CardId == CardCatalog.AnkaKusu) _baseColor = new Color(1f, 0.45f, 0.1f);

            float hover = u.Flying ? 0.45f + 0.06f * Mathf.Sin(Time.time * 3f + u.Id) : 0f;
            var target = BoardSpace.ToWorld(u.Pos, hover);
            Root.transform.localPosition = Vector3.Lerp(Root.transform.localPosition, target, 1f - Mathf.Exp(-dt * 10f));

            var body = _baseColor;
            if (u.Has(StatusKind.Hidden)) body = Color.Lerp(body, Color.black, 0.65f);
            if (u.Has(StatusKind.Blinded)) body = Color.Lerp(body, Color.gray, 0.6f);
            if (selected) body = Color.Lerp(body, Color.white, 0.35f + 0.15f * Mathf.Sin(Time.time * 6f));
            GrayboxFactory.SetColor(_body, body);

            var ringColor = u.ControlledBy >= 0 ? Palette.Faction(s.Players[u.ControlledBy].Faction) : _accent;
            if (engine.IsShadowed(u)) ringColor = Palette.Dark(ringColor, 0.4f);
            if (u.IsIllusion && viewer == u.Owner) ringColor = Color.Lerp(ringColor, Color.magenta, 0.5f);
            GrayboxFactory.SetColor(_ring, ringColor);

            SyncWeapon(u);
            SyncArmor(u);
            SyncMarkers(u);
        }

        /// <summary>Geliştirme kartı takılınca soketteki model değişir (GDD: "Daha İyi Kılıç" görsel olarak değişir).</summary>
        private void SyncWeapon(Unit u)
        {
            string id = u.WeaponCardId ?? "";
            if (id == _weaponId && _weapon != null) return;
            _weaponId = id;
            if (_weapon != null) Object.Destroy(_weapon);
            Vector3 scale;
            Color color;
            PrimitiveType type = PrimitiveType.Cube;
            switch (id)
            {
                case CardCatalog.DahaIyiKilic: scale = new Vector3(0.07f, 0.62f, 0.07f); color = new Color(1f, 0.97f, 0.75f); break;
                case CardCatalog.GunesKilici: scale = new Vector3(0.08f, 0.58f, 0.08f); color = new Color(1f, 0.5f, 0.1f); break;
                case CardCatalog.HilalHancer: scale = new Vector3(0.05f, 0.32f, 0.12f); color = new Color(0.85f, 0.85f, 1f); break;
                case CardCatalog.FirtinaMizragi: scale = new Vector3(0.04f, 0.9f, 0.04f); color = new Color(0.35f, 0.95f, 1f); type = PrimitiveType.Cylinder; break;
                case CardCatalog.GalaksiAsasi: scale = new Vector3(0.05f, 0.4f, 0.05f); color = new Color(0.65f, 0.4f, 1f); type = PrimitiveType.Cylinder; break;
                default: scale = new Vector3(0.05f, 0.4f, 0.05f); color = new Color(0.6f, 0.6f, 0.62f); break;
            }
            _weapon = GrayboxFactory.Primitive(type, _weaponSocket.transform, new Vector3(0f, scale.y * (type == PrimitiveType.Cylinder ? 1f : 0.5f), 0f), scale, color, "Silah");
            if (id == CardCatalog.GalaksiAsasi)
                GrayboxFactory.Primitive(PrimitiveType.Sphere, _weapon.transform, new Vector3(0f, 1.1f, 0f), new Vector3(3f, 0.35f, 3f), new Color(0.9f, 0.6f, 1f), "Galaksi");
        }

        private void SyncArmor(Unit u)
        {
            string id = u.ArmorCardId ?? "";
            if (id == _armorId) return;
            _armorId = id;
            if (_armor != null) Object.Destroy(_armor);
            _armor = null;
            if (id == CardCatalog.CelikZirh)
                _armor = GrayboxFactory.Primitive(PrimitiveType.Cube, _armorSocket.transform, Vector3.zero, new Vector3(0.5f, 0.28f, 0.5f), new Color(0.55f, 0.58f, 0.62f), "Zırh Plakaları");
        }

        private void SyncMarkers(Unit u)
        {
            bool defending = u.Has(StatusKind.Defending);
            if (defending && _shield == null)
                _shield = GrayboxFactory.Primitive(PrimitiveType.Cube, Root.transform, new Vector3(0f, _bodyHeight, -0.3f), new Vector3(0.38f, 0.38f, 0.05f), _accent, "Kalkan");
            else if (!defending && _shield != null)
            {
                Object.Destroy(_shield);
                _shield = null;
            }

            Color? marker = null;
            if (u.Has(StatusKind.Marked)) marker = Palette.Target;
            else if (u.Has(StatusKind.Burning)) marker = new Color(1f, 0.5f, 0f);
            else if (u.ControlledBy >= 0) marker = Color.magenta;
            if (marker.HasValue && _marker == null)
                _marker = GrayboxFactory.Primitive(PrimitiveType.Sphere, Root.transform, new Vector3(0f, _bodyHeight * 2.4f, 0f), Vector3.one * 0.16f, marker.Value, "İşaret");
            if (_marker != null)
            {
                if (!marker.HasValue)
                {
                    Object.Destroy(_marker);
                    _marker = null;
                }
                else GrayboxFactory.SetColor(_marker, marker.Value);
            }
        }

        public void Destroy()
        {
            if (Root != null) Object.Destroy(Root);
            Root = null;
        }
    }

    /// <summary>Yapının gri kutu modeli; kuleler uzun, Gök Kalkanı kubbe, Paratoner direk.</summary>
    internal sealed class StructureView
    {
        public GameObject Root;
        private GameObject _body;
        private Color _color;

        public static StructureView Create(Transform parent, Structure s, Faction owner, VisualLibrary lib)
        {
            var v = new StructureView();
            v.Root = new GameObject(s.Def.Name + " #" + s.Id);
            v.Root.transform.SetParent(parent, false);
            v.Root.transform.localPosition = BoardSpace.ToWorld(s.Pos);
            v._color = Color.Lerp(Palette.Faction(owner), new Color(0.75f, 0.75f, 0.78f), 0.35f);
            var custom = lib != null ? lib.Card(s.CardId) : null;
            if (custom != null && custom.Prefab != null)
            {
                v._body = Object.Instantiate(custom.Prefab, v.Root.transform);
                return v;
            }
            switch (s.Def.Passive)
            {
                case CardPassive.OkcuKulesi:
                    v._body = GrayboxFactory.Primitive(PrimitiveType.Cube, v.Root.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.55f, 1.2f, 0.55f), v._color, "Kule");
                    break;
                case CardPassive.GokKalkani:
                    v._body = GrayboxFactory.Primitive(PrimitiveType.Sphere, v.Root.transform, new Vector3(0f, 0.15f, 0f), new Vector3(0.9f, 0.55f, 0.9f), v._color, "Kalkan");
                    break;
                case CardPassive.Paratoner:
                    v._body = GrayboxFactory.Primitive(PrimitiveType.Cylinder, v.Root.transform, new Vector3(0f, 0.75f, 0f), new Vector3(0.12f, 0.75f, 0.12f), v._color, "Paratoner");
                    break;
                case CardPassive.KaragahSurlari:
                    v._body = GrayboxFactory.Primitive(PrimitiveType.Cube, v.Root.transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.9f, 0.6f, 0.35f), v._color, "Sur");
                    break;
                default:
                    float h = s.CardId == CardCatalog.KizginMancinik ? 0.8f : 0.5f;
                    v._body = GrayboxFactory.Primitive(PrimitiveType.Cube, v.Root.transform, new Vector3(0f, h * 0.5f, 0f), new Vector3(0.7f, h, 0.7f), v._color, "Yapı");
                    break;
            }
            return v;
        }

        public void Update(Structure s, bool selected)
        {
            if (_body == null) return;
            float hp = Mathf.Clamp01(s.Hp / (float)Mathf.Max(1, s.MaxHp));
            var c = Color.Lerp(Palette.Dark(_color, 0.5f), _color, hp);
            if (selected) c = Color.Lerp(c, Color.white, 0.35f + 0.15f * Mathf.Sin(Time.time * 6f));
            GrayboxFactory.SetColor(_body, c);
        }

        public void Destroy()
        {
            if (Root != null) Object.Destroy(Root);
            Root = null;
        }
    }
}
