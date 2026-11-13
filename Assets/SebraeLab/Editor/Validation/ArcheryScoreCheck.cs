using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using SebraeLab.Missions;

namespace SebraeLab.Editor
{
    /// <summary>
    /// Teste em EditMode das regras do arco (ArcheryScore e ArcheryDraw).
    /// Resultado em Logs/archery-score-check.txt.
    /// </summary>
    public static class ArcheryScoreCheck
    {
        const string Report = "Logs/archery-score-check.txt";

        [MenuItem("Sebrae Lab/Verificação/Regras do arco")]
        static void Menu() => Debug.Log("ArcheryScoreCheck: " + (Run() == 0 ? "OK" : "FALHA") + ". Detalhes em " + Report);

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
            bool Perto(float a, float b) => Mathf.Abs(a - b) < .001f;

            try
            {
                var s = new ArcheryScore();
                Check(s.Total == 0 && s.Tiros == 0 && s.UltimoPontos == 0 && s.UltimoRotulo == "nenhum", "começa com total 0 e nenhum tiro");
                Check(s.Texto == "ARCO ARCADE • 0 pontos\nÚltimo tiro: nenhum", "texto inicial: " + s.Texto.Replace("\n", " | "));
                Check(ArcheryScore.Raios.Length == 5 && ArcheryScore.PontosDosAneis.Length == 5, "a tabela tem 5 anéis");

                foreach (var (distancia, pontos) in new[]
                {
                    (0f, 10), (.139f, 10), (.14f, 10), (.141f, 8), (.28f, 8), (.281f, 6), (.42f, 6),
                    (.421f, 4), (.56f, 4), (.561f, 2), (.70f, 2), (.701f, 0), (5f, 0),
                })
                    Check(ArcheryScore.PontosDoAnel(distancia) == pontos, $"distância {distancia} m vale {ArcheryScore.PontosDoAnel(distancia)} (esperado {pontos})");

                s.Registrar(.05f);
                Check(s.Total == 10 && s.UltimoPontos == 10 && s.UltimoRotulo == "centro" && s.Tiros == 1,
                    $"centro: total {s.Total}, último {s.UltimoPontos} ({s.UltimoRotulo})");
                Check(s.Texto == "ARCO ARCADE • 10 pontos\nÚltimo tiro: 10 (centro)", "texto: " + s.Texto.Replace("\n", " | "));
                s.Registrar(.2f);
                Check(s.Total == 18 && s.UltimoRotulo == "anel 2", $"segundo anel soma 8: total {s.Total}, {s.UltimoRotulo}");
                Check(s.Texto == "ARCO ARCADE • 18 pontos\nÚltimo tiro: 8 (anel 2)", "texto: " + s.Texto.Replace("\n", " | "));
                s.Registrar(.35f);
                Check(s.Total == 24 && s.UltimoRotulo == "anel 3" && s.Texto.EndsWith("Último tiro: 6 (anel 3)"), $"terceiro anel soma 6: total {s.Total}, {s.UltimoRotulo}");
                s.Registrar(.5f);
                Check(s.Total == 28 && s.UltimoRotulo == "anel 4", $"quarto anel soma 4: total {s.Total}, {s.UltimoRotulo}");
                s.Registrar(.65f);
                Check(s.Total == 30 && s.UltimoRotulo == "anel 5", $"quinto anel soma 2: total {s.Total}, {s.UltimoRotulo}");
                s.Errou();
                Check(s.Total == 30 && s.UltimoPontos == 0 && s.UltimoRotulo == "Errou" && s.Tiros == 6, $"Errou não muda o total: {s.Total}");
                Check(s.Texto == "ARCO ARCADE • 30 pontos\nÚltimo tiro: Errou", "texto: " + s.Texto.Replace("\n", " | "));
                s.Registrar(.9f);
                Check(s.Total == 30 && s.UltimoRotulo == "Errou" && s.Tiros == 7, "distância fora do último anel conta como erro");
                Check(Lanca<ArgumentOutOfRangeException>(() => s.Registrar(-.01f)), "distância negativa é inválida");
                Check(s.Total == 30 && s.Tiros == 7, "distância inválida não altera o placar");

                Check(!ArcheryDraw.Dispara(.09f) && ArcheryDraw.Dispara(.10f), "dispara a partir de 0,10 m");
                Check(Perto(ArcheryDraw.Velocidade(.09f), 0f), "abaixo de 0,10 m a velocidade é 0");
                Check(Perto(ArcheryDraw.Velocidade(.10f), 15f), "0,10 m dá 15 m/s");
                Check(Perto(ArcheryDraw.Velocidade(.30f), 27.5f), "0,30 m dá 27,5 m/s");
                Check(Perto(ArcheryDraw.Velocidade(.50f), 40f), "0,50 m dá 40 m/s");
                Check(Perto(ArcheryDraw.Velocidade(.80f), 40f), "0,80 m fica limitado a 40 m/s");
                Check(Perto(ArcheryDraw.Limitar(.80f), .5f) && Perto(ArcheryDraw.Limitar(-.1f), 0f) && Perto(ArcheryDraw.Limitar(.2f), .2f), "a puxada fica entre 0 e 0,50 m");
            }
            catch (Exception e)
            {
                falhas++;
                texto.AppendLine("FALHA exceção: " + e);
            }

            texto.Insert(0, "Regras do arco (ArcheryScore e ArcheryDraw) • " + DateTime.Now + "\n");
            texto.AppendLine(falhas == 0 ? "RESULTADO: OK" : $"RESULTADO: FALHA ({falhas})");
            Directory.CreateDirectory("Logs");
            File.WriteAllText(Report, texto.ToString());
            Debug.Log(texto.ToString());
            return falhas;
        }
    }
}
