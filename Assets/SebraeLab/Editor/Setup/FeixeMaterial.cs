using UnityEditor;
using UnityEngine;

namespace SebraeLab.Editor
{
    /// <summary>Cria ou atualiza Assets/SebraeLab/Resources/Acabamentos/Feixe.mat com o shader SebraeLab/Feixe.</summary>
    public static class FeixeMaterial
    {
        const string Caminho = "Assets/SebraeLab/Resources/Acabamentos/Feixe.mat";
        static readonly Color Dourado = new Color(1f, .68f, .14f); // o mesmo dourado da maçaneta e das molduras

        [MenuItem("Sebrae Lab/Trilha/Criar material do feixe")]
        public static void Criar()
        {
            var shader = Shader.Find("SebraeLab/Feixe");
            if (!shader) throw new System.InvalidOperationException("Shader ausente: SebraeLab/Feixe");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Caminho);
            bool novo = !material;
            if (novo) material = new Material(shader);
            else material.shader = shader;
            material.SetColor("_Cor", Dourado);
            material.SetFloat("_Intensidade", .55f);
            material.SetFloat("_Brilho", 1f);
            if (novo) AssetDatabase.CreateAsset(material, Caminho);
            else EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log("Material do feixe: " + Caminho);
        }

        public static void CriarPelaLinhaDeComando()
        {
            try { Criar(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogError(e); EditorApplication.Exit(1); }
        }
    }
}
