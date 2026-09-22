using DortGok.Core;
using UnityEngine;

namespace DortGok.Data
{
    /// <summary>
    /// GDD Bölüm 5'teki veri tarafı: kural verisi (AreaEffectDef) ve yalnızca sunum katmanının okuduğu görsel.
    /// Varlıklar "Dört Gök > Veri > Alan Etkisi Varlıklarını Üret" menüsüyle çekirdek katalogdan üretilir.
    /// </summary>
    [CreateAssetMenu(menuName = "DortGok/Alan Etkisi", fileName = "AlanEtkisi")]
    public class AreaEffectAsset : ScriptableObject
    {
        public AreaEffectDef Def;
        public GameObject VfxPrefab;   // yalnızca sunum katmanı okur
        public Color TileColor = new Color(1f, 1f, 1f, 0.5f);
    }
}
