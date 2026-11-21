using System;
using UnityEngine;

namespace SebraeLab.Missions
{
    /// <summary>
    /// Seta dourada pequena sobre o controle esquerdo. Aparece só quando o feixe está fora da vista (mais de 50° do
    /// centro, no plano do chão) e aponta para ele, girando só no plano do chão.
    /// </summary>
    [DisallowMultipleComponent]
    public class SetaDoPulso : MonoBehaviour
    {
        /// <summary>Comprimento da seta, em metros.</summary>
        public const float Tamanho = .08f;
        public Transform cabeca;
        public Func<Vector3> alvo;
        public Transform Seta { get; private set; }

        public static SetaDoPulso Criar(Transform controle, Transform cabeca, Func<Vector3> alvo)
        {
            var seta = controle.gameObject.AddComponent<SetaDoPulso>();
            seta.cabeca = cabeca;
            seta.alvo = alvo;
            var raiz = new GameObject("Seta do pulso").transform;
            raiz.SetParent(controle, false);
            raiz.localPosition = new Vector3(0f, .06f, -.02f);
            // Ponta em V: duas hastes douradas que se encontram na ponta, em +Z da raiz.
            var ponta = new Vector3(0f, 0f, Tamanho * .375f);
            foreach (int lado in new[] { -1, 1 })
            {
                var haste = Placa.Lamina(raiz, "Haste", "PlacaMoldura");
                var direcao = Quaternion.Euler(0f, lado * 35f, 0f) * Vector3.forward;
                haste.localRotation = Quaternion.LookRotation(direcao);
                haste.localScale = new Vector3(.012f, .008f, Tamanho * .7f);
                haste.localPosition = ponta - direcao * (Tamanho * .35f);
            }
            seta.Seta = raiz;
            raiz.gameObject.SetActive(false);
            return seta;
        }

        void LateUpdate()
        {
            if (cabeca && alvo != null) Atualizar(cabeca.position, cabeca.forward, alvo());
        }

        public void Atualizar(Vector3 posicaoDaCabeca, Vector3 frenteDaCabeca, Vector3 posicaoDoAlvo)
        {
            bool mostrar = GuiaDaTrilha.SetaVisivel(posicaoDaCabeca, frenteDaCabeca, posicaoDoAlvo);
            if (Seta.gameObject.activeSelf != mostrar) Seta.gameObject.SetActive(mostrar);
            if (!mostrar) return;
            var direcao = posicaoDoAlvo - Seta.position;
            direcao.y = 0f;
            if (direcao.sqrMagnitude > 1e-4f) Seta.rotation = Quaternion.LookRotation(direcao.normalized, Vector3.up);
        }
    }
}
