using UnityEditor;
using UnityEngine;

namespace SebraeLab.Editor
{
    /// <summary>Cria ou atualiza Assets/SebraeLab/Resources/Acabamentos/Escuro.mat com o shader SebraeLab/Escuro.</summary>
    public static class EscuroMaterial
    {
        const string Caminho = "Assets/SebraeLab/Resources/Acabamentos/Escuro.mat";

        [MenuItem("Sebrae Lab/Trilha/Criar material do escuro")]
        public static void Criar()
        {
            var shader = Shader.Find("SebraeLab/Escuro");
            if (!shader) throw new System.InvalidOperationException("Shader ausente: SebraeLab/Escuro");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Caminho);
            bool novo = !material;
            if (novo) material = new Material(shader);
            else material.shader = shader;
            material.SetFloat("_Alfa", 0f);
            if (novo) AssetDatabase.CreateAsset(material, Caminho);
            else EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log("Material do escuro: " + Caminho);
        }

        public static void CriarPelaLinhaDeComando()
        {
            try { Criar(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogError(e); EditorApplication.Exit(1); }
        }
    }
}
