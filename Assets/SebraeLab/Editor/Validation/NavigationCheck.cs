using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using SebraeLab.Missions;
using Object = UnityEngine.Object;

namespace SebraeLab.Editor
{
    /// <summary>
    /// Verificação em Play para a locomoção livre: rota por todos os ambientes com um corpo do
    /// tamanho do rig VR, tetos sobre os pisos cobertos, luzes ajustadas para o Quest e custo de
    /// renderização por ambiente. Pedido: Logs/navigation-check.request (conteúdo "sair" fecha o
    /// editor ao terminar) ou menu. Resultado: Logs/navigation-check.txt.
    /// </summary>
    [InitializeOnLoad]
    public static class NavigationCheck
    {
        const string Request = "Logs/navigation-check.request";
        const string Report = "Logs/navigation-check.txt";
        const string ExitKey = "NavigationCheck.Exit";
        const float DegrauDesejado = .3f; // obstáculo mais alto que isso bloqueia a rota
        const float Grade = .2f;

        static readonly string[] PisosCobertos =
            { "Piso - Teletransporte", "Galeria da praça", "Piso do escritório", "Ala de atividades", "Galeria de descompressão", "Sala de descompressão" };

        static readonly Vector3[] Rota =
        {
            new Vector3(5.7f, 0, 7.1f), new Vector3(3.4f, 0, 6.7f), new Vector3(5.7f, 0, 8f),
            new Vector3(6, 0, 10), new Vector3(9, 0, 11), new Vector3(12, 0, 11), new Vector3(14, 0, 10),
            new Vector3(14, 0, 7), new Vector3(14, 0, 4), new Vector3(14, 0, 1), new Vector3(15, 0, 0),
            new Vector3(17, 0, 0), new Vector3(19, 0, 0), new Vector3(21, 0, 0), new Vector3(26, 0, 0), new Vector3(31, 0, 0),
            new Vector3(33, 0, 0), new Vector3(40, 0, 0), new Vector3(42, 0, 0), new Vector3(42, 0, -5), new Vector3(48, 0, -5), new Vector3(48, 0, 0),
            new Vector3(42, 0, -5), new Vector3(40, 0, 0), new Vector3(26, 0, 0), new Vector3(17, 0, 0), new Vector3(14, 0, 4), new Vector3(9, 0, 11), new Vector3(5.7f, 0, 7.1f),
        };

        static readonly (string nome, Vector3 ponto)[] Ambientes =
        {
            ("Recepção", new Vector3(5.7f, 0, 7.1f)), ("Praça", new Vector3(9, 0, 11)), ("Galeria da praça", new Vector3(14, 0, 2.5f)),
            ("Sala 2", new Vector3(18, 0, 0)), ("Ala de atividades", new Vector3(26, 0, 0)),
            ("Galeria de descompressão", new Vector3(36.5f, 0, 0)), ("Descompressão", new Vector3(48, 0, -5)),
        };

        sealed class Relatorio
        {
            public readonly StringBuilder Texto = new StringBuilder();
            public int Falhas;
            public void Secao(string nome) => Texto.AppendLine().AppendLine("== " + nome);
            public void Ok(string m) => Texto.AppendLine("OK    " + m);
            public void Falha(string m) { Falhas++; Texto.AppendLine("FALHA " + m); }
            public void Info(string m) => Texto.AppendLine("      " + m);
        }

        sealed class Caminhada
        {
            public string Erro;
            public float MaiorSubida;
            public Vector3 OndeSubiu;
        }

        static NavigationCheck() { EditorApplication.update += Poll; }

        [MenuItem("Sebrae Lab/Verificação/Navegação, tetos e luzes")]
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
            if (!mission || !mission.day) return;
            SessionState.SetBool(ExitKey, File.ReadAllText(Request).Trim() == "sair");
            File.Delete(Request);
            mission.StartCoroutine(Run(mission));
        }

        static IEnumerator Run(ManagerMission mission)
        {
            var r = new Relatorio();
            r.Texto.AppendLine("Navegação, tetos e luzes • " + DateTime.Now);
            yield return null;

            Secao(r, "Locomoção", Locomocao);
            Secao(r, "Iluminação", Iluminacao);
            Secao(r, "Tetos", Tetos);

            r.Secao("Rota");
            yield return Seguro(Percurso(mission, r), r, "rota");

            r.Secao("Custo por ambiente (editor, referência)");
            yield return Seguro(Custo(mission, r), r, "custo por ambiente");

            r.Texto.AppendLine().AppendLine(r.Falhas == 0 ? "RESULTADO: OK" : $"RESULTADO: FALHA ({r.Falhas})");
            try
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText(Report, r.Texto.ToString());
                Debug.Log("NavigationCheck\n" + r.Texto);
            }
            finally
            {
                if (SessionState.GetBool(ExitKey, false)) EditorApplication.Exit(r.Falhas == 0 ? 0 : 1);
            }
        }

        static void Secao(Relatorio r, string nome, Action<Relatorio> corpo)
        {
            r.Secao(nome);
            try { corpo(r); }
            catch (Exception e) { r.Falha($"erro em {nome}: {e.GetType().Name}: {e.Message}"); }
        }

        /// <summary>Anda o iterador por dentro de try/catch (yield não é permitido em try com catch); exceção vira falha.</summary>
        static IEnumerator Seguro(IEnumerator inner, Relatorio r, string nome)
        {
            while (true)
            {
                object atual;
                try
                {
                    if (!inner.MoveNext()) yield break;
                    atual = inner.Current;
                }
                catch (Exception e) { r.Falha($"erro em {nome}: {e.GetType().Name}: {e.Message}"); yield break; }
                if (atual is IEnumerator aninhado) yield return Seguro(aninhado, r, nome);
                else yield return atual;
            }
        }

        static IEnumerator Percurso(ManagerMission mission, Relatorio r)
        {
            var rig = Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.gameObject.name == "Jogador VR - Recepcao");
            if (!rig) { r.Falha("CharacterController do rig 'Jogador VR - Recepcao' não encontrado"); yield break; }

            var pc = mission.playerRoot ? mission.playerRoot.GetComponent<CharacterController>() : null;
            if (pc && pc != rig) pc.enabled = false; // o corpo do modo PC não pode bloquear o teste

            var porta = Object.FindFirstObjectByType<HandDoor>();
            if (porta && !porta.IsOpen) { porta.Toggle(); yield return new WaitForSeconds(1f); }
            var portao = GameObject.Find("Porta da descompressão");
            if (portao) { portao.SetActive(false); r.Info("Porta da descompressão aberta para o teste"); }

            var corpo = new GameObject("Corpo de teste (rig VR)").AddComponent<CharacterController>();
            corpo.height = rig.height; corpo.radius = rig.radius; corpo.center = rig.center;
            corpo.stepOffset = rig.stepOffset; corpo.slopeLimit = rig.slopeLimit; corpo.skinWidth = rig.skinWidth;
            corpo.transform.position = Rota[0];
            r.Info($"Corpo: raio {corpo.radius:0.##} m, altura {corpo.height:0.##} m, degrau {corpo.stepOffset:0.##} m, inclinação {corpo.slopeLimit:0}°");
            Physics.SyncTransforms();

            var estado = new Caminhada();
            for (int i = 1; i < Rota.Length && estado.Erro == null; i++)
            {
                yield return Andar(corpo, Rota[i], estado);
                if (estado.Erro == null) r.Ok($"trecho {i}: chegou a {Fmt(Rota[i])}");
            }
            if (estado.Erro != null) r.Falha("rota: " + estado.Erro);
            r.Info($"Maior subida em um passo: {estado.MaiorSubida:0.###} m, em {Fmt(estado.OndeSubiu)}");
            Object.Destroy(corpo.gameObject);
        }

        static void Locomocao(Relatorio r)
        {
            var esquerdo = Object.FindObjectsByType<ControllerInputActionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.gameObject.name == "Left Controller");
            if (!esquerdo) r.Falha("ControllerInputActionManager do Left Controller não encontrado");
            else if (esquerdo.smoothMotionEnabled) r.Ok("movimento contínuo ligado no controle esquerdo");
            else r.Falha("movimento contínuo desligado no controle esquerdo (override na cena)");
        }

        static void Iluminacao(Relatorio r)
        {
            var luzes = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            var direcionais = luzes.Where(l => l.type == LightType.Directional).ToArray();
            if (direcionais.Length == 1 && direcionais[0].renderMode == LightRenderMode.ForcePixel && direcionais[0].shadows == LightShadows.None)
                r.Ok("uma luz direcional, Important, sem sombra");
            else
                r.Falha($"luz direcional: {direcionais.Length} encontrada(s); " + string.Join(", ", direcionais.Select(l => $"{l.name} modo {l.renderMode} sombra {l.shadows}")));

            var pontuais = luzes.Where(l => l.type == LightType.Point).ToArray();
            var porPixel = pontuais.Where(l => l.renderMode != LightRenderMode.ForceVertex).ToArray();
            if (porPixel.Length == 0) r.Ok($"{pontuais.Length} luzes pontuais, todas por vértice");
            else r.Falha($"{porPixel.Length} de {pontuais.Length} luzes pontuais fora do modo por vértice: " + string.Join(", ", porPixel.Select(l => l.name).Distinct()));

            var luminarias = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(m => m.name == "Luminária").ToArray();
            var semEmissao = luminarias.Where(m => !m.sharedMaterial || m.sharedMaterial.name != "LuminariaAcesa").ToArray();
            if (luminarias.Length > 0 && semEmissao.Length == 0) r.Ok($"{luminarias.Length} luminárias com o material LuminariaAcesa");
            else r.Falha($"{semEmissao.Length} de {luminarias.Length} luminárias sem o material LuminariaAcesa");

            // Em Play, nem o objeto em memória nem o carregado do asset trazem a tabela por plataforma
            // (m_PerPlatformDefaultQuality vem vazia); por isso o YAML do projeto é lido direto.
            const string ArquivoQualidade = "ProjectSettings/QualitySettings.asset";
            var yaml = File.Exists(ArquivoQualidade) ? File.ReadAllText(ArquivoQualidade) : "";
            var padrao = System.Text.RegularExpressions.Regex.Match(yaml, @"m_PerPlatformDefaultQuality:[\s\S]*?\bAndroid:\s*(\d+)");
            var blocos = yaml.Split(new[] { "- serializedVersion:" }, StringSplitOptions.None).Skip(1).ToArray();
            if (!padrao.Success || !int.TryParse(padrao.Groups[1].Value, out int indiceAndroid) || indiceAndroid >= blocos.Length)
                r.Falha("nível de qualidade padrão do Android não encontrado");
            else
            {
                var bloco = blocos[indiceAndroid];
                string nome = "", valorAa = "";
                foreach (var linha in bloco.Split((char)10).Select(l => l.Trim()))
                {
                    if (nome == "" && linha.StartsWith("name:")) nome = linha.Substring(5).Trim();
                    if (linha.StartsWith("antiAliasing:")) valorAa = linha.Substring(13).Trim();
                }
                int aa = int.TryParse(valorAa, out var lido) ? lido : -1;
                if (aa == 4) r.Ok($"MSAA 4x no nível {nome} (Android)");
                else r.Falha($"MSAA {aa}x no nível {nome} (Android, esperado 4x)");
            }
        }

        static void Tetos(Relatorio r)
        {
            var antes = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true; // a parede norte tem pontos exatamente na linha do teto
            try { SondarTetos(r); }
            finally { Physics.queriesHitBackfaces = antes; }
        }

        static void SondarTetos(Relatorio r)
        {
            var colisores = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
            foreach (var nome in PisosCobertos)
            {
                var pisos = colisores.Where(c => c.name == nome && !c.isTrigger).ToArray();
                if (pisos.Length == 0) { r.Falha($"{nome}: piso não encontrado"); continue; }
                int total = 0, ignorados = 0;
                Bounds? pegada = null;
                if (nome == "Piso - Teletransporte")
                {
                    var teto = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).FirstOrDefault(m => m.gameObject.name == "Teto");
                    if (!teto) { r.Falha("Teto do prédio não encontrado"); continue; }
                    pegada = teto.bounds;
                }
                var descobertos = new List<Vector3>();
                foreach (var piso in pisos)
                {
                    var b = piso.bounds;
                    for (float x = b.min.x + .25f; x < b.max.x; x += .75f)
                    for (float z = b.min.z + .25f; z < b.max.z; z += .75f)
                    {
                        if (!piso.Raycast(new Ray(new Vector3(x, b.max.y + 1f, z), Vector3.down), out var chao, 3f)) continue;
                        if (pegada.HasValue && (chao.point.x < pegada.Value.min.x || chao.point.x > pegada.Value.max.x
                            || chao.point.z < pegada.Value.min.z || chao.point.z > pegada.Value.max.z)) { ignorados++; continue; }
                        total++;
                        var coberto = Physics.RaycastAll(chao.point + Vector3.up * 1.2f, Vector3.up, 6f, ~0, QueryTriggerInteraction.Ignore)
                            .Any(h => h.point.y >= chao.point.y + 2.2f);
                        if (!coberto) descobertos.Add(chao.point);
                    }
                }
                if (ignorados > 0) r.Info($"{nome}: {ignorados} pontos fora da pegada do Teto (área externa), ignorados");
                if (descobertos.Count == 0) r.Ok($"{nome}: {total} pontos, todos cobertos");
                else
                {
                    r.Falha($"{nome}: {descobertos.Count} de {total} pontos sem teto, por exemplo " + string.Join(" ", descobertos.Take(3).Select(Fmt)));
                    foreach (var area in Areas(descobertos))
                        r.Info($"sem teto entre x {area.Min(v => v.x):0.##} e {area.Max(v => v.x):0.##}, z {area.Min(v => v.z):0.##} e {area.Max(v => v.z):0.##}");
                }
            }
        }

        /// <summary>Separa os pontos em áreas quando há vão maior que 2 m em X ou Z (divisão 1D simples).</summary>
        static List<List<Vector3>> Areas(List<Vector3> pontos)
        {
            var grupos = new List<List<Vector3>> { pontos };
            foreach (var eixo in new Func<Vector3, float>[] { v => v.x, v => v.z })
            {
                var novos = new List<List<Vector3>>();
                foreach (var g in grupos)
                {
                    var ord = g.OrderBy(eixo).ToList();
                    var atual = new List<Vector3> { ord[0] };
                    for (int i = 1; i < ord.Count; i++)
                    {
                        if (eixo(ord[i]) - eixo(ord[i - 1]) > 2f) { novos.Add(atual); atual = new List<Vector3>(); }
                        atual.Add(ord[i]);
                    }
                    novos.Add(atual);
                }
                grupos = novos;
            }
            return grupos;
        }

        static IEnumerator Andar(CharacterController corpo, Vector3 alvo, Caminhada estado)
        {
            var caminho = Caminho(corpo, alvo);
            if (caminho == null) { estado.Erro = $"sem caminho livre de {Fmt(corpo.transform.position)} até {Fmt(alvo)}"; yield break; }
            foreach (var ponto in caminho)
            {
                for (int quadro = 0; ; quadro++)
                {
                    var delta = ponto - corpo.transform.position; delta.y = 0;
                    if (delta.magnitude < .05f) break;
                    if (quadro > 40) { estado.Erro = $"travou em {Fmt(corpo.transform.position)} indo para {Fmt(ponto)}"; yield break; }
                    float antes = corpo.transform.position.y;
                    corpo.Move(Vector3.ClampMagnitude(delta, .06f) + Vector3.down * .03f);
                    float subida = corpo.transform.position.y - antes;
                    if (subida > estado.MaiorSubida) { estado.MaiorSubida = subida; estado.OndeSubiu = corpo.transform.position; }
                    if (corpo.transform.position.y < -.5f) { estado.Erro = $"caiu em {Fmt(corpo.transform.position)}"; yield break; }
                    yield return null;
                }
            }
        }

        /// <summary>Busca em largura numa grade de 20 cm: chão firme embaixo e espaço livre do degrau até a cabeça.</summary>
        static List<Vector3> Caminho(CharacterController corpo, Vector3 alvo)
        {
            var origem = corpo.transform.position;
            Vector3 Mundo(Vector2Int c) => new Vector3(origem.x + c.x * Grade, 0, origem.z + c.y * Grade);
            bool Livre(Vector3 v)
            {
                if (!Physics.Raycast(v + Vector3.up * .6f, Vector3.down, out var chao, 1.2f, ~0, QueryTriggerInteraction.Ignore)) return false;
                if (chao.point.y < -.3f || chao.point.y > .5f) return false;
                var baixo = new Vector3(v.x, chao.point.y + DegrauDesejado + corpo.radius, v.z);
                var alto = new Vector3(v.x, Mathf.Max(baixo.y, chao.point.y + corpo.height - corpo.radius), v.z);
                return !Physics.OverlapCapsule(baixo, alto, corpo.radius + .04f, ~0, QueryTriggerInteraction.Ignore)
                    .Any(c => c != corpo);
            }
            var inicio = Vector2Int.zero;
            var anterior = new Dictionary<Vector2Int, Vector2Int> { [inicio] = inicio };
            var fila = new Queue<Vector2Int>(); fila.Enqueue(inicio);
            Vector2Int? fim = null;
            while (fila.Count > 0 && anterior.Count < 250000)
            {
                var c = fila.Dequeue();
                var p = Mundo(c);
                if (new Vector2(p.x - alvo.x, p.z - alvo.z).magnitude < Grade * .75f) { fim = c; break; }
                foreach (var d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                {
                    var n = c + d;
                    if (anterior.ContainsKey(n) || Mathf.Abs(n.x) > 400 || Mathf.Abs(n.y) > 400 || !Livre(Mundo(n))) continue;
                    anterior[n] = c; fila.Enqueue(n);
                }
            }
            if (fim == null) return null;
            var caminho = new List<Vector3> { alvo };
            for (var c = fim.Value; c != inicio; c = anterior[c]) caminho.Add(Mundo(c));
            caminho.Reverse();
            return caminho;
        }

        static IEnumerator Custo(ManagerMission mission, Relatorio r)
        {
            var raiz = mission.playerRoot;
            var cabeca = mission.playerHead;
            if (!raiz || !cabeca || !cabeca.GetComponent<Camera>()) { r.Info("sem câmera do modo PC; custo não medido"); yield break; }
            var cc = raiz.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            foreach (var (nome, ponto) in Ambientes)
            {
                raiz.position = ponto;
                int lotes = 0, triangulos = 0;
                for (int giro = 0; giro < 360; giro += 90)
                {
                    raiz.rotation = Quaternion.Euler(0, giro, 0);
                    cabeca.localRotation = Quaternion.identity;
                    yield return new WaitForEndOfFrame();
                    yield return new WaitForEndOfFrame();
                    lotes = Mathf.Max(lotes, UnityStats.batches);
                    triangulos = Mathf.Max(triangulos, UnityStats.triangles);
                }
                r.Info($"{nome}: até {lotes} batches, {triangulos} triângulos");
            }
        }

        static string Fmt(Vector3 v) => $"({v.x:0.##}; {v.y:0.##}; {v.z:0.##})";
    }
}
