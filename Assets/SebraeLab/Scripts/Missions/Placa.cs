using UnityEngine;

namespace SebraeLab.Missions
{
    /// <summary>
    /// Placa de texto da missão. Nesta versão, só o material do texto: shader com teste de profundidade
    /// (paredes escondem o texto) usando a textura da fonte.
    /// </summary>
    public class Placa : MonoBehaviour
    {
        static Material materialDoTexto;
        static Font fonteDoMaterial;

        /// <summary>Antes deste x a rota anda no eixo Z (praça e galeria); a partir dele, no eixo X.</summary>
        public const float LimiteDoTrecho = 15f;

        /// <summary>Direção de uma placa ou botão parado: de frente para o sentido em que se anda no trecho.</summary>
        public static float GuinadaPadrao(Vector3 posicao) => posicao.x < LimiteDoTrecho ? 0f : 90f;

        /// <summary>
        /// Material do texto, compartilhado por todas as placas. A fonte é dinâmica: o Unity pode recriar a textura
        /// ao incluir caracteres novos, e o material acompanha.
        /// </summary>
        public static Material MaterialDoTexto(Font fonte)
        {
            if (!materialDoTexto)
            {
                var modelo = Resources.Load<Material>("Acabamentos/PlacaTexto");
                if (!modelo) throw new System.InvalidOperationException("Material ausente: Acabamentos/PlacaTexto");
                materialDoTexto = new Material(modelo) { name = "PlacaTexto (instância)" };
                Font.textureRebuilt += AoRecriarTextura;
            }
            fonteDoMaterial = fonte;
            materialDoTexto.mainTexture = fonte.material.mainTexture;
            return materialDoTexto;
        }

        static void AoRecriarTextura(Font fonte)
        {
            if (materialDoTexto && fonte == fonteDoMaterial) materialDoTexto.mainTexture = fonte.material.mainTexture;
        }
    }
}
