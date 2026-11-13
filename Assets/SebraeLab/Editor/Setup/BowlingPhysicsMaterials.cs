using UnityEditor;
using UnityEngine;

namespace SebraeLab.Editor
{
    /// <summary>
    /// Cria ou atualiza os materiais físicos do boliche em Assets/SebraeLab/Resources/Fisica.
    /// Os valores podem ser ajustados depois no Inspector; rodar de novo restaura os do spec.
    /// </summary>
    public static class BowlingPhysicsMaterials
    {
        const string Pai = "Assets/SebraeLab/Resources";
        const string Pasta = Pai + "/Fisica";
        const string Extensao = ".asset";

        [MenuItem("Sebrae Lab/Boliche/Criar materiais físicos")]
        public static void Criar()
        {
            if (!AssetDatabase.IsValidFolder(Pasta)) AssetDatabase.CreateFolder(Pai, "Fisica");
            Salvar("Pista", .05f, .05f, 0f, PhysicsMaterialCombine.Minimum);
            Salvar("Bola", .2f, .2f, 0f, PhysicsMaterialCombine.Average);
            Salvar("Pino", .4f, .4f, .3f, PhysicsMaterialCombine.Average);
            AssetDatabase.SaveAssets();
        }

        public static void CriarPelaLinhaDeComando() { Criar(); EditorApplication.Exit(0); }

        static void Salvar(string nome, float dinamico, float estatico, float quique, PhysicsMaterialCombine atrito)
        {
            var caminho = Pasta + "/" + nome + Extensao;
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(caminho);
            bool novo = !material;
            if (novo) material = new PhysicsMaterial(nome);
            material.dynamicFriction = dinamico;
            material.staticFriction = estatico;
            material.bounciness = quique;
            material.frictionCombine = atrito;
            material.bounceCombine = PhysicsMaterialCombine.Average;
            if (novo) AssetDatabase.CreateAsset(material, caminho);
            else EditorUtility.SetDirty(material);
            Debug.Log($"Material físico {caminho}: atrito {dinamico}/{estatico}, quique {quique}, combinação {atrito}");
        }
    }
}
