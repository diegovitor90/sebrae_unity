using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using SebraeLab.Missions;
using Object = UnityEngine.Object;

namespace SebraeLab.Editor
{
    /// <summary>
    /// Verificação em Play do boliche: materiais e massas, ajuda no arremesso com controle de VR
    /// simulado, arremesso reto, calha e rearme. Pedido: Logs/bowling-check.request (conteúdo "sair"
    /// fecha o editor ao terminar) ou menu. Resultado: Logs/bowling-check.txt.
    /// </summary>
    [InitializeOnLoad]
    public static class BowlingCheck
    {
        const string Request = "Logs/bowling-check.request";
        const string Report = "Logs/bowling-check.txt";
        const string ExitKey = "BowlingCheck.Exit";
        const float LimiteJogada = 15f;

        sealed class Relatorio
        {
            public readonly StringBuilder Texto = new StringBuilder();
            public int Falhas;
            public void Secao(string nome) => Texto.AppendLine().AppendLine("== " + nome);
            public void Check(bool ok, string m) { if (!ok) Falhas++; Texto.AppendLine((ok ? "OK    " : "FALHA ") + m); }
            public void Info(string m) => Texto.AppendLine("      " + m);
        }

        static BowlingCheck() { EditorApplication.update += Poll; }

        [MenuItem("Sebrae Lab/Verificação/Boliche")]
        static void Menu() { Directory.CreateDirectory("Logs"); File.WriteAllText(Request, ""); }

        static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!EditorApplication.isPlaying)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.EnterPlaymode();
                return;
            }
            var mission = Object.FindFirstObjectByType<ManagerMission>();
            var lane = Object.FindFirstObjectByType<BowlingLane>();
            if (!mission || !lane) return;
            SessionState.SetBool(ExitKey, File.ReadAllText(Request).Trim() == "sair");
            File.Delete(Request);
            mission.StartCoroutine(Run(lane));
        }

        static IEnumerator Run(BowlingLane lane)
        {
            var r = new Relatorio();
            bool concluiu = false;
            r.Texto.AppendLine("Boliche • " + DateTime.Now);
            try
            {
                Fisica(lane, r);
                // Passos executados no mesmo iterador: uma exceção chega ao finally e o relatório sai mesmo assim.
                foreach (var passo in new Func<BowlingLane, Relatorio, IEnumerator>[] { Ajuda, ArremessoReto, Calha, Rearme })
                {
                    var e = passo(lane, r);
                    while (e.MoveNext()) yield return e.Current;
                }
                concluiu = true;
            }
            finally
            {
                if (!concluiu) r.Check(false, "execução interrompida por exceção; ver o Console");
                r.Texto.AppendLine().AppendLine(r.Falhas == 0 ? "RESULTADO: OK" : $"RESULTADO: FALHA ({r.Falhas})");
                Directory.CreateDirectory("Logs");
                File.WriteAllText(Report, r.Texto.ToString());
                Debug.Log("BowlingCheck\n" + r.Texto);
                if (SessionState.GetBool(ExitKey, false)) EditorApplication.Exit(r.Falhas == 0 ? 0 : 1);
            }
        }

        static void Fisica(BowlingLane lane, Relatorio r)
        {
            r.Secao("Física");
            bool MaterialOk(Collider c, string nome) =>
                c && c.sharedMaterial && c.sharedMaterial.name == nome && AssetDatabase.Contains(c.sharedMaterial);
            r.Check(MaterialOk(lane.Pista, "Pista"), "pista com o material Pista (asset)");
            r.Check(lane.Calhas.Count == 2 && lane.Calhas.All(c => MaterialOk(c, "Pista")), $"{lane.Calhas.Count} calhas com o material Pista (asset)");
            r.Check(MaterialOk(lane.Bola.GetComponent<Collider>(), "Bola"), "bola com o material Bola (asset)");
            var colisoresDosPinos = lane.Pinos.SelectMany(p => p.GetComponents<Collider>().Where(c => c.enabled)).ToList();
            r.Check(colisoresDosPinos.Count > 0 && colisoresDosPinos.All(c => MaterialOk(c, "Pino")), $"{colisoresDosPinos.Count} colisores de pino com o material Pino (asset)");
            var corpo = lane.Bola.GetComponent<Rigidbody>();
            r.Check(Mathf.Approximately(corpo.mass, 6f) && Mathf.Approximately(corpo.maxAngularVelocity, 60f) && corpo.interpolation == RigidbodyInterpolation.Interpolate,
                $"bola: {corpo.mass} kg, rotação máxima {corpo.maxAngularVelocity}, interpolação {corpo.interpolation}");
            r.Check(lane.Pinos.Count == 10 && lane.Pinos.All(p => Mathf.Approximately(p.mass, 1.5f)), $"{lane.Pinos.Count} pinos com 1,5 kg");
        }

        static IEnumerator Ajuda(BowlingLane lane, Relatorio r)
        {
            r.Secao("Ajuda no arremesso");
            lane.Rearmar();
            yield return new WaitForFixedUpdate();
            var pista = lane.Pista.bounds;
            var inicio = new Vector3(pista.center.x, pista.max.y + lane.RaioBola + .5f, pista.min.z + .5f);
            Soltar(lane, inicio, Quaternion.Euler(0, 25, 0) * Vector3.forward * 2f, true);
            yield return new WaitForFixedUpdate();
            var corpo = lane.Bola.GetComponent<Rigidbody>();
            var v = corpo.linearVelocity;
            var horizontal = new Vector3(v.x, 0f, v.z);
            float apoio = corpo.position.y - lane.RaioBola - pista.max.y;
            float angulo = Vector3.Angle(Vector3.forward, horizontal);
            r.Check(Mathf.Abs(apoio) < .03f, $"bola apoiada na pista (folga {apoio:0.###} m)");
            r.Check(angulo <= 10.5f, $"direção puxada para {angulo:0.#}° do eixo (soltou a 25°)");
            r.Check(horizontal.magnitude >= 2.95f && horizontal.magnitude <= 12.05f, $"velocidade {horizontal.magnitude:0.##} m/s (soltou a 2 m/s)");
            for (float t = 0; t < LimiteJogada && EmJogada(lane); t += Time.deltaTime) yield return null;
            r.Info($"jogada da ajuda terminou com {lane.Partida.PinosNestaBola} pino(s)");
        }

        static IEnumerator ArremessoReto(BowlingLane lane, Relatorio r)
        {
            r.Secao("Arremesso reto");
            lane.Rearmar();
            yield return new WaitForFixedUpdate();
            int rodadaAntes = lane.Partida.Rodada;
            var pista = lane.Pista.bounds;
            Soltar(lane, new Vector3(pista.center.x, pista.max.y + lane.RaioBola + .01f, pista.min.z + .5f), Vector3.forward * 8f, true);
            for (float t = 0; t < LimiteJogada && EmJogada(lane); t += Time.deltaTime) yield return null;
            int n = lane.Partida.PinosNestaBola;
            r.Check(!EmJogada(lane), $"jogada encerrada (fase {lane.FaseAtual})");
            r.Check(n >= 1, $"derrubou {n} pino(s)");
            var corpo = lane.Bola.GetComponent<Rigidbody>();
            r.Check(Vector3.Distance(corpo.position, lane.SuporteBola) < .05f, $"bola de volta ao suporte (a {Vector3.Distance(corpo.position, lane.SuporteBola):0.###} m)");
            r.Check(lane.Placar.text.Contains("Nesta bola: " + n), $"placar mostra a bola: \"{lane.Placar.text.Replace("\n", " | ")}\"");
            if (n == 10)
            {
                r.Check(lane.Partida.Destaque == "STRIKE" && lane.FaseAtual == BowlingLane.Fase.AguardandoRearme, "strike encerra a rodada e aguarda o rearme");
                yield return new WaitForSeconds(2.5f);
                r.Check(lane.Partida.Rodada == rodadaAntes + 1 && lane.Pinos.All(p => p.gameObject.activeSelf), "rearme automático depois do strike");
            }
            else
            {
                r.Check(lane.Partida.Bola == 2, $"rodada segue para a bola 2 (bola {lane.Partida.Bola})");
                int varridos = lane.Pinos.Count(p => !p.gameObject.activeSelf);
                r.Check(varridos == n, $"{varridos} pino(s) varrido(s) da pista (esperado {n})");
            }
        }

        static IEnumerator Calha(BowlingLane lane, Relatorio r)
        {
            r.Secao("Calha");
            lane.Rearmar();
            yield return new WaitForFixedUpdate();
            var fundo = lane.Calhas[0].bounds;
            Soltar(lane, new Vector3(fundo.center.x, fundo.max.y + lane.RaioBola + .01f, lane.Pista.bounds.min.z + .2f), Vector3.forward * 6f, false);
            for (float t = 0; t < LimiteJogada && EmJogada(lane); t += Time.deltaTime) yield return null;
            r.Check(lane.Partida.PinosNestaBola == 0 && lane.Partida.Destaque == "CALHA", $"calha: {lane.Partida.PinosNestaBola} pino(s), destaque '{lane.Partida.Destaque}'");
            r.Check(lane.Placar.text.StartsWith("CALHA"), $"placar destaca CALHA: \"{lane.Placar.text.Replace("\n", " | ")}\"");
        }

        static IEnumerator Rearme(BowlingLane lane, Relatorio r)
        {
            r.Secao("Rearme");
            int total = lane.Partida.Total;
            lane.Rearmar();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            var emPe = lane.Pinos.Count(p => p.gameObject.activeSelf && Vector3.Angle(p.rotation * Vector3.up, Vector3.up) < 5f);
            r.Check(emPe == 10, $"{emPe} de 10 pinos em pé e ativos");
            r.Check(lane.Partida.Bola == 1 && lane.Partida.PontosDaRodada == 0 && lane.Partida.Total == total,
                $"volta à bola 1 sem mudar o total (bola {lane.Partida.Bola}, total {lane.Partida.Total}, antes {total})");
            r.Check(Vector3.Distance(lane.Bola.transform.position, lane.SuporteBola) < .05f, "bola no suporte");
        }

        static bool EmJogada(BowlingLane lane) =>
            lane.FaseAtual == BowlingLane.Fase.Solta || lane.FaseAtual == BowlingLane.Fase.Rolando || lane.FaseAtual == BowlingLane.Fase.Assentando;

        /// <summary>Posiciona a bola, dá a velocidade e dispara o selectExited como se a mão a soltasse.</summary>
        static void Soltar(BowlingLane lane, Vector3 posicao, Vector3 velocidade, bool comControle)
        {
            var corpo = lane.Bola.GetComponent<Rigidbody>();
            corpo.isKinematic = false;
            corpo.transform.SetPositionAndRotation(posicao, Quaternion.identity);
            corpo.position = posicao;
            corpo.linearVelocity = velocidade;
            corpo.angularVelocity = Vector3.zero;
            corpo.WakeUp();
            Physics.SyncTransforms();
            IXRSelectInteractor quem = null;
            if (comControle)
            {
                // Objeto inativo: o interactor não acorda nem tenta selecionar nada; serve só como quem soltou.
                var mao = new GameObject("Mão de teste (boliche)");
                mao.SetActive(false);
                mao.transform.position = posicao;
                quem = mao.AddComponent<XRDirectInteractor>();
            }
            lane.Bola.selectExited.Invoke(new SelectExitEventArgs { interactorObject = quem, interactableObject = lane.Bola });
        }
    }
}
