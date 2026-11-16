using UnityEditor;
using UnityEngine;

namespace SebraeLab.Editor
{
    /// <summary>
    /// Cria ou atualiza os materiais das placas e dos botões em Assets/SebraeLab/Resources/Acabamentos:
    /// PlacaTexto (shader SebraeLab/Placa Texto), PlacaCorpo e PlacaMoldura (Unlit/Color, sem iluminação).
    /// </summary>
    public static class PlacaMateriais
    {
        const string Pasta = "Assets/SebraeLab/Resources/Acabamentos";
        static readonly Color Azul = new Color(.04f, .13f, .22f);    // #0A2138, o mesmo fundo dos botões
        static readonly Color Dourado = new Color(1f, .68f, .14f);   // #FFAD24, a mesma borda dos botões

        [MenuItem("Sebrae Lab/Placas/Criar materiais")]
        public static void Criar()
        {
            Salvar("PlacaTexto", "SebraeLab/Placa Texto", Color.white);
            Salvar("PlacaCorpo", "Unlit/Color", Azul);
            Salvar("PlacaMoldura", "Unlit/Color", Dourado);
            AssetDatabase.SaveAssets();
        }

        public static void CriarPelaLinhaDeComando()
        {
            try { Criar(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogError(e); EditorApplication.Exit(1); }
        }

        static void Salvar(string nome, string nomeDoShader, Color cor)
        {
            var shader = Shader.Find(nomeDoShader);
            if (!shader) throw new System.InvalidOperationException("Shader ausente: " + nomeDoShader);
            var caminho = Pasta + "/" + nome + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            bool novo = !material;
            if (novo) material = new Material(shader);
            else material.shader = shader;
            material.color = cor;
            if (novo) AssetDatabase.CreateAsset(material, caminho);
            else EditorUtility.SetDirty(material);
            Debug.Log($"Material da placa {caminho}: shader {nomeDoShader}, cor #{ColorUtility.ToHtmlStringRGB(cor)}");
        }
    }
}
