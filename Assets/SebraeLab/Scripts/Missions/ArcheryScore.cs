using System;

namespace SebraeLab.Missions
{
    /// <summary>
    /// Regras do alvo do arco: anel pela distância ao centro e placar do tiro. Sem dependência de cena.
    /// Os raios são os mesmos usados para pintar a textura do alvo (AlvoTexture).
    /// </summary>
    public class ArcheryScore
    {
        /// <summary>Raio externo de cada anel, em metros, do centro para fora. A borda pertence ao anel de dentro.</summary>
        public static readonly float[] Raios = { .14f, .28f, .42f, .56f, .70f };
        public static readonly int[] PontosDosAneis = { 10, 8, 6, 4, 2 };
        static readonly string[] Rotulos = { "centro", "anel 2", "anel 3", "anel 4", "anel 5" };

        public int Total { get; private set; }
        public int Tiros { get; private set; }
        public int UltimoPontos { get; private set; }
        public string UltimoRotulo { get; private set; } = "nenhum";

        /// <summary>Pontos do anel atingido a esta distância do centro, ou 0 fora do último anel.</summary>
        public static int PontosDoAnel(float distancia)
        {
            int i = IndiceDoAnel(distancia);
            return i < 0 ? 0 : PontosDosAneis[i];
        }

        public void Registrar(float distancia)
        {
            if (distancia < 0f) throw new ArgumentOutOfRangeException(nameof(distancia), "A distância não pode ser negativa.");
            int i = IndiceDoAnel(distancia);
            if (i < 0) { Errou(); return; }
            Tiros++;
            UltimoPontos = PontosDosAneis[i];
            UltimoRotulo = Rotulos[i];
            Total += UltimoPontos;
        }

        public void Errou()
        {
            Tiros++;
            UltimoPontos = 0;
            UltimoRotulo = "Errou";
        }

        public string Texto
        {
            get
            {
                string ultimo = Tiros == 0 ? "nenhum" : UltimoPontos == 0 ? "Errou" : $"{UltimoPontos} ({UltimoRotulo})";
                return $"ARCO ARCADE • {Total} pontos\nÚltimo tiro: {ultimo}";
            }
        }

        static int IndiceDoAnel(float distancia)
        {
            for (int i = 0; i < Raios.Length; i++) if (distancia <= Raios[i]) return i;
            return -1;
        }
    }
}
