using UnityEngine;

namespace SebraeLab.Missions
{
    /// <summary>
    /// Porta de vidro dos fundos do prédio. Fica trancada (com colisão) até <see cref="Abrir"/>, que tira a colisão e a
    /// desliza para oeste. Precisa estar sem a marca de estático na cena (TrilhaCena), senão o lote estático não a move.
    /// </summary>
    [DisallowMultipleComponent]
    public class PortaDeVidro : MonoBehaviour
    {
        public bool Aberta { get; private set; }
        Vector3 fechada;
        float tempo = -1f;

        void Awake() => fechada = transform.position;

        public void Abrir()
        {
            if (Aberta) return;
            Aberta = true;
            foreach (var colisor in GetComponentsInChildren<Collider>()) colisor.enabled = false;
            tempo = 0f;
        }

        void Update()
        {
            if (tempo < 0f) return;
            tempo += Time.deltaTime;
            float t = Mathf.Clamp01(tempo / PlantaDaTrilha.TempoDaPortaDeVidro);
            transform.position = fechada + Vector3.left * (PlantaDaTrilha.CursoDaPortaDeVidro * Mathf.SmoothStep(0f, 1f, t));
            if (t >= 1f) tempo = -1f;
        }
    }
}
