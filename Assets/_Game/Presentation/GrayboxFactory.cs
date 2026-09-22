using UnityEngine;
using UnityEngine.Rendering;

namespace DortGok.Presentation
{
    /// <summary>
    /// Gri kutu (Faz 1) modelleri: ilkel şekiller. Hem URP'de hem Built-in'de çalışır;
    /// renkler materyalin ana rengine (URP _BaseColor, Built-in _Color) yazılır.
    /// </summary>
    public static class GrayboxFactory
    {
        public static GameObject Primitive(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color color, string name)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            SetColor(go, color);
            return go;
        }

        public static void SetColor(GameObject go, Color color)
        {
            if (go == null) return;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material.color = color;
        }

        /// <summary>Işıktan etkilenmeyen materyal (tahta dokusu için); bulunamazsa varsayılan materyal.</summary>
        public static Material UnlitMaterial(Material fallback)
        {
            string shaderName = GraphicsSettings.currentRenderPipeline != null ? "Universal Render Pipeline/Unlit" : "Unlit/Texture";
            var shader = Shader.Find(shaderName);
            var mat = shader != null ? new Material(shader) : new Material(fallback);
            if (mat.HasProperty("_BaseColor") || mat.HasProperty("_Color")) mat.color = Color.white;
            return mat;
        }
    }
}
