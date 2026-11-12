using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using SebraeLab.Missions;

namespace SebraeLab.Editor
{
    /// <summary>
    /// Teste em EditMode das regras da rodada de boliche (BowlingRound).
    /// Resultado em Logs/bowling-round-check.txt.
    /// </summary>
    public static class BowlingRoundCheck
    {
        const string Report = "Logs/bowling-round-check.txt";

        [MenuItem("Sebrae Lab/Verificação/Regras do boliche")]
        static void Menu() => Debug.Log("BowlingRoundCheck: " + (Run() == 0 ? "OK" : "FALHA") + ". Detalhes em " + Report);

        /// <summary>Para -executeMethod: fecha o Unity com 0 (ok) ou 1 (falha).</summary>
        public static void RunFromCommandLine() => EditorApplication.Exit(Run() == 0 ? 0 : 1);

        public static int Run()
        {
            var texto = new StringBuilder();
            int falhas = 0;
            void Check(bool ok, string mensagem)
            {
                if (!ok) falhas++;
                texto.AppendLine((ok ? "OK    " : "FALHA ") + mensagem);
            }
            bool Lanca<T>(Action acao) where T : Exception
            {
                try { acao(); return false; }
                catch (T) { return true; }
            }

            try
            {
                var strike = new BowlingRound();
                Check(strike.Rodada == 1 && strike.Bola == 1 && strike.Total == 0 && !strike.RodadaEncerrada, "começa na rodada 1, bola 1, total 0");
                strike.RegistrarBola(10, false);
                Check(strike.Destaque == "STRIKE" && strike.RodadaEncerrada && strike.PontosDaRodada == 10 && strike.Total == 10,
                    $"strike: destaque {strike.Destaque}, encerrada {strike.RodadaEncerrada}, total {strike.Total}");
                Check(Lanca<InvalidOperationException>(() => strike.RegistrarBola(0, false)), "rodada encerrada não aceita outra bola");

                var spare = new BowlingRound();
                spare.RegistrarBola(7, false);
                Check(spare.Bola == 2 && !spare.RodadaEncerrada && spare.Total == 0 && spare.PinosNestaBola == 7, "7 pinos na bola 1 passa para a bola 2 sem somar ao total");
                spare.RegistrarBola(3, false);
                Check(spare.Destaque == "SPARE" && spare.RodadaEncerrada && spare.Total == 10, $"spare: destaque {spare.Destaque}, total {spare.Total}");

                var aberta = new BowlingRound();
                aberta.RegistrarBola(4, false);
                aberta.RegistrarBola(3, false);
                Check(aberta.Destaque == "" && aberta.RodadaEncerrada && aberta.Total == 7, $"rodada aberta 4 + 3: destaque '{aberta.Destaque}', total {aberta.Total}");

                var calha = new BowlingRound();
                calha.RegistrarBola(0, true);
                Check(calha.Destaque == "CALHA" && calha.Bola == 2 && !calha.RodadaEncerrada, "calha na bola 1 marca CALHA e segue para a bola 2");
                calha.RegistrarBola(0, true);
                Check(calha.Destaque == "CALHA" && calha.RodadaEncerrada && calha.Total == 0, "calha na bola 2 encerra com 0");

                var zeroDez = new BowlingRound();
                zeroDez.RegistrarBola(0, false);
                zeroDez.RegistrarBola(10, false);
                Check(zeroDez.Destaque == "SPARE" && zeroDez.Total == 10, "0 + 10 é spare");

                var invalida = new BowlingRound();
                Check(Lanca<ArgumentOutOfRangeException>(() => invalida.RegistrarBola(11, false)), "11 pinos numa bola é inválido");
                Check(Lanca<ArgumentOutOfRangeException>(() => invalida.RegistrarBola(-1, false)), "pinos negativos é inválido");
                invalida.RegistrarBola(7, false);
                Check(Lanca<ArgumentOutOfRangeException>(() => invalida.RegistrarBola(4, false)), "7 + 4 passa de 10 e é inválido");

                var recomecar = new BowlingRound();
                recomecar.RegistrarBola(6, false);
                recomecar.RecomecarRodada();
                Check(recomecar.Rodada == 1 && recomecar.Bola == 1 && recomecar.PontosDaRodada == 0 && recomecar.Total == 0 && recomecar.Destaque == "",
                    "recomeçar rodada aberta volta à bola 1 sem mudar o total");
                recomecar.RegistrarBola(10, false);
                recomecar.RecomecarRodada();
                Check(recomecar.Rodada == 2 && recomecar.Bola == 1 && recomecar.Total == 10 && !recomecar.RodadaEncerrada,
                    $"recomeçar rodada encerrada avança sem contar de novo (rodada {recomecar.Rodada}, total {recomecar.Total})");

                var tres = new BowlingRound();
                tres.RegistrarBola(10, false); tres.ProximaRodada();
                tres.RegistrarBola(5, false); tres.RegistrarBola(4, false); tres.ProximaRodada();
                tres.RegistrarBola(6, false); tres.RegistrarBola(4, false);
                Check(tres.Rodada == 3 && tres.Total == 29, $"três rodadas 10 + 9 + 10 somam 29 (rodada {tres.Rodada}, total {tres.Total})");
                tres.ProximaRodada();
                Check(tres.Rodada == 4 && tres.Bola == 1 && tres.PinosNestaBola == 0 && tres.PontosDaRodada == 0 && tres.Destaque == "" && !tres.RodadaEncerrada,
                    "próxima rodada zera bola, pinos, pontos e destaque");
            }
            catch (Exception e)
            {
                falhas++;
                texto.AppendLine("FALHA exceção: " + e);
            }

            texto.Insert(0, "Regras do boliche (BowlingRound) • " + DateTime.Now + "\n");
            texto.AppendLine(falhas == 0 ? "RESULTADO: OK" : $"RESULTADO: FALHA ({falhas})");
            Directory.CreateDirectory("Logs");
            File.WriteAllText(Report, texto.ToString());
            Debug.Log(texto.ToString());
            return falhas;
        }
    }
}
