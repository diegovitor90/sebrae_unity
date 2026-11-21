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
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
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
        static readonly Func<ManagerMission, Relatorio, IEnumerator>[] Passos = { Abertura, Sala2, MuralECafe, Organizacao, Vestigios, Guia, Seta, ForaDeOrdem,
            PortaDosFundos, Descompressao, PaineisEParedes, Simulador, Orcamento };

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

        static IEnumerator Guia(ManagerMission mission, Relatorio r)
        {
            r.Secao("Feixe");
            yield return null;
            var feixes = Object.FindObjectsByType<Feixe>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            r.Check(feixes.Length == 1, $"um feixe só na cena ({feixes.Length})");
            if (feixes.Length != 1) yield break;
            var feixe = feixes[0];
            r.Check(Vector3.Distance(feixe.transform.position, GuiaDaTrilha.Destino(mission.day.Estado())) < .01f, "o feixe está onde a guia manda");
            r.Check(mission.State != ManagerMissionState.FindManager || Vector3.Distance(feixe.transform.position, PlantaDaTrilha.FeixeDoGerente) < .01f,
                "no começo, o feixe está diante do gerente");
            var pintor = feixe.GetComponent<MeshRenderer>();
            r.Check(pintor && pintor.sharedMaterial && pintor.sharedMaterial.shader.name == "SebraeLab/Feixe", "material com o shader SebraeLab/Feixe");
            var tamanho = pintor ? pintor.bounds.size : Vector3.zero;
            r.Check(Mathf.Abs(tamanho.x - Feixe.Diametro) < .05f && Mathf.Abs(tamanho.y - Feixe.Altura) < .05f,
                $"feixe de {tamanho.x:0.##} m de diâmetro por {tamanho.y:0.##} m de altura");
            var cabecaOriginal = feixe.cabeca;
            var cabecaDeTeste = new GameObject("Cabeça de teste (trilha)").transform;
            feixe.cabeca = cabecaDeTeste;
            cabecaDeTeste.position = feixe.transform.position + Vector3.up * 1.6f;
            yield return new WaitForSeconds(.6f);
            float perto = feixe.Brilho;
            cabecaDeTeste.position = feixe.transform.position + new Vector3(4f, 1.6f, 0f);
            yield return new WaitForSeconds(.6f);
            float longe = feixe.Brilho;
            feixe.cabeca = cabecaOriginal;
            Object.Destroy(cabecaDeTeste.gameObject);
            r.Check(perto <= Feixe.BrilhoDePerto + .01f, $"de perto, o feixe esmaece (brilho {perto:0.##})");
            r.Check(longe >= Feixe.BrilhoMinimoDoPulso - .01f, $"de longe, volta ao brilho do pulso (brilho {longe:0.##})");
        }

        static IEnumerator Seta(ManagerMission mission, Relatorio r)
        {
            r.Secao("Seta do pulso");
            yield return null;
            var seta = Object.FindObjectsByType<SetaDoPulso>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            r.Check(seta && seta.name == "Left Controller", "a seta fica no controle esquerdo");
            if (!seta) yield break;
            var alvo = PlantaDaTrilha.FeixeDoGerente;
            var olho = PlantaDaTrilha.Nascimento + Vector3.up * 1.6f;
            seta.Atualizar(olho, alvo - olho, alvo);
            r.Check(!seta.Seta.gameObject.activeSelf, "olhando para o feixe, a seta some");
            seta.Atualizar(olho, Vector3.right, alvo); // olhando para leste, o gerente fica atrás
            r.Check(seta.Seta.gameObject.activeSelf, "de costas para o feixe, a seta aparece");
            var direcao = alvo - seta.Seta.position;
            direcao.y = 0f;
            r.Check(Vector3.Angle(seta.Seta.forward, direcao) < 2f, "a seta aponta para o feixe no plano do chão");
            r.Check(seta.Seta.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Haste").All(t => t.localScale.z <= SetaDoPulso.Tamanho),
                "a seta é pequena (cerca de 8 cm)");
            // No mesmo quadro: o DesktopPlayer reescreve a rotação da cabeça no Update.
            var cabeca = mission.playerHead;
            var giro = cabeca.rotation;
            var paraOFeixe = Vector3.ProjectOnPlane(Object.FindFirstObjectByType<Feixe>().transform.position - cabeca.position, Vector3.up);
            cabeca.rotation = Quaternion.LookRotation(paraOFeixe);
            r.Check(mission.day.SetaDoHud() == "", "olhando para o feixe, o HUD do PC não mostra seta");
            cabeca.rotation = Quaternion.LookRotation(-paraOFeixe);
            r.Check(mission.day.SetaDoHud().Contains("atrás"), "de costas para o feixe, o HUD do PC diz que a luz está atrás");
            cabeca.rotation = Quaternion.LookRotation(Quaternion.Euler(0f, 90f, 0f) * paraOFeixe); // virado 90° à direita, o feixe fica à esquerda
            r.Check(mission.day.SetaDoHud().Contains("esquerda"), "virado para a direita, o HUD do PC diz que a luz está à esquerda");
            cabeca.rotation = giro;
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

        /// <summary>
        /// Fotografa cada painel e placa de pontos do prédio em que uma parede opaca fica entre a pessoa e ele, ligado e
        /// desligado. Fotos diferentes: o painel aparece; cada pixel que mudou é conferido pela própria linha de visão, e só
        /// conta como falha o que está atrás de superfície opaca (atrás de vidro, o painel é visível de verdade).
        /// </summary>
        static IEnumerator PaineisEParedes(ManagerMission mission, Relatorio r)
        {
            r.Secao("Painéis e paredes");
            yield return null;

            var paineis = new List<(string nome, Bounds caixa, Action<bool> ligar, Plane plano, string shader, bool original)>();
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                         .Where(c => c.isRootCanvas && c.renderMode == RenderMode.WorldSpace && c.gameObject.activeInHierarchy))
            {
                var cantos = new Vector3[4];
                c.GetComponent<RectTransform>().GetWorldCorners(cantos);
                var caixa = new Bounds(cantos[0], Vector3.zero);
                foreach (var v in cantos) caixa.Encapsulate(v);
                var canvas = c;
                var nome = c.transform.parent && (c.name == "Frente" || c.name == "Verso") ? $"{c.transform.parent.name} • {c.name}" : c.name;
                var grafico = c.GetComponentInChildren<UnityEngine.UI.Text>();
                paineis.Add((nome, caixa, ligado => canvas.enabled = ligado, new Plane(c.transform.forward, c.transform.position),
                    grafico ? grafico.materialForRendering.shader.name : "?", c.enabled));
            }
            foreach (var t in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None).Where(t => t.gameObject.activeInHierarchy))
            {
                var render = t.GetComponent<MeshRenderer>();
                if (!render) continue;
                paineis.Add(($"{t.name} \"{t.text.Replace('\n', ' ')}\"", render.bounds, ligado => render.enabled = ligado,
                    new Plane(t.transform.forward, t.transform.position), render.sharedMaterial ? render.sharedMaterial.shader.name : "?", render.enabled));
            }
            r.Info($"{paineis.Count} painéis e placas ativos medidos");

            var objetoCamera = new GameObject("Câmera dos painéis");
            var alvo = new RenderTexture(160, 160, 24);
            var textura = new Texture2D(160, 160, TextureFormat.RGB24, false);
            try
            {
            var camera = objetoCamera.AddComponent<Camera>();
            camera.fieldOfView = 40f; camera.nearClipPlane = .05f; camera.farClipPlane = 60f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.targetTexture = alvo;
            Color32[] Foto()
            {
                RenderTexture.active = alvo;
                camera.Render();
                textura.ReadPixels(new Rect(0, 0, 160, 160), 0, 0);
                textura.Apply(false);
                RenderTexture.active = null;
                return textura.GetPixels32();
            }
            // Material do ponto atingido. Num modelo com parede e vidro na mesma malha, vale o da submalha do triângulo atingido.
            Material MaterialAtingido(RaycastHit h)
            {
                var render = h.collider.GetComponent<Renderer>();
                if (!render) return null;
                var materiais = render.sharedMaterials;
                if (materiais.Length == 1) return materiais[0];
                if (h.collider is MeshCollider malha && malha.sharedMesh && h.triangleIndex >= 0)
                {
                    int indice = h.triangleIndex * 3;
                    for (int s = 0; s < malha.sharedMesh.subMeshCount && s < materiais.Length; s++)
                    {
                        var sub = malha.sharedMesh.GetSubMesh(s);
                        if (indice >= sub.indexStart && indice < sub.indexStart + sub.indexCount) return materiais[s];
                    }
                }
                return materiais.FirstOrDefault(m => m && m.renderQueue >= 2500) ?? materiais.FirstOrDefault();
            }
            bool Opaco(RaycastHit h)
            {
                var material = MaterialAtingido(h);
                return material && material.renderQueue < 2500;
            }

            byte[] Png(Color32[] pixels)
            {
                var copia = new Texture2D(160, 160, TextureFormat.RGB24, false);
                copia.SetPixels32(pixels); copia.Apply(false);
                var png = copia.EncodeToPNG(); Object.Destroy(copia); return png;
            }
            int Diferentes(Color32[] com, Color32[] sem)
            {
                int n = 0;
                for (int i = 0; i < com.Length; i++)
                    if (Mathf.Abs(com[i].r - sem[i].r) + Mathf.Abs(com[i].g - sem[i].g) + Mathf.Abs(com[i].b - sem[i].b) > 24) n++;
                return n;
            }
            // Cada pixel que mudou tem a própria linha de visão até o plano do painel: livre, através de vidro ou através de
            // superfície opaca. O veredito e o controle negativo usam esta mesma conta.
            (int livres, int porVidro, int porOpaco) Classificar(Color32[] com, Color32[] sem, Plane plano)
            {
                int livres = 0, porVidro = 0, porOpaco = 0;
                for (int i = 0; i < com.Length; i++)
                {
                    if (Mathf.Abs(com[i].r - sem[i].r) + Mathf.Abs(com[i].g - sem[i].g) + Mathf.Abs(com[i].b - sem[i].b) <= 24) continue;
                    var raio = camera.ViewportPointToRay(new Vector3((i % 160 + .5f) / 160f, (i / 160 + .5f) / 160f, 0f));
                    if (!plano.Raycast(raio, out var ate)) continue;
                    if (!Physics.Raycast(raio, out var antes, ate - .02f, ~0, QueryTriggerInteraction.Ignore)) livres++;
                    else if (Opaco(antes)) porOpaco++;
                    else porVidro++;
                }
                return (livres, porVidro, porOpaco);
            }
            const int PixelsDeFalha = 12;

            // Controle: de frente, a 2 m e sem nada no meio, a medição precisa enxergar o painel da conversa.
            var conversa = paineis.FirstOrDefault(p => p.nome == "Conversa com o gerente");
            if (conversa.ligar != null)
            {
                var canvasDaConversa = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.name == "Conversa com o gerente");
                var olhoDeControle = conversa.caixa.center - canvasDaConversa.transform.forward * 2f;
                objetoCamera.transform.SetPositionAndRotation(olhoDeControle, Quaternion.LookRotation(conversa.caixa.center - olhoDeControle));
                conversa.ligar(true); var com = Foto();
                conversa.ligar(false); var sem = Foto();
                conversa.ligar(conversa.original);
                int vistos = Diferentes(com, sem);
                r.Check(vistos > PixelsDeFalha, $"controle: sem parede no meio, a medição enxerga o painel da conversa ({vistos} pixels mudam)");

                // Controle negativo: um cubo opaco no meio e o painel forçado a desenhar por cima de tudo (ZTest Always).
                // A mesma classificação do veredito precisa apontar o caso como texto atrás de parede opaca.
                var graficos = canvasDaConversa.GetComponentsInChildren<UnityEngine.UI.Graphic>();
                var materiaisOriginais = graficos.Select(g => g.material).ToArray();
                var forcados = new List<Material>();
                var cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubo.name = "Cubo do controle negativo (painéis)";
                try
                {
                    cubo.transform.SetPositionAndRotation(Vector3.Lerp(olhoDeControle, conversa.caixa.center, .5f), objetoCamera.transform.rotation);
                    cubo.transform.localScale = new Vector3(1.6f, 1.2f, .1f);
                    Physics.SyncTransforms();
                    bool cuboOpaco = Physics.Raycast(olhoDeControle, conversa.caixa.center - olhoDeControle, out var noCubo, 2f, ~0, QueryTriggerInteraction.Ignore)
                        && noCubo.collider.gameObject == cubo && Opaco(noCubo);
                    r.Check(cuboOpaco, "controle negativo: o cubo opaco fica entre a câmera e o painel e a medição o reconhece como opaco");
                    conversa.ligar(true); var semForcar = Foto(); conversa.ligar(false); var semPainel = Foto();
                    var normal = Classificar(semForcar, semPainel, conversa.plano);
                    r.Info($"controle negativo, material normal (UI/Default): {Diferentes(semForcar, semPainel)} pixels mudam, {normal.porOpaco} através de parede opaca");

                    // Jeito 1: a propriedade do UI/Default. Jeito 2, se o Canvas a sobrescrever: GUI/Text Shader, que tem ZTest Always fixo.
                    var jeitos = new (string nome, Func<UnityEngine.UI.Graphic, Material> material)[]
                    {
                        ("UI/Default com unity_GUIZTestMode = Always", g =>
                        {
                            var m = new Material(g.materialForRendering);
                            m.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
                            return m;
                        }),
                        ("GUI/Text Shader (ZTest Always)", g =>
                        {
                            var m = new Material(Shader.Find("GUI/Text Shader"));
                            m.mainTexture = g.mainTexture;
                            return m;
                        }),
                    };
                    bool apontou = false;
                    foreach (var jeito in jeitos)
                    {
                        for (int i = 0; i < graficos.Length; i++)
                        {
                            var m = jeito.material(graficos[i]);
                            forcados.Add(m);
                            graficos[i].material = m;
                        }
                        conversa.ligar(true); var comForcado = Foto(); conversa.ligar(false); var semForcado = Foto();
                        var conta = Classificar(comForcado, semForcado, conversa.plano);
                        r.Info($"controle negativo, {jeito.nome}: {Diferentes(comForcado, semForcado)} pixels mudam " +
                               $"({conta.livres} com visão livre, {conta.porVidro} através de vidro, {conta.porOpaco} através de parede opaca)");
                        for (int i = 0; i < graficos.Length; i++) graficos[i].material = materiaisOriginais[i];
                        if (conta.porOpaco > PixelsDeFalha) { apontou = true; break; }
                    }
                    r.Check(apontou, apontou ? "controle negativo: texto com ZTest Always atrás de um cubo opaco é apontado como atravessando parede"
                        : "controle negativo: texto forçado por cima de um cubo opaco NÃO foi apontado; a medição está cega");
                }
                finally
                {
                    for (int i = 0; i < graficos.Length; i++) if (graficos[i]) graficos[i].material = materiaisOriginais[i];
                    foreach (var m in forcados) Object.Destroy(m);
                    conversa.ligar(conversa.original);
                    cubo.SetActive(false); // Destroy só vale no fim do quadro: desligado, já não entra nos raios do veredito
                    Object.Destroy(cubo);
                    Physics.SyncTransforms();
                }
            }
            else r.Check(false, "controle: painel \"Conversa com o gerente\" não encontrado");

            var falhas = new List<string>();
            var atrasDeVidro = new HashSet<string>();
            var casosPorPainel = new Dictionary<int, int>();
            int fotos = 0;
            const int limiteDeFotos = 2400, casosPorPainelNoMaximo = 12;
            var predio = PlantaDaTrilha.Predio;
            for (float x = predio.min.x; x <= predio.max.x && fotos < limiteDeFotos; x += 1.5f)
            for (float z = predio.min.z; z <= predio.max.z && fotos < limiteDeFotos; z += 1.5f)
            {
                var pe = new Vector3(x, 0f, z);
                if (!Physics.Raycast(pe + Vector3.up * .6f, Vector3.down, out var chao, 1.2f, ~0, QueryTriggerInteraction.Ignore) || chao.point.y > .5f) continue;
                var cabeca = new Vector3(x, chao.point.y + 1.6f, z);
                if (Physics.CheckSphere(cabeca, .2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                for (int k = 0; k < paineis.Count; k++)
                {
                    var p = paineis[k];
                    var centro = p.caixa.center;
                    var vetor = centro - cabeca;
                    if (vetor.magnitude > 9f || fotos >= limiteDeFotos) continue;
                    casosPorPainel.TryGetValue(k, out var casos);
                    if (casos >= casosPorPainelNoMaximo) continue; // espalha a amostra pelo prédio todo
                    var bateu = Physics.Raycast(cabeca, vetor, out var parede, vetor.magnitude - .05f, ~0, QueryTriggerInteraction.Ignore);
                    if (!bateu) continue;
                    if (!Opaco(parede))
                    {
                        // Livre de parede opaca: o que a pessoa vê através de vidro é visível de verdade. Anota para o relatório.
                        var material = MaterialAtingido(parede);
                        if (material && material.renderQueue >= 2500 && !parede.collider.GetComponent<TextMesh>())
                            atrasDeVidro.Add($"{p.nome} atrás de {parede.collider.name} ({material.name})");
                        continue;
                    }
                    objetoCamera.transform.SetPositionAndRotation(cabeca, Quaternion.LookRotation(vetor));
                    p.ligar(true);
                    var com = Foto();
                    p.ligar(false);
                    var sem = Foto();
                    p.ligar(p.original);
                    fotos += 2;
                    casosPorPainel[k] = casos + 1;
                    int diferentes = Diferentes(com, sem);
                    if (diferentes <= PixelsDeFalha) continue;
                    // O raio do centro bateu em algo opaco, mas o painel é largo: cada pixel que mudou tem a própria linha de
                    // visão. Só conta como "através da parede" o pixel cuja primeira superfície no caminho é opaca; atrás de
                    // vidro (caixilho ao lado) o painel é visível de verdade.
                    var (livres, porVidro, porOpaco) = Classificar(com, sem, p.plano);
                    r.Info($"{p.nome} visto de {Fmt(cabeca)} com o centro atrás de {parede.collider.name}: {diferentes} pixels mudam " +
                           $"({livres} com visão livre, {porVidro} através de vidro, {porOpaco} através de parede opaca); shader {p.shader}");
                    if (porOpaco <= PixelsDeFalha) continue;
                    File.WriteAllBytes($"Logs/paineis-{falhas.Count}-com.png", Png(com));
                    File.WriteAllBytes($"Logs/paineis-{falhas.Count}-sem.png", Png(sem));
                    falhas.Add($"{p.nome} aparece através de {parede.collider.name}, visto de {Fmt(cabeca)} ({porOpaco} pixels atrás de parede opaca)");
                }
                yield return null;
            }
            r.Check(fotos > 0, $"a medição fotografou {fotos / 2} casos de parede opaca entre a pessoa e o painel, cobrindo {casosPorPainel.Count} dos {paineis.Count} painéis e placas");
            r.Check(fotos < limiteDeFotos, $"a amostra percorreu o prédio inteiro antes do limite de {limiteDeFotos} fotos");
            r.Check(falhas.Count == 0, falhas.Count == 0 ? "nenhum painel ou placa aparece através de parede opaca"
                : $"{falhas.Count} casos, por exemplo: {string.Join(" | ", falhas.Take(3))}");
            foreach (var f in falhas.Skip(3).Take(20)) r.Info(f);
            r.Info(atrasDeVidro.Count == 0 ? "nenhum painel visto através de vidro" : $"vistos através de vidro ({atrasDeVidro.Count}): {string.Join(" | ", atrasDeVidro.Take(40))}");
            }
            finally
            {
                foreach (var p in paineis) p.ligar(p.original);
                Object.Destroy(objetoCamera);
                Object.Destroy(textura);
                alvo.Release();
                Object.Destroy(alvo);
            }
        }

        /// <summary>
        /// Ata da reunião: nada avança sem a etapa anterior, peças e jogador caídos voltam, e tudo que se pega é alcançável.
        /// Roda antes da porta dos fundos, com o gerente ainda à espera e a porta trancada (LiberarDescompressao não volta atrás).
        /// </summary>
        static IEnumerator ForaDeOrdem(ManagerMission mission, Relatorio r)
        {
            r.Secao("Fora de ordem");
            yield return null;
            var day = mission.day;
            var cabeca = mission.playerHead;
            var cabecaAntes = cabeca.position;
            var gerente = Chao(mission.manager.position);
            var perto = PlantaDaTrilha.FeixeDoGerente + Vector3.up * 1.62f;
            var longe = gerente + (Chao(PlantaDaTrilha.FeixeDoGerente) - gerente).normalized * 6f + Vector3.up * 1.62f;
            r.Check(mission.State == ManagerMissionState.FindManager, $"a seção começa com o gerente à espera ({mission.State})");

            // 1. Sem aproximar, a conversa não começa.
            cabeca.position = longe;
            mission.Interact();
            cabeca.position = cabecaAntes;
            r.Check(mission.State == ManagerMissionState.FindManager, "a 6 m do gerente, interagir não começa a conversa");

            // 3. Sem o gerente, o feixe não pula etapas.
            r.Check(Vector3.Distance(GuiaDaTrilha.Destino(day.Estado()), PlantaDaTrilha.FeixeDoGerente) < .01f, "sem falar com o gerente, o feixe fica diante dele");

            // 4. Atividades fora de ordem, com o gerente à espera.
            var etapaAntes = day.Estado().Etapa;
            bool portaAntes = day.PortaDosFundosAberta;
            var copo = GameObject.Find("Copo de café");
            var pegaCopo = copo ? copo.GetComponent<XRGrabInteractable>() : null;
            r.Check(pegaCopo, "o copo de café pode ser pego");
            if (pegaCopo)
            {
                var corpo = copo.GetComponent<Rigidbody>();
                var (pos, rot) = (corpo.position, corpo.rotation);
                pegaCopo.selectEntered.Invoke(new SelectEnterEventArgs());
                bool naMao = day.Estado().CafeNaMao;
                Colocar(corpo, PlantaDaTrilha.Pires + Vector3.up * .11f, rot);
                pegaCopo.selectExited.Invoke(new SelectExitEventArgs());
                yield return null;
                yield return null;
                var etapa = day.Estado().Etapa;
                Colocar(corpo, pos, rot);
                r.Check(!naMao && etapa == etapaAntes, $"pegar o café e soltar no pires antes da hora não conta (café na mão: {naMao}; etapa {etapa})");
            }
            var corpos = Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
            foreach (var (nome, inicio, destino) in new[] {
                ("Livro", PlantaDaTrilha.InicioDosLivros[0], PlantaDaTrilha.DestinoDosLivros[0]),
                ("Cadeira", PlantaDaTrilha.InicioDasCadeiras[0], PlantaDaTrilha.DestinoDasCadeiras[0]),
                ("Notebook", PlantaDaTrilha.InicioDosNotebooks[0], PlantaDaTrilha.DestinoDosNotebooks[0]) })
            {
                var item = corpos.Where(b => b.name == nome).OrderBy(b => Vector3.Distance(b.position, inicio)).FirstOrDefault();
                var pega = item ? item.GetComponent<XRGrabInteractable>() : null;
                if (!pega) { r.Check(false, $"{nome} 1 não encontrado"); continue; }
                var (pos, rot) = (item.position, item.rotation);
                pega.selectEntered.Invoke(new SelectEnterEventArgs());
                Colocar(item, destino, Quaternion.identity);
                pega.selectExited.Invoke(new SelectExitEventArgs());
                yield return null;
                yield return null;
                var etapa = day.Estado().Etapa;
                bool entregue = item.isKinematic || !pega.enabled;
                Colocar(item, pos, rot);
                r.Check(!entregue && etapa == etapaAntes, $"soltar {nome} 1 na marca antes da organização não conta (entregue: {entregue}; etapa {etapa})");
            }
            var porta = Object.FindFirstObjectByType<HandDoor>();
            if (porta)
            {
                porta.Toggle();
                yield return new WaitForSeconds(1f);
                bool aberta = day.DoorOpen;
                cabeca.position = mission.room.transform.position + Vector3.up * 1.6f;
                bool dentro = mission.room.Contains(cabeca.position);
                mission.EvaluatePlayer();
                cabeca.position = cabecaAntes;
                yield return null;
                var etapa = day.Estado().Etapa;
                r.Check(aberta && dentro && mission.State == ManagerMissionState.FindManager && mission.CompletionCount == 0 && etapa == etapaAntes,
                    $"entrar na Sala 2 sem falar com o gerente não conclui a sala (porta aberta: {aberta}; dentro: {dentro}; {mission.State}; etapa {etapa})");
                porta.Toggle();
                yield return new WaitForSeconds(1f);
            }
            else r.Check(false, "porta da Sala 2 não encontrada");
            var lerMural = Peca("LER O MURAL");
            r.Check(lerMural && day.UseStation(lerMural), "o botão Ler o mural responde");
            yield return null;
            yield return null;
            r.Check(!day.Reading && day.Estado().Etapa == etapaAntes, $"Ler o mural antes da hora não abre o painel nem avança (etapa {day.Estado().Etapa})");
            r.Check(day.PortaDosFundosAberta == portaAntes && !day.PortaDosFundosAberta, "nenhuma ação fora de ordem abre a porta dos fundos");
            r.Check(Vector3.Distance(GuiaDaTrilha.Destino(day.Estado()), PlantaDaTrilha.FeixeDoGerente) < .01f, "depois das tentativas, o feixe continua diante do gerente");

            // 2. Sem estar perto, o diálogo não avança.
            cabeca.position = perto;
            mission.Interact();
            bool emDialogo = mission.State == ManagerMissionState.Dialogue;
            int indice = mission.DialogueIndex;
            cabeca.position = longe;
            mission.Interact();
            bool parado = mission.State == ManagerMissionState.Dialogue && mission.DialogueIndex == indice;
            cabeca.position = cabecaAntes;
            r.Check(emDialogo && parado, $"perto, a conversa começa; longe, o diálogo não avança (fala {mission.DialogueIndex}, {mission.State})");
            mission.RestartMission(); // volta à espera para as seções seguintes
            r.Check(mission.State == ManagerMissionState.FindManager, "a missão volta à espera do gerente");

            // 5. Peça perdida volta.
            var nomes = new[] { "Livro", "Cadeira", "Notebook", "Cartilha de inovação", "Protótipo" };
            var pecas = Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Where(b => nomes.Contains(b.name)).ToList();
            var inicioDasPecas = pecas.ToDictionary(b => b, b => b.position);
            foreach (var b in pecas) Colocar(b, new Vector3(b.position.x, -3f, b.position.z), b.rotation);
            yield return new WaitForSeconds(1f);
            var perdidas = pecas.Where(b => Vector3.Distance(b.position, inicioDasPecas[b]) >= .3f).Select(b => $"{b.name} em {Fmt(b.position)}").ToList();
            r.Check(pecas.Count == 8 && perdidas.Count == 0,
                $"{pecas.Count} peças jogadas para y = −3 voltaram ao lugar{(perdidas.Count == 0 ? "" : "; não voltaram: " + string.Join(", ", perdidas))}");

            // 6. Jogador caído volta.
            var raiz = mission.playerRoot;
            var controle = raiz.GetComponent<CharacterController>();
            bool ligado = controle && controle.enabled;
            if (ligado) controle.enabled = false;
            raiz.position = new Vector3(raiz.position.x, -5f, raiz.position.z);
            if (ligado) controle.enabled = true;
            yield return new WaitForSeconds(1f);
            var ondeFicou = new Vector3(mission.playerHead.position.x, raiz.position.y, mission.playerHead.position.z);
            r.Check(Vector3.Distance(ondeFicou, PlantaDaTrilha.Nascimento) < 1f, $"o jogador caído volta ao nascimento ({Fmt(ondeFicou)})");

            // 7. Tudo que se pega é alcançável: há chão livre a até 1 m do lugar de cada peça.
            foreach (var b in pecas)
                r.Check(Caminho(mission, PlantaDaTrilha.Nascimento, Chao(inicioDasPecas[b]), out var d, 1f),
                    $"{b.name} em {Fmt(inicioDasPecas[b])} é alcançável a pé ({d:0.#} m)");
        }

        static void Colocar(Rigidbody corpo, Vector3 posicao, Quaternion giro)
        {
            if (!corpo.isKinematic) { corpo.linearVelocity = Vector3.zero; corpo.angularVelocity = Vector3.zero; }
            corpo.position = posicao;
            corpo.rotation = giro;
            corpo.transform.SetPositionAndRotation(posicao, giro);
            Physics.SyncTransforms();
        }

        static IEnumerator Simulador(ManagerMission mission, Relatorio r)
        {
            r.Secao("Simulador XR");
            yield return null;
            var simulador = AcharObjeto("Simulador PC - excluido da build");
            r.Check(simulador, "o XR Device Simulator está na cena");
            r.Check(simulador && !simulador.gameObject.activeSelf && simulador.CompareTag("EditorOnly"), "desligado e fora da build");
            r.Check(AcharObjeto("Left Controller") && AcharObjeto("Right Controller"), "os dois controles existem no jogador");
            r.Check(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Samples/XR Interaction Toolkit/3.0.11/XR Device Simulator/XR Device Simulator.prefab"),
                "o prefab do simulador existe nos Samples");
        }

        static IEnumerator Orcamento(ManagerMission mission, Relatorio r)
        {
            r.Secao("Orçamento de interface");
            yield return null;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.WorldSpace).ToList();
            var graficos = canvases.Sum(c => c.GetComponentsInChildren<UnityEngine.UI.Graphic>().Count(g => g.isActiveAndEnabled));
            var luzes = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l => l.isActiveAndEnabled);
            var placas = Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None).Count(t => t.gameObject.activeInHierarchy && t.GetComponent<MeshRenderer>().enabled);
            r.Info($"Canvas de mundo ativos: {canvases.Count}; elementos de UI ativos: {graficos}; placas com texto: {placas}; luzes ativas: {luzes}");
            r.Info("Só informa. O custo de verdade se mede no Quest (ver o roteiro).");
        }

        // ------------------------------------------------------------------------------------------- Ajudantes

        /// <summary>
        /// Busca em largura numa grade de 20 cm, em quatro direções, com um corpo de 0,22 m de raio e 1,75 m de altura:
        /// chão firme embaixo e espaço livre do degrau (0,15 m) até a cabeça. Ignora gatilhos e o próprio jogador.
        /// O comprimento é o número de passos vezes 0,2 m.
        /// <c>chegada</c>: distância no chão que conta como ter chegado (padrão 0,15 m, menos que uma casa da grade).
        /// </summary>
        static bool Caminho(ManagerMission mission, Vector3 de, Vector3 ate, out float comprimento, float chegada = .15f)
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
                if (new Vector2(p.x - ate.x, p.z - ate.z).magnitude < chegada)
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
