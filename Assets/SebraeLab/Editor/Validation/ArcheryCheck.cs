using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using SebraeLab.Missions;
using Object = UnityEngine.Object;

namespace SebraeLab.Editor
{
    /// <summary>
    /// Verificação em Play do arco: montagem, disparo do PC, gatilho do Quest, tiro no chão e recarga
    /// (e, com a corda, puxada, soltura e cancelamento). Mãos de teste são XRDirectInteractor inativos que só
    /// servem de "quem pegou ou soltou". Pedido: Logs/archery-check.request (conteúdo "sair" fecha o editor ao
    /// terminar) ou menu. Resultado: Logs/archery-check.txt.
    /// </summary>
    [InitializeOnLoad]
    public static class ArcheryCheck
    {
        const string Request = "Logs/archery-check.request";
        const string Report = "Logs/archery-check.txt";
        const string ExitKey = "ArcheryCheck.Exit";
        const string PrefixoDasMaos = "Mão de teste (arco)";
        const float LimiteDoVoo = 8f;
        static readonly Vector3 PosicaoDoArco = new Vector3(51f, 1.5f, -2f);

        sealed class Relatorio
        {
            public readonly StringBuilder Texto = new StringBuilder();
            public int Falhas;
            public void Secao(string nome) => Texto.AppendLine().AppendLine("== " + nome);
            public void Check(bool ok, string m) { if (!ok) Falhas++; Texto.AppendLine((ok ? "OK    " : "FALHA ") + m); }
            public void Info(string m) => Texto.AppendLine("      " + m);
        }

        static ArcheryCheck() { EditorApplication.update += Poll; }

        [MenuItem("Sebrae Lab/Verificação/Arco")]
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
            var alcance = Object.FindFirstObjectByType<ArcheryRange>();
            if (!mission || !alcance) return;
            SessionState.SetBool(ExitKey, File.ReadAllText(Request).Trim() == "sair");
            File.Delete(Request);
            mission.StartCoroutine(Run(alcance));
        }

        static IEnumerator Run(ArcheryRange alcance)
        {
            var r = new Relatorio();
            bool concluiu = false;
            r.Texto.AppendLine("Arco • " + DateTime.Now);
            var corpo = alcance.Arco.GetComponent<Rigidbody>();
            var posicaoInicial = corpo.position;
            var rotacaoInicial = corpo.rotation;
            try
            {
                Montagem(alcance, r);
                // Passos executados no mesmo iterador: uma exceção chega ao finally e o relatório sai mesmo assim.
                foreach (var passo in new Func<ArcheryRange, Relatorio, IEnumerator>[] { DisparoDoPc, Recarga, GatilhoDoQuest, TiroNoChao,
                    PuxadaMaxima, PuxadaFraca, PuxadaMinima, PuxadaMedia, SoltarOArco })
                {
                    var e = passo(alcance, r);
                    while (e.MoveNext()) yield return e.Current;
                }
                concluiu = true;
            }
            finally
            {
                if (!concluiu) r.Check(false, "execução interrompida por exceção; ver o Console");
                corpo.isKinematic = false;
                corpo.transform.SetPositionAndRotation(posicaoInicial, rotacaoInicial);
                corpo.position = posicaoInicial;
                corpo.rotation = rotacaoInicial;
                foreach (var mao in Object.FindObjectsByType<XRDirectInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (mao.name.StartsWith(PrefixoDasMaos)) Object.Destroy(mao.gameObject);
                r.Texto.AppendLine().AppendLine(r.Falhas == 0 ? "RESULTADO: OK" : $"RESULTADO: FALHA ({r.Falhas})");
                Directory.CreateDirectory("Logs");
                File.WriteAllText(Report, r.Texto.ToString());
                Debug.Log("ArcheryCheck\n" + r.Texto);
                if (SessionState.GetBool(ExitKey, false)) EditorApplication.Exit(r.Falhas == 0 ? 0 : 1);
            }
        }

        static void Montagem(ArcheryRange a, Relatorio r)
        {
            r.Secao("Montagem");
            r.Check(a.Arco && a.Alvo && a.FlechaCarregada, "arco, alvo e flecha carregada ligados ao ArcheryRange");
            Physics.SyncTransforms();
            var colisores = a.Alvo.GetComponents<Collider>();
            var caixa = colisores.OfType<BoxCollider>().FirstOrDefault(c => c.enabled);
            r.Check(caixa != null && !colisores.OfType<SphereCollider>().Any(c => c.enabled), "alvo com colisor de caixa e sem esfera ligada");
            if (caixa != null)
            {
                var t = caixa.bounds.size;
                r.Check(Mathf.Abs(t.x - 1.4f) < .01f && Mathf.Abs(t.y - 1.4f) < .01f && Mathf.Abs(t.z - .15f) < .01f, $"caixa do alvo com {t.x:0.###} x {t.y:0.###} x {t.z:0.###} m");
            }
            var face = a.Alvo.Find("Face do alvo");
            r.Check(face && Mathf.Abs(face.lossyScale.x - 1.4f) < .01f, $"face do alvo com {(face ? face.lossyScale.x : 0f):0.###} m de diâmetro");
            r.Check(a.Texto && a.Texto.text.StartsWith("ARCO ARCADE • 0 pontos"), $"placar inicial: \"{(a.Texto ? a.Texto.text.Replace("\n", " | ") : "")}\"");
            r.Check(a.FlechaCarregada && a.FlechaCarregada.transform.parent == a.Visual, "flecha de luz carregada sobre o arco recurvo");
        }

        static IEnumerator DisparoDoPc(ArcheryRange a, Relatorio r)
        {
            r.Secao("Disparo do PC");
            var centro = a.Alvo.position;
            Mirar(a, PosicaoDoArco, centro);
            yield return null;
            int antes = a.Placar.Total, tiros = a.Disparos;
            a.Arco.activated.Invoke(null); // o DesktopPlayer chama assim, sem argumentos
            yield return AteAcabar(a);
            var f = a.UltimaFlecha;
            r.Check(a.Disparos == tiros + 1 && f != null, $"um disparo ({a.Disparos - tiros})");
            if (f == null) yield break;
            r.Check(Mathf.Abs(f.VelocidadeInicial - ArcheryDraw.VelocidadeMaxima) < .05f, $"velocidade inicial {f.VelocidadeInicial:0.##} m/s");
            r.Check(f.AcertouAlvo, "a flecha parou no alvo");
            r.Check(Mathf.Abs(f.Ponta.z - (centro.z - .075f)) < .03f, $"parou na face do alvo a 40 m/s (z {f.Ponta.z:0.###}, face em {centro.z - .075f:0.###})");
            r.Check(a.Placar.UltimoPontos >= 8 && a.Placar.Total == antes + a.Placar.UltimoPontos,
                $"mirando o centro pontua 8 ou 10 (ganhou {a.Placar.UltimoPontos}, {a.Placar.UltimoRotulo}, a {f.DistanciaDoCentro:0.###} m do centro)");
            r.Check(a.Texto.text.StartsWith($"ARCO ARCADE • {a.Placar.Total} pontos"), $"placar mostra o total: \"{a.Texto.text.Replace("\n", " | ")}\"");
        }

        static IEnumerator Recarga(ArcheryRange a, Relatorio r)
        {
            r.Secao("Recarga");
            yield return AguardarFlecha(a);
            r.Check(a.FlechaCarregada && a.FlechaCarregada.transform.parent == a.Visual, "flecha recarregada sobre a corda");
        }

        static IEnumerator GatilhoDoQuest(ArcheryRange a, Relatorio r)
        {
            r.Secao("Gatilho do Quest");
            var mao = Mao("gatilho");
            int tiros = a.Disparos;
            a.Arco.activated.Invoke(new ActivateEventArgs { interactorObject = mao, interactableObject = a.Arco });
            yield return null;
            r.Check(a.Disparos == tiros && a.FlechaCarregada, "o gatilho do controle não dispara: a flecha só sai pela corda");
        }

        static IEnumerator TiroNoChao(ArcheryRange a, Relatorio r)
        {
            r.Secao("Tiro no chão");
            Mirar(a, PosicaoDoArco, new Vector3(PosicaoDoArco.x, 0f, 0f));
            yield return null;
            int antes = a.Placar.Total, tiros = a.Disparos;
            a.Arco.activated.Invoke(null);
            yield return AteAcabar(a);
            var f = a.UltimaFlecha;
            r.Check(a.Disparos == tiros + 1 && f != null && !f.Voando && !f.AcertouAlvo, "a flecha parou fora do alvo");
            r.Check(a.Placar.Total == antes && a.Placar.UltimoRotulo == "Errou", $"conta Errou sem mexer no total ({a.Placar.UltimoRotulo}, total {a.Placar.Total}, antes {antes})");
            r.Check(a.Texto.text.Contains("Último tiro: Errou"), $"placar mostra o erro: \"{a.Texto.text.Replace("\n", " | ")}\"");
            yield return AguardarFlecha(a);
        }

        static IEnumerator PuxadaMaxima(ArcheryRange a, Relatorio r)
        {
            r.Secao("Puxada máxima");
            var centro = a.Alvo.position;
            Mirar(a, PosicaoDoArco, centro);
            yield return AguardarFlecha(a);
            var corda = a.Corda;
            r.Check(corda != null && corda.Pegador, "o arco tem corda com pegador");
            if (corda == null || !corda.Pegador) yield break;
            var maoDoArco = Mao("arco");
            var maoDaCorda = Mao("corda");
            var colisor = corda.Pegador.GetComponent<Collider>();
            yield return null;
            r.Check(!colisor.enabled, "pegador sem colisor enquanto o arco não está na mão");
            a.Arco.selectEntered.Invoke(Entrar(maoDoArco, a.Arco));
            yield return null;
            yield return null;
            r.Check(colisor.enabled && colisor.isTrigger, "pegador agarrável (trigger) com o arco na mão");

            float zRepouso = a.FlechaCarregada.transform.localPosition.z;
            corda.Pegador.selectEntered.Invoke(Entrar(maoDaCorda, corda.Pegador));
            corda.Pegador.transform.position = corda.PosicaoDeRepouso - a.Visual.forward * .8f; // além do limite
            yield return null;
            yield return null;
            r.Check(corda.PegadorAgarrado, "pegador agarrado");
            r.Check(Mathf.Abs(corda.Puxada - ArcheryDraw.PuxadaMaxima) < .005f, $"puxada limitada a 0,50 m (puxou 0,80): {corda.Puxada:0.###} m");
            r.Check(Mathf.Abs(a.FlechaCarregada.transform.localPosition.z - (zRepouso - ArcheryDraw.PuxadaMaxima)) < .01f, "a flecha recuou 0,50 m junto com a corda");
            float esperado = Vector3.Distance(new Vector3(0f, .77f, -.12f), new Vector3(0f, 0f, -.12f - ArcheryDraw.PuxadaMaxima));
            r.Check(Mathf.Abs(corda.SegmentoSuperior.localScale.y - esperado) < .01f && Mathf.Abs(corda.SegmentoInferior.localScale.y - esperado) < .01f,
                $"os dois segmentos da corda esticam até o pegador ({corda.SegmentoSuperior.localScale.y:0.###} m, esperado {esperado:0.###})");

            int antes = a.Placar.Total, tiros = a.Disparos;
            corda.Pegador.selectExited.Invoke(Sair(maoDaCorda, corda.Pegador));
            yield return AteAcabar(a);
            var f = a.UltimaFlecha;
            r.Check(a.Disparos == tiros + 1 && f != null, $"soltar o pegador dispara ({a.Disparos - tiros} tiro)");
            if (f != null)
            {
                r.Check(Mathf.Abs(f.VelocidadeInicial - ArcheryDraw.VelocidadeMaxima) < .05f, $"velocidade {f.VelocidadeInicial:0.##} m/s");
                r.Check(f.AcertouAlvo && a.Placar.UltimoPontos >= 8 && a.Placar.Total == antes + a.Placar.UltimoPontos,
                    $"a flecha acertou o alvo pelo anel de 8 ou 10 (ganhou {a.Placar.UltimoPontos}, {a.Placar.UltimoRotulo})");
            }
            r.Check(corda.Puxada == 0f && !corda.PegadorAgarrado && Vector3.Distance(corda.Pegador.transform.position, corda.PosicaoDeRepouso) < .005f, "pegador de volta ao repouso");
            a.Arco.selectExited.Invoke(Sair(maoDoArco, a.Arco));
            yield return AguardarFlecha(a);
            r.Check(!colisor.enabled, "pegador sem colisor de novo depois de soltar o arco");
        }

        static IEnumerator PuxadaFraca(ArcheryRange a, Relatorio r)
        {
            r.Secao("Puxada fraca");
            Mirar(a, PosicaoDoArco, a.Alvo.position);
            yield return AguardarFlecha(a);
            var corda = a.Corda;
            var maoDoArco = Mao("arco");
            var maoDaCorda = Mao("corda");
            a.Arco.selectEntered.Invoke(Entrar(maoDoArco, a.Arco));
            yield return null;
            var flecha = a.FlechaCarregada;
            float zRepouso = flecha.transform.localPosition.z;
            corda.Pegador.selectEntered.Invoke(Entrar(maoDaCorda, corda.Pegador));
            corda.Pegador.transform.position = corda.PosicaoDeRepouso - a.Visual.forward * .05f;
            yield return null;
            yield return null;
            int tiros = a.Disparos;
            corda.Pegador.selectExited.Invoke(Sair(maoDaCorda, corda.Pegador));
            yield return null;
            r.Check(a.Disparos == tiros, "puxada de 0,05 m não dispara");
            r.Check(a.FlechaCarregada == flecha && Mathf.Abs(flecha.transform.localPosition.z - zRepouso) < .005f, "a flecha continua carregada, de volta ao repouso");
            r.Check(corda.Puxada == 0f && !corda.PegadorAgarrado, "pegador solto e sem puxada");
            a.Arco.selectExited.Invoke(Sair(maoDoArco, a.Arco));
        }

        static IEnumerator PuxadaMinima(ArcheryRange a, Relatorio r)
        {
            r.Secao("Puxada mínima");
            Mirar(a, PosicaoDoArco, a.Alvo.position);
            yield return AguardarFlecha(a);
            var corda = a.Corda;
            var maoDoArco = Mao("arco");
            var maoDaCorda = Mao("corda");
            a.Arco.selectEntered.Invoke(Entrar(maoDoArco, a.Arco));
            yield return null;
            corda.Pegador.selectEntered.Invoke(Entrar(maoDaCorda, corda.Pegador));
            const float puxada = .11f; // um pouco acima do mínimo: 0,10 exato pode medir 0,0999 por arredondamento
            corda.Pegador.transform.position = corda.PosicaoDeRepouso - a.Visual.forward * puxada;
            yield return null;
            yield return null;
            int antes = a.Placar.Total, tiros = a.Disparos;
            corda.Pegador.selectExited.Invoke(Sair(maoDaCorda, corda.Pegador));
            yield return AteAcabar(a);
            var f = a.UltimaFlecha;
            r.Check(a.Disparos == tiros + 1 && f != null, "puxada de 0,11 m dispara");
            if (f != null)
            {
                float v = ArcheryDraw.Velocidade(puxada);
                r.Check(Mathf.Abs(f.VelocidadeInicial - v) < .05f, $"velocidade {f.VelocidadeInicial:0.##} m/s (esperado {v:0.##})");
                r.Check(!f.AcertouAlvo && a.Placar.UltimoRotulo == "Errou" && a.Placar.Total == antes,
                    $"a flecha lenta cai antes do alvo e conta Errou ({a.Placar.UltimoRotulo}, total {a.Placar.Total}, antes {antes})");
            }
            a.Arco.selectExited.Invoke(Sair(maoDoArco, a.Arco));
            yield return AguardarFlecha(a);
        }

        static IEnumerator PuxadaMedia(ArcheryRange a, Relatorio r)
        {
            r.Secao("Puxada média");
            Mirar(a, PosicaoDoArco, a.Alvo.position);
            yield return AguardarFlecha(a);
            var corda = a.Corda;
            var maoDoArco = Mao("arco");
            var maoDaCorda = Mao("corda");
            a.Arco.selectEntered.Invoke(Entrar(maoDoArco, a.Arco));
            yield return null;
            corda.Pegador.selectEntered.Invoke(Entrar(maoDaCorda, corda.Pegador));
            corda.Pegador.transform.position = corda.PosicaoDeRepouso - a.Visual.forward * .3f;
            yield return null;
            yield return null;
            int tiros = a.Disparos;
            corda.Pegador.selectExited.Invoke(Sair(maoDaCorda, corda.Pegador));
            yield return AteAcabar(a);
            var f = a.UltimaFlecha;
            r.Check(a.Disparos == tiros + 1 && f != null, "puxada de 0,30 m dispara");
            if (f != null) r.Check(Mathf.Abs(f.VelocidadeInicial - 27.5f) < .05f, $"velocidade {f.VelocidadeInicial:0.##} m/s (esperado 27,5)");
            a.Arco.selectExited.Invoke(Sair(maoDoArco, a.Arco));
            yield return AguardarFlecha(a);
        }

        static IEnumerator SoltarOArco(ArcheryRange a, Relatorio r)
        {
            r.Secao("Soltar o arco no meio da puxada");
            Mirar(a, PosicaoDoArco, a.Alvo.position);
            yield return AguardarFlecha(a);
            var corda = a.Corda;
            var maoDoArco = Mao("arco");
            var maoDaCorda = Mao("corda");
            a.Arco.selectEntered.Invoke(Entrar(maoDoArco, a.Arco));
            yield return null;
            var flecha = a.FlechaCarregada;
            float zRepouso = flecha.transform.localPosition.z;
            corda.Pegador.selectEntered.Invoke(Entrar(maoDaCorda, corda.Pegador));
            corda.Pegador.transform.position = corda.PosicaoDeRepouso - a.Visual.forward * .3f;
            yield return null;
            yield return null;
            r.Check(corda.Puxada > .25f, $"puxando 0,30 m ({corda.Puxada:0.###})");
            int tiros = a.Disparos;
            a.Arco.selectExited.Invoke(Sair(maoDoArco, a.Arco)); // arco solto com a corda puxada
            yield return null;
            yield return null;
            r.Check(a.Disparos == tiros, "soltar o arco no meio da puxada não dispara");
            r.Check(corda.Puxada == 0f && !corda.PegadorAgarrado && Vector3.Distance(corda.Pegador.transform.position, corda.PosicaoDeRepouso) < .005f, "pegador e corda de volta ao repouso");
            r.Check(a.FlechaCarregada == flecha && Mathf.Abs(flecha.transform.localPosition.z - zRepouso) < .005f, "a flecha voltou ao repouso");
            corda.Pegador.selectExited.Invoke(Sair(maoDaCorda, corda.Pegador)); // o XRI avisa depois do cancelamento
            yield return null;
            r.Check(a.Disparos == tiros, "o aviso tardio de soltura do pegador também não dispara");
        }

        // Ajudantes ---------------------------------------------------------------------------------------

        /// <summary>Põe o arco parado em <paramref name="de"/>, com o eixo +Z apontando para <paramref name="para"/>.</summary>
        static void Mirar(ArcheryRange a, Vector3 de, Vector3 para)
        {
            var corpo = a.Arco.GetComponent<Rigidbody>();
            corpo.isKinematic = true;
            var rotacao = Quaternion.LookRotation(para - de);
            corpo.transform.SetPositionAndRotation(de, rotacao);
            corpo.position = de;
            corpo.rotation = rotacao;
            Physics.SyncTransforms();
        }

        static IEnumerator AteAcabar(ArcheryRange a)
        {
            for (float t = 0; t < LimiteDoVoo && a.UltimaFlecha != null && a.UltimaFlecha.Voando; t += Time.deltaTime) yield return null;
            yield return null;
        }

        static IEnumerator AguardarFlecha(ArcheryRange a)
        {
            for (float t = 0; t < 2f && !a.FlechaCarregada; t += Time.deltaTime) yield return null;
            yield return null;
        }

        /// <summary>Mão de teste: objeto inativo, o interactor não acorda nem tenta selecionar nada.</summary>
        static XRDirectInteractor Mao(string nome)
        {
            var objeto = new GameObject(PrefixoDasMaos + " • " + nome);
            objeto.SetActive(false);
            return objeto.AddComponent<XRDirectInteractor>();
        }

        static SelectEnterEventArgs Entrar(IXRSelectInteractor mao, IXRSelectInteractable alvo) =>
            new SelectEnterEventArgs { interactorObject = mao, interactableObject = alvo };

        static SelectExitEventArgs Sair(IXRSelectInteractor mao, IXRSelectInteractable alvo) =>
            new SelectExitEventArgs { interactorObject = mao, interactableObject = alvo };
    }
}
