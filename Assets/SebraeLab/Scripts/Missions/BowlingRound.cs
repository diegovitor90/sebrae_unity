using System;

namespace SebraeLab.Missions
{
    /// <summary>
    /// Regras de uma rodada de boliche de até duas bolas, sem a contagem oficial de 10 frames.
    /// Pontos da rodada são os pinos derrubados, sem bônus; o total soma as rodadas encerradas.
    /// </summary>
    public class BowlingRound
    {
        public int Rodada { get; private set; } = 1;
        public int Bola { get; private set; } = 1;
        public int PinosNestaBola { get; private set; }
        public int PontosDaRodada { get; private set; }
        public int Total { get; private set; }
        /// <summary>"STRIKE", "SPARE", "CALHA" ou vazio.</summary>
        public string Destaque { get; private set; } = "";
        public bool RodadaEncerrada { get; private set; }

        /// <summary>Registra os pinos derrubados pela bola atual (0 a 10, sem passar de 10 na rodada).</summary>
        public void RegistrarBola(int pinos, bool calha)
        {
            if (RodadaEncerrada)
                throw new InvalidOperationException("Rodada encerrada: chame ProximaRodada antes de registrar outra bola.");
            if (pinos < 0 || PontosDaRodada + pinos > 10)
                throw new ArgumentOutOfRangeException(nameof(pinos), pinos, $"A rodada já tem {PontosDaRodada} pinos; o máximo é 10.");

            PinosNestaBola = pinos;
            PontosDaRodada += pinos;
            Destaque = calha && pinos == 0 ? "CALHA" : "";

            if (Bola == 1 && pinos == 10) { Destaque = "STRIKE"; Encerrar(); return; }
            if (Bola == 2)
            {
                if (PontosDaRodada == 10) Destaque = "SPARE";
                Encerrar();
                return;
            }
            Bola = 2;
        }

        public void ProximaRodada()
        {
            Rodada++;
            Zerar();
        }

        /// <summary>
        /// Volta à bola 1 sem mudar o total. Se a rodada já estava encerrada, os pontos dela já estão
        /// no total, então avança para a próxima em vez de repetir a mesma.
        /// </summary>
        public void RecomecarRodada()
        {
            if (RodadaEncerrada) { ProximaRodada(); return; }
            Zerar();
        }

        void Encerrar()
        {
            RodadaEncerrada = true;
            Total += PontosDaRodada;
        }

        void Zerar()
        {
            Bola = 1;
            PinosNestaBola = 0;
            PontosDaRodada = 0;
            Destaque = "";
            RodadaEncerrada = false;
        }
    }
}
