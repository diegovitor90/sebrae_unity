using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SebraeLab.Missions;
using Object = UnityEngine.Object;

namespace SebraeLab.Editor
{
    /// <summary>
    /// Verificação em Play da trilha dentro do prédio. Pedido: Logs/trilha-check.request (conteúdo "sair" fecha o editor
    /// ao terminar) ou menu. Resultado: Logs/trilha-check.txt.
    /// </summary>
    [InitializeOnLoad]
    public static class TrilhaCheck
    {
        const string Request = "Logs/trilha-check.request";
        const string Report = "Logs/trilha-check.txt";
        const string ExitKey = "TrilhaCheck.Exit";

        sealed class Relatorio
        {
            public readonly StringBuilder Texto = new StringBuilder();
            public int Falhas;
            public void Secao(string nome) => Texto.AppendLine().AppendLine("== " + nome);
            public void Check(bool ok, string m) { if (!ok) Falhas++; Texto.AppendLine((ok ? "OK    " : "FALHA ") + m); }
            public void Info(string m) => Texto.AppendLine("      " + m);
        }

        /// <summary>Seções, na ordem em que rodam. Cada tarefa do plano acrescenta as suas.</summary>
        static readonly Func<ManagerMission, Relatorio, IEnumerator>[] Passos = { Abertura, Sala2, MuralECafe, Organizacao, Vestigios, PortaDosFundos, Descompressao };

        static TrilhaCheck() { EditorApplication.update += Poll; }

        [MenuItem("Sebrae Lab/Verificação/Trilha no prédio")]
        static void Menu() { Directory.CreateDirectory("Logs"); File.WriteAllText(Request, ""); }

        static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!EditorApplication.isPlaying)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    // O editor pode abrir sem cena (Untitled): abre a da missão antes de entrar em Play.
                    if (SceneManager.GetActiveScene().path != ManagerMissionSetup.MissionScene) EditorSceneManager.OpenScene(ManagerMissionSetup.MissionScene);
                    EditorApplication.EnterPlaymode();
                }
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
            bool concluiu = false;
            r.Texto.AppendLine("Trilha no prédio • " + DateTime.Now);
            try
            {
                foreach (var passo in Passos)
                {
                    var e = passo(mission, r);
                    while (true)
                    {
                        bool continua;
                        try { continua = e.MoveNext(); }
                        catch (Exception erro) { r.Check(false, $"{passo.Method.Name} lançou {erro.GetType().Name}: {erro.Message}"); break; }
                        if (!continua) break;
                        yield return e.Current;
                    }
                }
                concluiu = true;
            }
            finally
            {
                if (!concluiu) r.Check(false, "execução interrompida por exceção; ver o Console");
                r.Texto.AppendLine().AppendLine(r.Falhas == 0 ? "RESULTADO: OK" : $"RESULTADO: FALHA ({r.Falhas})");
                Directory.CreateDirectory("Logs");
                File.WriteAllText(Report, r.Texto.ToString());
                Debug.Log("TrilhaCheck\n" + r.Texto);
                if (SessionState.GetBool(ExitKey, false)) EditorApplication.Exit(r.Falhas == 0 ? 0 : 1);
            }
        }

        // ------------------------------------------------------------------------------------------- Seções

        static IEnumerator Abertura(ManagerMission mission, Relatorio r)
        {
            r.Secao("Abertura");
            yield return null;
            var caixa = AcharObjeto("Conversa com o gerente");
            r.Check(Distancia2D(mission.manager.position, PlantaDaTrilha.Gerente) < .05f, $"gerente em {Fmt(mission.manager.position)}");
            r.Check(caixa && Vector3.Distance(caixa.position, PlantaDaTrilha.CaixaDeConversa) < .05f,
                $"caixa de conversa em {(caixa ? Fmt(caixa.position) : "lugar nenhum")}");
            var olho = PlantaDaTrilha.Nascimento + Vector3.up * 1.62f;
            var olhar = Quaternion.Euler(0f, 270f, 0f) * Vector3.forward; // quem nasce olha para oeste
            float aGerente = GuiaDaTrilha.AnguloHorizontal(olho, olhar, mission.manager.position);
            float aCaixa = caixa ? GuiaDaTrilha.AnguloHorizontal(olho, olhar, caixa.position) : 999f;
            r.Check(Mathf.Abs(aGerente) < 30f && Mathf.Abs(aCaixa) < 30f, $"gerente ({aGerente:0}°) e caixa ({aCaixa:0}°) na frente de quem nasce");
            r.Check(aGerente > 0f && aCaixa < 0f, "gerente à direita e caixa à esquerda do centro da visão");

            bool bateu = Physics.Raycast(olho, Vector3.right, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore);
            r.Check(bateu && DaFachada(hit.collider), $"o raio para leste bate na parede transparente ({(bateu ? hit.collider.name : "nada")})");
            var mira = (new Vector3(11f, 0f, 9f) - olho).normalized;
            bool teleporte = Physics.Raycast(olho, mira, out var hitT, 15f, ~0, QueryTriggerInteraction.Ignore);
            r.Check(teleporte && DaFachada(hitT.collider), $"o raio de teletransporte para a praça para na parede ({(teleporte ? hitT.collider.name : "nada")})");
            foreach (var fora in new[] { new Vector3(12f, 0f, 7f), new Vector3(9f, 0f, 11f) })
                r.Check(!Caminho(mission, PlantaDaTrilha.Nascimento, fora, out _), $"a pé, não se chega da recepção a {Fmt(fora)}");
            var trechos = AcharObjeto(PlantaDaTrilha.NomeDaFachada);
            r.Check(trechos && trechos.GetComponentsInChildren<BoxCollider>().All(c => c.enabled && !c.isTrigger && !c.GetComponent<Renderer>()),
                "a parede da fachada só tem colisores, sem nada visível");

            // Gerente atrás de uma mesa (ata da reunião): a mesa fica entre quem nasce e o gerente, sem bater em nada, e a conversa continua possível.
            var mesa = AcharObjeto(PlantaDaTrilha.NomeDaMesaDoGerente);
            var caixaDaMesa = mesa ? Limites(mesa.gameObject) : new Bounds();
            r.Check(mesa && Distancia2D(caixaDaMesa.center, PlantaDaTrilha.MesaDoGerente) < .1f,
                $"a mesa do gerente está em {(mesa ? Fmt(caixaDaMesa.center) : "lugar nenhum")}");
            var daMesa = mesa ? Physics.OverlapBox(caixaDaMesa.center, caixaDaMesa.extents * .8f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                .Where(c => c.transform.IsChildOf(mesa) || c.transform == mesa).ToList() : new List<Collider>();
            r.Check(daMesa.Count > 0, "a mesa tem colisor (sem ele, a mesa não barra o andar: ponha um BoxCollider nela)");
            var vistaDeCima = new Ray(new Vector3(PlantaDaTrilha.Nascimento.x, caixaDaMesa.center.y, PlantaDaTrilha.Nascimento.z),
                (new Vector3(mission.manager.position.x, caixaDaMesa.center.y, mission.manager.position.z)
                    - new Vector3(PlantaDaTrilha.Nascimento.x, caixaDaMesa.center.y, PlantaDaTrilha.Nascimento.z)).normalized);
            r.Check(mesa && caixaDaMesa.IntersectRay(vistaDeCima, out var aMesa) && aMesa < Distancia2D(PlantaDaTrilha.Nascimento, mission.manager.position),
                "a mesa fica entre quem nasce e o gerente");
            float atras = caixaDaMesa.min.x - PlantaDaTrilha.Gerente.x;
            r.Check(mesa && atras > .2f && atras < .7f, $"o gerente fica {atras:0.##} m atrás da mesa (entre 0,2 e 0,7 m)");
            var outros = mesa ? Physics.OverlapBox(caixaDaMesa.center + Vector3.up * .15f, new Vector3(caixaDaMesa.extents.x, .1f, caixaDaMesa.extents.z) * .95f,
                    Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                .Where(c => !c.transform.IsChildOf(mesa) && c.transform != mesa && !c.transform.IsChildOf(mission.playerRoot)).Select(c => c.name).Distinct().ToList() : new List<string>();
            r.Check(outros.Count == 0, outros.Count == 0 ? "a mesa não invade outro móvel" : "a mesa invade: " + string.Join(", ", outros));
            var cabecaDoJogador = mission.playerHead;
            var cabecaAntes = cabecaDoJogador.position;
            cabecaDoJogador.position = PlantaDaTrilha.FeixeDoGerente + Vector3.up * 1.62f;
            bool podeConversar = mission.CanConverse();
            cabecaDoJogador.position = cabecaAntes;
            r.Check(podeConversar, "parado no feixe, diante da mesa, dá para conversar com o gerente (distância e linha de visão)");
            r.Check(Caminho(mission, PlantaDaTrilha.Nascimento, PlantaDaTrilha.FeixeDoGerente, out var ateOGerente), $"do nascimento ao feixe do gerente ({ateOGerente:0.#} m)");
            r.Check(Caminho(mission, PlantaDaTrilha.FeixeDoGerente, PlantaDaTrilha.FeixeDaPortaDaSala2, out var dali), $"do gerente à porta da Sala 2, contornando a mesa ({dali:0.#} m)");
        }

        static bool DaFachada(Collider c) => c && c.transform.parent && c.transform.parent.name == PlantaDaTrilha.NomeDaFachada;

        static IEnumerator Sala2(ManagerMission mission, Relatorio r)
        {
            r.Secao("Sala 2");
            yield return null;
            var porta = Object.FindFirstObjectByType<HandDoor>();
            r.Check(porta, "a porta da Sala 2 existe");
            if (!porta) yield break;
            r.Check(Distancia2D(porta.transform.position, PlantaDaTrilha.Dobradica) < .05f, $"dobradiça no vão da Sala A {Fmt(porta.transform.position)}");
            var fora = PlantaDaTrilha.FeixeDaPortaDaSala2;
            var dentro = PlantaDaTrilha.FeixeDoPires;
            r.Check(!Caminho(mission, fora, dentro, out _), "com a porta fechada, não se entra na Sala 2");
            porta.Toggle();
            yield return new WaitForSeconds(1f);
            var painel = porta.transform.Find("Porta • sala do estagiário");
            r.Check(porta.IsOpen, "a porta abre");
            r.Check(painel && PlantaDaTrilha.Sala2.Contains(painel.GetComponent<Renderer>().bounds.center), "aberta, a porta fica dentro da Sala A");
            r.Check(Caminho(mission, fora, dentro, out var d), $"com a porta aberta, entra-se na Sala 2 ({d:0.#} m)");
            r.Check(PlantaDaTrilha.Sala2.Contains(mission.room.transform.position + Vector3.up), "a área de conclusão fica dentro da Sala A");
            var placa = AcharPlaca("SALA 2");
            r.Check(placa && Mathf.Abs(Mathf.DeltaAngle(placa.eulerAngles.y, 270f)) < .5f, "placa SALA 2 lida por quem está no corredor");
            r.Check(mission.routeMarkers.transform.childCount == 0 || mission.routeMarkers.GetComponentsInChildren<Renderer>().Length == 0,
                "as marcas azuis da cena antiga saíram");
            porta.Toggle(); // volta fechada para as seções seguintes
            yield return new WaitForSeconds(1f);
        }

        static IEnumerator MuralECafe(ManagerMission mission, Relatorio r)
        {
            r.Secao("Mural e café");
            yield return null;
            var mural = AcharPlaca("MURAL DO CONHECIMENTO");
            r.Check(mural && Vector3.Distance(mural.position, PlantaDaTrilha.Mural) < .05f && Mathf.Abs(Mathf.DeltaAngle(mural.eulerAngles.y, 90f)) < .5f,
                "mural na parede leste, lido por quem olha para leste");
            r.Check(!GameObject.Find(PlantaDaTrilha.QuadroQueSai), "o quadro decorativo saiu do lugar do mural");
            var copo = GameObject.Find("Copo de café");
            r.Check(copo && PlantaDaTrilha.Copa.Contains(copo.transform.position), "copo de café na copa");
            r.Check(copo && Physics.Raycast(copo.transform.position, Vector3.down, out var apoio, .3f, ~0, QueryTriggerInteraction.Ignore)
                && apoio.collider.name == "Bancada Cozinha", "o copo está apoiado na bancada original da copa");
            var pires = AcharObjeto("Pires • café");
            r.Check(pires && PlantaDaTrilha.Sala2.Contains(pires.position), "pires sobre a mesa da Sala 2");
            var status = Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "Placa" && Vector3.Distance(t.transform.position, PlantaDaTrilha.PlacaDoExpediente) < .05f);
            r.Check(status, "placa do expediente na entrada do corredor");
            r.Check(AcharPlaca("MESA DE INOVAÇÃO") && AcharObjeto("Cartilha de inovação") && AcharObjeto("Protótipo"),
                "mesa de inovação sobre a Mesa Grande, com cartilha e protótipo");
        }

        static IEnumerator Organizacao(ManagerMission mission, Relatorio r)
        {
            r.Secao("Organização");
            yield return null;
            var corpos = Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var nome in new[] { "Livro", "Cadeira", "Notebook" })
            {
                var itens = corpos.Where(b => b.name == nome).ToList();
                r.Check(itens.Count == 2 && itens.All(i => PlantaDaTrilha.SalaDeAula.Contains(i.position)), $"{nome}: 2 itens, começando na sala de aula");
                var marcas = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(m => m.name == "Destino • " + nome).ToList();
                r.Check(marcas.Count == 2 && marcas.All(m => PlantaDaTrilha.SalaDeAula.Contains(m.transform.position)), $"{nome}: 2 destinos na sala de aula");
            }
            r.Check(Caminho(mission, new Vector3(1.4f, 0f, -8.1f), new Vector3(-.2f, 0f, -6.85f), out var entrada),
                $"da porta ao corredor interno, passando pelas cadeiras ({entrada:0.#} m)");
            r.Check(Caminho(mission, new Vector3(-.2f, 0f, -6.85f), new Vector3(-4.6f, 0f, -6.85f), out var interno),
                $"o corredor interno vai até a estante ({interno:0.#} m)");
            r.Check(!AcharObjeto("Ala de atividades") && !AcharObjeto("Galeria da praça"), "a ala e a galeria de fora saíram");
        }

        static IEnumerator PortaDosFundos(ManagerMission mission, Relatorio r)
        {
            r.Secao("Porta dos fundos");
            yield return null;
            var corredor = new Vector3(2.2f, 0f, -6f);
            var fundos = PlantaDaTrilha.ChegadaDosFundos;
            var botao = Peca("SALA DE DESCOMPRESSÃO");
            r.Check(!mission.day.PortaDosFundosAberta, "a porta começa trancada");
            r.Check(!Caminho(mission, corredor, fundos, out _), "trancada, não há caminho do corredor aos fundos (frestas fechadas)");
            r.Check(botao && !botao.activeSelf, "o botão da descompressão começa escondido");
            var vidro = GameObject.Find(PlantaDaTrilha.NomeDaPortaDeVidro);
            float xAntes = vidro ? Limites(vidro).center.x : float.NaN; // o pivô do FBX fica longe da porta: mede o vidro visível
            mission.day.LiberarDescompressao();
            yield return new WaitForSeconds(PlantaDaTrilha.TempoDaPortaDeVidro + .3f);
            r.Check(mission.day.PortaDosFundosAberta, "LiberarDescompressao abre a porta");
            r.Check(vidro && GameObjectUtility.GetStaticEditorFlags(vidro) == 0, "a porta de vidro não tem marca de estático (senão não desliza em Play)");
            float xDepois = vidro ? Limites(vidro).center.x : float.NaN;
            r.Check(vidro && Mathf.Abs(xAntes - xDepois - PlantaDaTrilha.CursoDaPortaDeVidro) < .05f, $"a porta de vidro deslizou para oeste (centro de {xAntes:0.##} para {xDepois:0.##})");
            r.Check(Caminho(mission, corredor, fundos, out var d), $"aberta, chega-se aos fundos a pé ({d:0.#} m)");
            r.Check(botao && botao.activeSelf, "o botão da descompressão aparece");
            r.Check(AcharPlaca("DESCOMPRESSÃO   •   ENTRADA LIBERADA"), "a placa da porta diz que a entrada foi liberada");
        }

        static IEnumerator Vestigios(ManagerMission mission, Relatorio r)
        {
            r.Secao("Vestígios");
            int erros = 0;
            void Contar(string mensagem, string pilha, LogType tipo) { if (tipo == LogType.Error || tipo == LogType.Exception) erros++; }
            Application.logMessageReceived += Contar;
            yield return new WaitForSeconds(1f);
            Application.logMessageReceived -= Contar;
            var praca = GameObject.Find("Praça do Sebrae Lab");
            bool DaPraca(Component c) => praca && c.transform.IsChildOf(praca.transform);
            var visiveis = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(x => x.enabled && !DaPraca(x) && !PlantaDaTrilha.Predio.Intersects(x.bounds)).ToList();
            r.Check(visiveis.Count == 0, $"nada visível fora do prédio além da praça ({visiveis.Count}: {string.Join(", ", visiveis.Take(5).Select(x => x.name))})");
            var solidos = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                .Where(c => c.enabled && !c.isTrigger && !DaPraca(c) && !PlantaDaTrilha.Predio.Intersects(c.bounds)).ToList();
            r.Check(solidos.Count == 0, $"nenhum colisor fora do prédio além da praça ({solidos.Count}: {string.Join(", ", solidos.Take(5).Select(c => c.name))})");
            var raiz = AcharObjeto("Descompressão (provisória)");
            r.Check(raiz && !raiz.gameObject.activeSelf, "a descompressão fica desligada durante a trilha");
            r.Check(erros == 0, $"nenhum erro no Console com a descompressão desligada ({erros})");
        }

        static IEnumerator Descompressao(ManagerMission mission, Relatorio r)
        {
            r.Secao("Descompressão");
            mission.day.LiberarDescompressao();
            yield return new WaitForSeconds(PlantaDaTrilha.TempoDaPortaDeVidro + .3f);
            var raiz = GameObject.Find("Descompressão (provisória)");
            r.Check(raiz, "a descompressão liga quando a porta dos fundos abre");
            if (!raiz) yield break;
            r.Check(!PlantaDaTrilha.Predio.Intersects(Limites(raiz)), "a descompressão fica fora do prédio, atrás da parede sul");
            var pista = Object.FindFirstObjectByType<BowlingLane>();
            r.Check(pista && pista.Pista && pista.Pista.transform.IsChildOf(raiz.transform), "a pista de boliche está dentro da descompressão");
            var placas = Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None).Where(t => t.name == "Placa" && t.transform.IsChildOf(raiz.transform)).ToList();
            r.Check(placas.Count >= 5 && placas.All(t => Mathf.Abs(Mathf.DeltaAngle(t.transform.eulerAngles.y, PlantaDaTrilha.GuinadaDaDescompressao)) < .5f),
                $"as {placas.Count} placas da descompressão continuam a 90°");
            var botao = Peca("SALA DE DESCOMPRESSÃO");
            r.Check(botao && mission.day.UseStation(botao), "o botão da descompressão responde");
            yield return null;
            yield return null;
            r.Check(Distancia2D(mission.playerHead.position, PlantaDaTrilha.EntradaDaDescompressao) < .6f, $"o botão leva à descompressão {Fmt(mission.playerHead.position)}");
            var voltar = Peca("VOLTAR AO PRÉDIO");
            r.Check(voltar && mission.day.UseStation(voltar), "o botão Voltar ao prédio responde");
            yield return null;
            yield return null;
            r.Check(Distancia2D(mission.playerHead.position, PlantaDaTrilha.ChegadaDosFundos) < .6f, "Voltar ao prédio leva aos fundos");
        }

        // ------------------------------------------------------------------------------------------- Ajudantes

        /// <summary>
        /// Busca em largura numa grade de 20 cm, em quatro direções, com um corpo de 0,22 m de raio e 1,75 m de altura:
        /// chão firme embaixo e espaço livre do degrau (0,15 m) até a cabeça. Ignora gatilhos e o próprio jogador.
        /// O comprimento é o número de passos vezes 0,2 m.
        /// </summary>
        static bool Caminho(ManagerMission mission, Vector3 de, Vector3 ate, out float comprimento)
        {
            const float grade = .2f, raio = .22f, altura = 1.75f, degrau = .15f;
            var jogador = mission.playerRoot;
            bool DoJogador(Component c) => jogador && c.transform.IsChildOf(jogador);
            bool Livre(Vector3 v)
            {
                if (!Physics.Raycast(v + Vector3.up * .6f, Vector3.down, out var chao, 1.2f, ~0, QueryTriggerInteraction.Ignore)) return false;
                if (chao.point.y < -.3f || chao.point.y > .5f || DoJogador(chao.collider)) return false;
                var baixo = new Vector3(v.x, chao.point.y + degrau + raio, v.z);
                var alto = new Vector3(v.x, chao.point.y + altura - raio, v.z);
                return !Physics.OverlapCapsule(baixo, alto, raio, ~0, QueryTriggerInteraction.Ignore).Any(c => !DoJogador(c));
            }
            Vector3 Mundo(Vector2Int c) => new Vector3(de.x + c.x * grade, 0f, de.z + c.y * grade);
            var anterior = new Dictionary<Vector2Int, Vector2Int> { [Vector2Int.zero] = Vector2Int.zero };
            var fila = new Queue<Vector2Int>();
            fila.Enqueue(Vector2Int.zero);
            while (fila.Count > 0)
            {
                var c = fila.Dequeue();
                var p = Mundo(c);
                if (new Vector2(p.x - ate.x, p.z - ate.z).magnitude < grade * .75f)
                {
                    int passos = 0;
                    for (var k = c; k != Vector2Int.zero; k = anterior[k]) passos++;
                    comprimento = passos * grade;
                    return true;
                }
                foreach (var d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                {
                    var n = c + d;
                    if (anterior.ContainsKey(n) || Mathf.Abs(n.x) > 150 || Mathf.Abs(n.y) > 150 || !Livre(Mundo(n))) continue;
                    anterior[n] = c;
                    fila.Enqueue(n);
                }
            }
            comprimento = 0f;
            return false;
        }

        /// <summary>Objeto da cena pelo nome exato, ativo ou não.</summary>
        static Transform AcharObjeto(string nome) =>
            Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == nome);

        /// <summary>Placa (TextMesh raiz de uma Placa) pelo texto exato, ativa ou não.</summary>
        static Transform AcharPlaca(string texto) =>
            Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.name == "Placa" && t.text == texto).Select(t => t.transform).FirstOrDefault();

        /// <summary>Peça de um botão de missão (pai das duas faces) pela legenda, ativa ou não.</summary>
        static GameObject Peca(string legenda) =>
            Object.FindObjectsByType<MissionButton>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(b => b.Peca).FirstOrDefault(p => p && p.name == legenda);

        static Vector3 Chao(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static float Distancia2D(Vector3 a, Vector3 b) => Vector3.Distance(Chao(a), Chao(b));

        static Bounds Limites(GameObject raiz)
        {
            var renderers = raiz.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(raiz.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var x in renderers) b.Encapsulate(x.bounds);
            return b;
        }

        static string Fmt(Vector3 v) => $"({v.x:0.##}; {v.y:0.##}; {v.z:0.##})";
    }
}
