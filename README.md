# Dört Gök (çalışma adı)

4 oyunculu, 12×12 kareli holografik masada oynanan, Card Wars'tan ilham alan sıra tabanlı kart savaşı.
Tasarımın tek kaynağı GDD'dir: [Kart Savaşları: Dört Gök — Oyun Tasarım Dokümanı](https://claude.ai/code/artifact/3d168270-4983-4ad2-bcb1-7629b286ab34).
Bu depo GDD yol haritasının **Faz 0–2** çıktılarını ve **Faz 1 gri kutu** sunumunu içerir.

## Durum

| Yol haritası fazı | Durum | Kanıt |
| --- | --- | --- |
| 0. Kâğıt prototip | Kart sayfası hazır | `Docs/kagit-prototip-kartlari.html`: 20 çekirdek + 9 nötr + 16 ulti kartı, 12×12 tahta |
| 1. Gri kutu | Kod hazır, Unity'de açılmayı bekliyor | `Assets/_Game/Presentation`: hot-seat, botlar, küp modeller, IMGUI arayüz |
| 2. Kural motoru | Tamam | 4 fraksiyon, 29 kart, 16 ulti, alan etkileri, etkileşim tablosu, 63 test, basit bot |
| 2. Çıkış ölçütü | Tuttu | 1.000 bot-bot maçı **0 hatayla** bitiyor |

## Klasörler (GDD Bölüm 5)

```
Assets/_Game/
  Core/          saf C# kural motoru (asmdef: noEngineReferences)
    Model/         GameState, GridModel, Board, RulesConfig (GDD'deki tüm sayılar)
    Definitions/   CardCatalog, UltiCatalog, EffectCatalog, AreaEffectDef
    Engine/        RulesEngine, TurnManager, CardManager, EffectSystem, ForcedMovementService,
                   StatResolver, DamageSystem, UnitActionSystem, FloopSystem, UltiSystem
    AI/            SimpleBot (GDD Faz 2 "basit bot")
    Sim/           MatchRunner (bot-bot maçları, metrikler)
  Data/          ScriptableObject: AreaEffectAsset, CardVisualAsset, VisualLibrary
  Net/           ICommandTransport, LocalTransport (hot-seat; online için aynı arayüz)
  Presentation/  GameBootstrap, BoardView, HudView, PresentationQueue
  Editor/        "Dört Gök" menüsü
Assets/Tests/EditMode/   NUnit testleri (Unity Test Runner ve dotnet test ortak)
Tools/           Unity dışı derleme, test ve simülasyon projeleri
Docs/            Kâğıt prototip kartları
```

## Unity'de açmak

1. Unity Hub → **Add project from disk** → bu klasör. Unity 6 LTS (6000.x) seç.
2. İlk açılışta Unity `Packages/` ve `ProjectSettings/` dosyalarını üretir; `.meta` dosyalarını da depoya ekle.
3. Menü: **Dört Gök → URP'yi Kur ve Ayarla** (paket yoksa kurar; bitince bir kez daha çalıştır). Oyun URP olmadan Built-in hatta da çalışır.
4. Menü: **Dört Gök → Gri Kutu Sahnesini Oluştur** → Play. Boş bir sahnede Play'e basmak da oyunu otomatik kurar.
5. Testler: Window → General → Test Runner → EditMode → Run All. (Test Framework paketi yoksa Package Manager'dan ekle.)

**Kontroller:** kart seç → vurgulu kareye tıkla · birim seç → yeşil kareye tıkla (hareket) / kırmızıya tıkla (saldırı) ·
ulti seç → şekli tahtada gezdir, sarı geçerli / kırmızı geçersiz · Esc ya da sağ tık iptal · tekerlek yakınlaştırma.
Sol panelde her koltuk insan/bot arasında değiştirilebilir; varsayılan: Ay insan, diğerleri bot.

## Unity olmadan oynamak (.NET 10 test istemcisi)

Windows'ta, Unity kurmadan aynı kural motoruyla 2D oynamak için:

```
dotnet run --project Tools/Playtest
```

Varsayılan: Ay (sol üst) insan, diğer üç koltuk bot. Sağdaki "İnsan" kutularıyla her koltuğu insana çevirip
aynı ekranda sırayla oynayabilirsin. Kart seç → vurgulu kareye tıkla · birim seç → yeşil kare hareket, kırmızı kare
saldırı · ulti seç → fareyi tahtada gezdir (sarı geçerli, kırmızı geçersiz) · Esc ya da sağ tık iptal.
Kareye gelince altta etkiler, sahibi ve yol hasarı yazar. Tıklama ve hedefleme mantığı Unity HUD'u ile
ortaktır (`Core/Interaction/InteractionController`). Bu istemci yalnızca test içindir; asıl sunum Unity'dedir.

## Unity olmadan derleme, test ve simülasyon

.NET 10 SDK gerekir.

```
dotnet test Tools/Tests/DortGok.Tests.csproj                       # 63 kural testi
dotnet run --project Tools/Sim/DortGok.Sim.csproj -c Release -- sim 1000   # GDD Bölüm 6 metrikleri
dotnet run --project Tools/Sim/DortGok.Sim.csproj -c Release -- diag 300   # fraksiyon başına hasar kaynakları
dotnet run --project Tools/Sim/DortGok.Sim.csproj -c Release -- replay 3   # tek maçın olay günlüğü
dotnet run --project Tools/Sim/DortGok.Sim.csproj -c Release -- cards Docs/kagit-prototip-kartlari.html
```

## İlk denge ölçümü (1.000 bot-bot maçı, tohum 1–1000)

| Metrik | Ölçülen | GDD hedefi |
| --- | --- | --- |
| Hatalı maç | 0 | 0 (Faz 2 çıkış ölçütü) |
| Ay / Güneş / Bulut / Yıldız kazanma | %5,8 / %14,2 / %66,8 / %13,2 | %25 ± 3 |
| 1.–4. sıradaki oyuncunun kazanma | %17,6 / %23,7 / %22,8 / %35,9 | %25 ± 3 |
| Ortalama maç süresi | 24,8 raund | 10–14 raund |
| Karargah hasarında ultilerin payı | %1,1 | En fazla %30 |
| K3'lerin ilk kullanım raundu | 10,1 | 7–9 |
| 2 raundda 20+ karargah CAN'ı kaybı | %0,3 | En fazla %5 |

Bot basit olduğu için bu sayılar kesin denge yargısı değil, ama üç sinyal tutarlı:

- **Karargahlar pek düşmüyor.** Köşedeki 2×2 karargahın yalnızca 5 bitişik karesi var; savunanın birim ve yapıları bu kareleri kolayca kapatıyor. Maçlar çoğunlukla Gök Çöküşü ve deste bitimiyle bitiyor.
- **Şimşek Çarpması (K1) en verimli ulti.** 30 GE'ye 4 hasar, 1–3 AP'lik birimlerin çoğunu tek atışta siliyor ve her 2 turda bir geliyor. Bulut'un üstünlüğünün ana kaynağı bu.
- **4. sıradaki oyuncu avantajlı.** Uzun maçlarda Gök Çöküşü hasarını her raund en son o alıyor.

Bu sayıların hiçbiri değiştirilmedi. Dengeleme kararları GDD'ye aittir; `RulesConfig` tüm sayıları tek yerde tutar.

## GDD'de açık kalan noktalar ve seçilen yorum

| Konu | Seçilen yorum |
| --- | --- |
| 15 nötr kartın dağılımı | Şövalye 3, Daha İyi Kılıç 2, Çelik Zırh 2, Okçu Kulesi 2, Dinlenme Kampı 2, diğerleri 1'er |
| Konuşlanma: "kendi yapılarına bitişik" ile İleri Karakol metni çelişiyor | Tahta tablosundaki genel kural uygulandı; `RulesConfig.AllStructuresExtendDeployment = false` yalnızca Karakol'u bırakır |
| Alan etkileri kimi etkiler | Zararlı ve engelleyici etkiler yalnızca büyüyü yapanın rakiplerini etkiler; "dost ve düşman" yazan Nova Golemi ve sahipsiz Buhar hariç |
| Karargah Zırhı ve ultiler | Zırh ulti darbelerine de uygulanır (Şimşek 2 → 1, Meteor 3 → 2); deste bitimi ve Gök Çöküşü Zırh'tan etkilenmez |
| Gölge Örtüsü ve ultiler | Gölge altındaki birim tek hedefli ultilerle seçilemez; Süper Hücre'nin kendiliğinden vuruşu Gölge'yi aşar, Gizli'yi aşmaz |
| Elenen oyuncunun büyüleri | Süreleri onun turlarıyla sayıldığı için elendiğinde masadan kalkar |
| Tutulma | Yönetilen birimler EN harcamadan bir Hareket ve bir Saldırı yapar |
| Kıvılcım Koşucusu | Kor, çevresindeki 8 kareye düşer; feda "birim kaybı" sayılır (+6 GE) |
| Sunak | Sunak karelerine yapı kurulamaz; İllüzyonlar Sunak GE'si vermez |
| Hortum yönü | Büyüde seçilir, sonraki turlarda Hortum yönü komutuyla değiştirilebilir |
| Uçan birimler | Karargah ve Harabelerin de üzerinden geçer; Yıldız Geçidi onları da ışınlar |
| El sınırı | Oyuncu atacağı kartları seçer; seçmezse en son çekilenler atılır |
| Gök Yankısı "1 raund" | Kilit, her diğer oyuncunun bir sonraki turu bitene kadar sürer |

## Sıradaki adımlar (GDD yol haritası)

- Faz 0: Kâğıt kartlarla 5 tam maç oyna, maç süresini ölç. Karargah köşe sorunu masada da görülürse GDD'ye dön.
- Faz 3 (dikey kesit): holografik tahta shader'ı (katman maskeleri hazır), 1 fraksiyonun modelleri, VFX Graph ultileri.
- Özgün isim ve "Floop" yerine terim (öneri: Tetik) — Card Wars / Adventure Time fikri mülkiyeti.
