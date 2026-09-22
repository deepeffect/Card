using UnityEngine;

namespace DortGok.Data
{
    /// <summary>Bir kartın tahtadaki görünümü: model ve renk. Boş bırakılırsa gri kutu modeli kullanılır.</summary>
    [CreateAssetMenu(menuName = "DortGok/Kart Görseli", fileName = "KartGorseli")]
    public class CardVisualAsset : ScriptableObject
    {
        public string CardId;
        public GameObject Prefab;
        public Color Tint = Color.white;
        /// <summary>Geliştirme kartları için: silah ya da zırh soketine takılan model.</summary>
        public GameObject SocketPrefab;
    }
}
