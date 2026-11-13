using UnityEngine;

namespace SebraeLab.Missions
{
    /// <summary>Conversão da puxada da corda (metros) em velocidade da flecha (m/s). Sem dependência de cena.</summary>
    public static class ArcheryDraw
    {
        public const float PuxadaMaxima = .5f;
        public const float PuxadaMinima = .1f;
        public const float VelocidadeMinima = 15f;
        public const float VelocidadeMaxima = 40f;

        public static float Limitar(float puxada) => Mathf.Clamp(puxada, 0f, PuxadaMaxima);

        public static bool Dispara(float puxada) => puxada >= PuxadaMinima;

        /// <summary>0 abaixo da puxada mínima; depois, interpolação linear de 15 a 40 m/s até a puxada máxima.</summary>
        public static float Velocidade(float puxada)
        {
            if (!Dispara(puxada)) return 0f;
            return Mathf.Lerp(VelocidadeMinima, VelocidadeMaxima, Mathf.InverseLerp(PuxadaMinima, PuxadaMaxima, Limitar(puxada)));
        }
    }
}
