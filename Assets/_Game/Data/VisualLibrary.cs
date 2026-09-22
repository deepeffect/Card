using System.Collections.Generic;
using UnityEngine;

namespace DortGok.Data
{
    /// <summary>
    /// Sunum katmanının baktığı tek kütüphane. "Resources/DortGokVisuals" adıyla kaydedilirse
    /// BoardView gri kutu modelleri yerine buradaki modelleri kullanır.
    /// </summary>
    [CreateAssetMenu(menuName = "DortGok/Görsel Kütüphanesi", fileName = "DortGokVisuals")]
    public class VisualLibrary : ScriptableObject
    {
        public List<CardVisualAsset> Cards = new List<CardVisualAsset>();
        public List<AreaEffectAsset> Effects = new List<AreaEffectAsset>();

        public CardVisualAsset Card(string id)
        {
            foreach (var c in Cards)
                if (c != null && c.CardId == id) return c;
            return null;
        }

        public AreaEffectAsset Effect(string id)
        {
            foreach (var e in Effects)
                if (e != null && e.Def != null && e.Def.Id == id) return e;
            return null;
        }

        public static VisualLibrary Load() => Resources.Load<VisualLibrary>("DortGokVisuals");
    }
}
