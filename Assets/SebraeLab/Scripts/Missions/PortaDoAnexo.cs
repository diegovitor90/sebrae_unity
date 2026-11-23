using UnityEngine;

namespace SebraeLab.Missions
{
    /// <summary>
    /// Porta entre os fundos e o anexo. A folha não abre, porque atrás dela fica a parede: a maçaneta leva ao outro lado
    /// pela <see cref="Passagem"/>. Fica travada até o fim da trilha.
    /// </summary>
    public sealed class PortaDoAnexo : MonoBehaviour
    {
        Passagem passagem;

        public Vector3 Chegada { get; private set; }
        public float GuinadaDeChegada { get; private set; }
        /// <summary>Destravada por InternshipDay.LiberarDescompressao.</summary>
        public bool Liberada { get; set; }

        public void Configurar(Passagem passagem, Vector3 chegada, float guinadaDeChegada)
        {
            this.passagem = passagem;
            Chegada = chegada;
            GuinadaDeChegada = guinadaDeChegada;
        }

        /// <summary>Leva ao outro lado. Devolve false se a porta está travada ou se outra passagem está em andamento.</summary>
        public bool Usar() => Liberada && passagem && passagem.Atravessar(Chegada, GuinadaDeChegada);
    }
}
