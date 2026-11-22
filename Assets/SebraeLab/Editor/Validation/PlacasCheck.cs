using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using SebraeLab.Missions;
using Object = UnityEngine.Object;

namespace SebraeLab.Editor
{
    /// <summary>
    /// Verificação em Play das placas e dos botões da missão: paredes escondem o texto, nada gira com a cabeça,
    /// o texto fica numa caixa legível dos dois lados e os botões têm duas faces. As provas visuais usam uma câmera
    /// de teste que renderiza uma cópia da placa longe da cena. Pedido: Logs/placas-check.request (conteúdo "sair"
    /// fecha o editor ao terminar) ou menu. Resultado: Logs/placas-check.txt.
    /// </summary>
    [InitializeOnLoad]
    public static class PlacasCheck
    {
        const string Request = "Logs/placas-check.request";
        const string Report = "Logs/placas-check.txt";
        const string ExitKey = "PlacasCheck.Exit";
        const string TextoDeReferencia = "MESA DE INOVAÇÃO";
        static readonly Vector3 LugarDoTeste = new Vector3(500f, 50f, 500f);

        sealed class Relatorio
        {
            public readonly StringBuilder Texto = new StringBuilder();
            public int Falhas;
            public void Secao(string nome) => Texto.AppendLine().AppendLine("== " + nome);
            public void Check(bool ok, string m) { if (!ok) Falhas++; Texto.AppendLine((ok ? "OK    " : "FALHA ") + m); }
            public void Info(string m) => Texto.AppendLine("      " + m);
        }

        static PlacasCheck() { EditorApplication.update += Poll; }

        [MenuItem("Sebrae Lab/Verificação/Placas")]
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
            bool concluiu = false;
            r.Texto.AppendLine("Placas • " + DateTime.Now);
            try
            {
                // A descompressão só liga quando a porta dos fundos abre; sem ela, os placares do boliche e do arco ficam
                // inativos e nunca ajustam a caixa ao texto.
                mission.day.LiberarDescompressao();
                yield return new WaitForSeconds(PlantaDaTrilha.TempoDaPortaDeVidro + .3f);
                // Passos executados no mesmo iterador: uma exceção chega ao finally e o relatório sai mesmo assim.
                foreach (var passo in new Func<ManagerMission, Relatorio, IEnumerator>[] { Desenho, Paredes, Parada, Caixa, Mural, DoisLados, OutroLado, Botoes, Memoria })
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
                Debug.Log("PlacasCheck\n" + r.Texto);
                if (SessionState.GetBool(ExitKey, false)) EditorApplication.Exit(r.Falhas == 0 ? 0 : 1);
            }
        }

        /// <summary>Custo de desenho na recepção e na ala, só como registro (antes e depois da etapa).</summary>
        static IEnumerator Desenho(ManagerMission mission, Relatorio r)
        {
            r.Secao("Desenho (informativo)");
            for (int i = 0; i < 10; i++) yield return null;
            r.Info($"na recepção: {UnityStats.batches} lotes, {UnityStats.drawCalls} chamadas de desenho, {UnityStats.setPassCalls} trocas de material");
            var jogador = mission.playerRoot;
            var controle = jogador.GetComponent<CharacterController>();
            var posicao = jogador.position;
            var rotacao = jogador.rotation;
            if (controle) controle.enabled = false;
            jogador.SetPositionAndRotation(new Vector3(2.2f, 0f, -6f), Quaternion.Euler(0f, 180f, 0f));
            for (int i = 0; i < 10; i++) yield return null;
            r.Info($"no corredor sul: {UnityStats.batches} lotes, {UnityStats.drawCalls} chamadas de desenho, {UnityStats.setPassCalls} trocas de material");
            jogador.SetPositionAndRotation(posicao, rotacao);
            if (controle) controle.enabled = true;
            yield return null;
        }

        static IEnumerator Paredes(ManagerMission mission, Relatorio r)
        {
            r.Secao("Paredes");
            var original = PlacaDeReferencia();
            r.Check(original, $"placa \"{TextoDeReferencia}\" encontrada na cena");
            if (!original) yield break;
            var copia = CopiarParaOTeste(original);
            yield return null;
            int semParede = ContarBrancos(Renderizar(false));

            // Mesma placa com o material padrão da fonte: a forma do texto tem de ser a mesma (atlas da fonte certo).
            var renderer = copia.GetComponent<MeshRenderer>();
            var proprio = renderer.sharedMaterial;
            renderer.sharedMaterial = copia.GetComponent<TextMesh>().font.material;
            int referencia = ContarBrancos(Renderizar(false));
            renderer.sharedMaterial = proprio;

            var parede = Parede();
            int comParede = ContarBrancos(Renderizar(false));
            Object.Destroy(parede.GetComponent<Renderer>().sharedMaterial);
            Object.Destroy(parede);
            Object.Destroy(copia);

            r.Check(semParede > 20, $"sem parede, o texto aparece ({semParede} pixels brancos)");
            r.Check(Mathf.Abs(semParede - referencia) <= referencia * .1f,
                $"o texto tem a mesma forma do material padrão da fonte ({semParede} e {referencia} pixels brancos)");
            r.Check(comParede == 0, $"atrás de uma parede, o texto some ({comParede} pixels brancos)");
        }

        static readonly (string nome, float guinada)[] BotoesDaMissao =
        {
            ("LER O MURAL", 90f), ("ENTENDIDO", 90f), ("SALA DE DESCOMPRESSÃO", 180f),
            ("REARMAR BOLICHE", 90f), ("VOLTAR AO PRÉDIO", 90f), ("RECOMEÇAR O DIA", 90f)
        };

        static IEnumerator Parada(ManagerMission mission, Relatorio r)
        {
            r.Secao("Parada");
            yield return null;
            var placas = Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(t => t.name == "Placa").Select(t => t.transform).ToList();
            var botoes = Object.FindObjectsByType<MissionButton>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(b => b.GetComponentInParent<Canvas>(true).transform).Distinct().ToList();
            var todos = placas.Concat(botoes).ToList();
            var antes = todos.Select(t => t.rotation).ToList();
            var cabeca = mission.playerHead;
            var posicao = cabeca.position;
            var rotacao = cabeca.rotation;
            // Duas mudanças de cabeça em direções bem diferentes: um objeto que gira só numa delas ainda é pego,
            // e um objeto que por acaso já olhava para a cabeça numa delas não passa por parado.
            var giraram = new bool[todos.Count];
            var mudancas = new[]
            {
                (new Vector3(3f, 0f, 3f), Quaternion.Euler(0f, 90f, 0f)),
                (new Vector3(-4f, 0f, 2f), Quaternion.Euler(0f, -60f, 0f)),
            };
            foreach (var (deslocamento, giro) in mudancas)
            {
                cabeca.SetPositionAndRotation(posicao + deslocamento, rotacao * giro);
                yield return null;
                yield return null;
                for (int i = 0; i < todos.Count; i++)
                    if (Quaternion.Angle(antes[i], todos[i].rotation) > .01f) giraram[i] = true;
            }
            cabeca.SetPositionAndRotation(posicao, rotacao);
            int giraramTotal = giraram.Count(g => g);

            r.Check(placas.Count > 0 && botoes.Count > 0, $"{placas.Count} placas e {botoes.Count} botões na cena (inativos incluídos)");
            r.Check(giraramTotal == 0, $"nada gira com a cabeça, em duas mudanças diferentes ({giraramTotal} de {todos.Count} giraram)");
            int foraDoEixo = placas.Count(t => Mathf.Abs(Mathf.DeltaAngle(t.eulerAngles.y, Mathf.Round(t.eulerAngles.y / 90f) * 90f)) > .5f);
            r.Check(foraDoEixo == 0, $"toda placa parada num eixo (0°, 90°, 180° ou 270°): {foraDoEixo} fora");

            // Convenção do projeto: a placa é lida por quem olha no sentido do seu próprio +Z, então a frente
            // (forward) aponta para longe de quem lê.
            var primeiroDia = placas.FirstOrDefault(t => t.GetComponent<TextMesh>().text.StartsWith("PRIMEIRO DIA"));
            r.Check(primeiroDia && Mathf.Abs(Mathf.DeltaAngle(primeiroDia.eulerAngles.y, 270f)) < .5f,
                $"PRIMEIRO DIA a 270°, lida por quem nasce a leste olhando o gerente ({(primeiroDia ? primeiroDia.eulerAngles.y : float.NaN):0.#}°)");
            r.Check(primeiroDia && Vector3.Dot(primeiroDia.forward, primeiroDia.position - new Vector3(5.7f, 1.62f, 7.1f)) > 0f,
                "quem nasce na recepção lê PRIMEIRO DIA pela frente");
            var sala2 = placas.FirstOrDefault(t => t.GetComponent<TextMesh>().text == "SALA 2");
            r.Check(sala2 && Mathf.Abs(Mathf.DeltaAngle(sala2.eulerAngles.y, 270f)) < .5f,
                $"SALA 2 a 270°, sobre a porta ({(sala2 ? sala2.eulerAngles.y : float.NaN):0.#}°)");
            r.Check(sala2 && Vector3.Dot(sala2.forward, Vector3.left) > .99f, "quem está no corredor olhando para oeste lê SALA 2 pela frente");

            foreach (var (nome, guinada) in BotoesDaMissao)
            {
                // O botão da missão é uma peça (o pai dos Canvas, com o nome da legenda); botão solto usa o próprio Canvas.
                var botao = botoes.Select(PecaDoBotao).FirstOrDefault(t => t.name == nome);
                r.Check(botao && Mathf.Abs(Mathf.DeltaAngle(botao.eulerAngles.y, guinada)) < .5f,
                    $"botão {nome} existe e está a {guinada:0}° ({(botao ? botao.eulerAngles.y : float.NaN):0.#}°)");
            }
        }

        /// <summary>Peça do botão (pai dos Canvas das duas faces) ou, em botão que não é peça, o próprio Canvas.</summary>
        static Transform PecaDoBotao(Transform canvas) => canvas.parent && canvas.parent.Find("Corpo") ? canvas.parent : canvas;

        static IEnumerator Caixa(ManagerMission mission, Relatorio r)
        {
            r.Secao("Caixa");
            yield return null;
            var raizes = Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == "Placa").ToList();
            int semCaixa = 0, pequenas = 0, comColisor = 0, outroMaterial = 0;
            var nomesDasPequenas = new System.Collections.Generic.List<string>();
            foreach (var t in raizes)
            {
                var corpo = t.transform.Find("Corpo");
                var moldura = t.transform.Find("Moldura");
                var verso = t.transform.Find("Verso");
                if (!corpo || !moldura || !verso) { semCaixa++; continue; }
                var medida = MedidaLocal(t);
                int linhas = Mathf.Max(1, t.text.Split('\n').Length);
                float margem = .5f * medida.size.y / linhas;
                if (corpo.localScale.x < medida.size.x + 1.8f * margem || corpo.localScale.y < medida.size.y + 1.8f * margem)
                { pequenas++; nomesDasPequenas.Add($"\"{t.text.Replace("\n", " / ")}\" ({corpo.localScale.x:0.###}x{corpo.localScale.y:0.###}, texto {medida.size.x:0.###}x{medida.size.y:0.###})"); }
                if (corpo.GetComponent<Collider>() || moldura.GetComponent<Collider>()) comColisor++;
                if (!corpo.GetComponent<Renderer>().sharedMaterial.name.StartsWith("PlacaCorpo")
                    || !moldura.GetComponent<Renderer>().sharedMaterial.name.StartsWith("PlacaMoldura")) outroMaterial++;
            }
            var inativas = raizes.Where(t => !t.gameObject.activeInHierarchy && t.transform.Find("Corpo")).ToList();
            var comCaixa = raizes.Where(t => t.transform.Find("Corpo")).Select(t => t.transform.Find("Corpo").localScale).ToList();
            if (comCaixa.Count > 0)
                r.Info($"{inativas.Count} placas inativas na hierarquia; menor caixa {comCaixa.Min(c => c.x):0.###} x {comCaixa.Min(c => c.y):0.###} m"
                    + (inativas.Count > 0 ? $"; caixas das inativas: {string.Join(", ", inativas.Select(t => $"{t.transform.Find("Corpo").localScale.x:0.###}x{t.transform.Find("Corpo").localScale.y:0.###}"))}" : ""));
            r.Check(raizes.Count > 0 && semCaixa == 0, $"{raizes.Count - semCaixa} de {raizes.Count} placas com corpo, moldura e verso");
            r.Check(pequenas == 0, $"a caixa cobre o texto com margem em todas ({pequenas} pequenas demais{(pequenas > 0 ? ": " + string.Join("; ", nomesDasPequenas) : "")})");
            r.Check(comColisor == 0, $"caixas sem colisor ({comColisor} com colisor)");
            r.Check(outroMaterial == 0, $"corpo PlacaCorpo e moldura PlacaMoldura em todas ({outroMaterial} com outro material)");
        }

        static readonly Vector3 AreaDeCliqueDoMural = new Vector3(2.1f, .8f, .1f);

        /// <summary>O clique do PC no mural: o colisor nunca fica menor que a área antiga, e cobre a caixa quando ela cresce.</summary>
        static IEnumerator Mural(ManagerMission mission, Relatorio r)
        {
            r.Secao("Mural");
            yield return null;
            var texto = Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "Placa" && t.text == "MURAL DO CONHECIMENTO");
            r.Check(texto, "placa \"MURAL DO CONHECIMENTO\" encontrada na cena");
            if (!texto) yield break;
            var colisor = texto.GetComponent<BoxCollider>();
            r.Check(colisor, "o mural tem colisor de clique");
            if (!colisor) yield break;
            var corpo = texto.transform.Find("Corpo");
            r.Check(corpo && corpo.localScale.x < AreaDeCliqueDoMural.x && corpo.localScale.y < AreaDeCliqueDoMural.y,
                $"com o título, a caixa é menor que a área de clique ({(corpo ? corpo.localScale.x : 0f):0.###} x {(corpo ? corpo.localScale.y : 0f):0.###} m)");
            r.Check(colisor.size.x >= AreaDeCliqueDoMural.x - .001f && colisor.size.y >= AreaDeCliqueDoMural.y - .001f && colisor.size.z >= AreaDeCliqueDoMural.z - .001f,
                $"o colisor do mural não fica menor que 2,1 x 0,8 x 0,1 m ({colisor.size.x:0.###} x {colisor.size.y:0.###} x {colisor.size.z:0.###})");

            var copia = CopiarParaOTeste(texto.gameObject);
            yield return null;
            var linha = "UMA LINHA LONGA DO MURAL COM MUITO TEXTO PARA PASSAR DA ÁREA DE CLIQUE ANTIGA, LARGA DEMAIS PARA O PAINEL";
            copia.GetComponent<TextMesh>().text = string.Join("\n", Enumerable.Repeat(linha, 14));
            yield return new WaitForSeconds(.4f);
            var colisorDaCopia = copia.GetComponent<BoxCollider>();
            var corpoDaCopia = copia.transform.Find("Corpo");
            r.Check(colisorDaCopia && corpoDaCopia, "a cópia do mural tem colisor e corpo");
            if (colisorDaCopia && corpoDaCopia)
            {
                var caixa = corpoDaCopia.localScale;
                var tamanho = colisorDaCopia.size;
                r.Check(caixa.x > AreaDeCliqueDoMural.x && caixa.y > AreaDeCliqueDoMural.y,
                    $"com texto longo, a caixa passa da área antiga ({caixa.x:0.###} x {caixa.y:0.###} m)");
                r.Check(tamanho.x >= caixa.x - .001f && tamanho.y >= caixa.y - .001f, $"com texto longo, o colisor cobre a caixa ({tamanho.x:0.###} x {tamanho.y:0.###} m)");
                r.Check(tamanho.x >= AreaDeCliqueDoMural.x - .001f && tamanho.y >= AreaDeCliqueDoMural.y - .001f && tamanho.z >= AreaDeCliqueDoMural.z - .001f,
                    "com texto longo, o colisor continua no mínimo da área antiga");
            }
            Object.Destroy(copia);
        }

        static IEnumerator DoisLados(ManagerMission mission, Relatorio r)
        {
            r.Secao("Dois lados");
            var original = PlacaDeReferencia();
            if (!original) { r.Check(false, $"placa \"{TextoDeReferencia}\" encontrada na cena"); yield break; }
            var copia = CopiarParaOTeste(original);
            yield return null;
            var frente = copia.GetComponent<TextMesh>();
            var verso = copia.transform.Find("Verso") ? copia.transform.Find("Verso").GetComponent<TextMesh>() : null;
            var corpo = copia.transform.Find("Corpo");
            r.Check(verso && corpo, "a placa tem verso e corpo");
            if (!verso || !corpo) { Object.Destroy(copia); yield break; }
            r.Check(verso.text == frente.text && Mathf.Abs(Mathf.DeltaAngle(verso.transform.localEulerAngles.y, 180f)) < .5f,
                "o verso tem o mesmo texto, girado 180°");
            float alturaAntes = corpo.localScale.y;
            frente.text = "UMA LINHA\nDUAS LINHAS\nTRÊS LINHAS";
            yield return null;
            yield return null;
            r.Check(corpo.localScale.y > alturaAntes * 1.5f, $"a caixa cresce logo que o texto muda (altura de {alturaAntes:0.###} para {corpo.localScale.y:0.###} m em 2 quadros)");
            // O verso é conferido a cada 0,2 s por placa: 0,4 s cobre a defasagem máxima com folga.
            yield return new WaitForSeconds(.4f);
            r.Check(verso.text == frente.text, "o verso acompanha quando o texto da frente muda (em até 0,4 s)");
            r.Check(corpo.localScale.y > alturaAntes * 1.5f, $"a caixa continua do tamanho do texto (altura de {alturaAntes:0.###} para {corpo.localScale.y:0.###} m)");
            int deTras = ContarBrancos(Renderizar(true));
            r.Check(deTras > 20, $"por trás, o verso aparece ({deTras} pixels brancos)");
            Object.Destroy(copia);
        }

        static IEnumerator OutroLado(ManagerMission mission, Relatorio r)
        {
            r.Secao("Outro lado");
            var original = PlacaDeReferencia();
            if (!original) { r.Check(false, $"placa \"{TextoDeReferencia}\" encontrada na cena"); yield break; }
            var copia = CopiarParaOTeste(original);
            yield return null;
            var verso = copia.transform.Find("Verso");
            if (!verso) { r.Check(false, "a placa tem verso"); Object.Destroy(copia); yield break; }
            var comVerso = Renderizar(false);
            verso.gameObject.SetActive(false);
            var semVerso = Renderizar(false);
            int diferentes = Diferentes(comVerso, semVerso);
            r.Check(diferentes == 0, $"da frente, o texto do verso não aparece ({diferentes} pixels mudam com e sem ele)");
            Object.Destroy(copia);
        }

        static IEnumerator Botoes(ManagerMission mission, Relatorio r)
        {
            r.Secao("Botões");
            int apertos = 0, apertosBloqueado = 0;
            var pai = new GameObject("Teste dos botões (placas)").transform;
            MissionButton.Criar("TESTE", pai, LugarDoTeste, new Vector2(1.1f, .3f), () => apertos++);
            var missoes = Object.FindFirstObjectByType<MissionManager>();
            var paiBloqueado = new GameObject("Teste do botão bloqueado (placas)").transform;
            MissionButton.Criar("TESTE BLOQUEADO", paiBloqueado, LugarDoTeste + Vector3.up * 3f, new Vector2(1.1f, .3f),
                () => apertosBloqueado++, missoes, EtapaExpediente.Descompressao);
            yield return null;

            var peca = pai.childCount == 1 ? pai.GetChild(0) : null;
            r.Check(peca && peca.Find("Corpo") && peca.Find("Moldura") && peca.Find("Frente") && peca.Find("Verso") && peca.GetComponent<BoxCollider>(),
                "o botão é uma peça com corpo, moldura, frente, verso e colisor");
            var faces = pai.GetComponentsInChildren<MissionButton>();
            r.Check(faces.Length == 2, $"o botão tem duas faces ({faces.Length})");
            foreach (var face in faces) face.GetComponent<Button>().onClick.Invoke();
            r.Check(faces.Length == 2 && apertos == 2, $"cada face dispara a ação uma vez ({apertos} disparos em {faces.Length} faces)");
            r.Check(faces.All(f => f.GetComponentInParent<TrackedDeviceGraphicRaycaster>().ignoreReversedGraphics),
                "cada face ignora o raio que chega pelas costas");

            var facesBloqueadas = paiBloqueado.GetComponentsInChildren<MissionButton>();
            foreach (var face in facesBloqueadas) face.GetComponent<Button>().onClick.Invoke();
            r.Check(facesBloqueadas.Length == 2 && facesBloqueadas.All(f => f.Bloqueado) && apertosBloqueado == 0,
                $"o bloqueio por etapa vale nas duas faces ({facesBloqueadas.Count(f => f.Bloqueado)} bloqueadas, {apertosBloqueado} disparos)");

            if (peca && peca.Find("Verso"))
            {
                var comVerso = Renderizar(false);
                peca.Find("Verso").gameObject.SetActive(false);
                var semVerso = Renderizar(false);
                int diferentes = Diferentes(comVerso, semVerso);
                r.Check(diferentes == 0, $"da frente, a face do verso não aparece ({diferentes} pixels mudam com e sem ela)");
            }
            Object.Destroy(pai.gameObject);
            Object.Destroy(paiBloqueado.gameObject);
            yield return null; // Destroy só acontece no fim do quadro: sem esperar, os botões de teste entrariam na contagem

            // Os 6 botões da missão (Station): cada um numa peça com duas faces.
            var pecasDaMissao = Object.FindObjectsByType<MissionButton>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(b => b.GetComponentInParent<Canvas>(true).transform.parent)
                .Where(p => p && p.Find("Corpo"))
                .GroupBy(p => p)
                .ToList();
            r.Check(pecasDaMissao.Count == 6 && pecasDaMissao.All(g => g.Count() == 2),
                $"os 6 botões da missão têm duas faces ({pecasDaMissao.Count} peças, {pecasDaMissao.Count(g => g.Count() == 2)} com duas faces)");
        }

        /// <summary>Limite de lixo gerado pelas placas, em bytes por quadro (todas juntas), definido a partir da medição do código antigo.</summary>
        const float LimiteDeLixoPorQuadro = 800f;

        /// <summary>
        /// Lixo por quadro das placas. Medir o quadro inteiro com e sem as placas não serve: a cena já gera de 18 a 24 KB por
        /// quadro e a variação entre rodadas (vários KB) é maior que o custo das placas. Por isso o LateUpdate de cada placa é
        /// chamado direto, em laço, e conta-se o crescimento da memória gerenciada em uso (as quedas, que são coletas de
        /// lixo, são ignoradas). GC.GetAllocatedBytesForCurrentThread devolve 0 no Mono do Unity.
        /// </summary>
        static IEnumerator Memoria(ManagerMission mission, Relatorio r)
        {
            r.Secao("Memória");
            for (int i = 0; i < 10; i++) yield return null; // as cópias das seções anteriores já saíram da cena
            var metodo = typeof(Placa).GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var placas = Object.FindObjectsByType<Placa>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(p => p.gameObject.activeInHierarchy).ToList();
            r.Check(metodo != null && placas.Count > 0, $"{placas.Count} placas ativas e LateUpdate encontrado");
            if (metodo == null || placas.Count == 0) yield break;
            var chamadas = placas.Select(p => (Action)Delegate.CreateDelegate(typeof(Action), p, metodo)).ToArray();
            foreach (var chamada in chamadas) chamada(); // primeira chamada de cada placa fora da medida
            const int quadros = 200;
            long cresceu = 0;
            for (int q = 0; q < quadros; q++)
            {
                long antes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                foreach (var chamada in chamadas) chamada();
                long depois = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                if (depois > antes) cresceu += depois - antes;
            }
            float porQuadro = cresceu / (float)quadros;
            r.Info($"{placas.Count} placas: {porQuadro:0} B de lixo por quadro ({quadros} quadros simulados)");
            r.Check(porQuadro < LimiteDeLixoPorQuadro, $"as placas geram pouco lixo por quadro ({porQuadro:0} B, limite {LimiteDeLixoPorQuadro:0})");
        }

        /// <summary>Caixa do texto da frente no espaço da raiz da placa (rotação em múltiplos de 90°).</summary>
        static Bounds MedidaLocal(TextMesh texto)
        {
            var mundo = texto.GetComponent<MeshRenderer>().bounds;
            var tamanho = texto.transform.InverseTransformVector(mundo.size);
            return new Bounds(texto.transform.InverseTransformPoint(mundo.center),
                new Vector3(Mathf.Abs(tamanho.x), Mathf.Abs(tamanho.y), Mathf.Abs(tamanho.z)));
        }

        // Ajudantes ---------------------------------------------------------------------------------------

        static GameObject PlacaDeReferencia()
        {
            var texto = Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "Placa" && t.text == TextoDeReferencia);
            return texto ? texto.gameObject : null;
        }

        /// <summary>Cópia da placa longe da cena, sem girar: lida por quem olha no sentido +Z.</summary>
        static GameObject CopiarParaOTeste(GameObject original)
        {
            var copia = Object.Instantiate(original);
            copia.name = "Placa de teste";
            copia.transform.SetParent(null);
            copia.transform.SetPositionAndRotation(LugarDoTeste, Quaternion.identity);
            copia.transform.localScale = Vector3.one;
            return copia;
        }

        /// <summary>Parede opaca entre a câmera da frente (2,5 m antes) e o lugar do teste.</summary>
        static GameObject Parede()
        {
            var parede = GameObject.CreatePrimitive(PrimitiveType.Cube);
            parede.name = "Parede de teste (placas)";
            parede.transform.position = LugarDoTeste + Vector3.back;
            parede.transform.localScale = new Vector3(6f, 6f, .1f);
            parede.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Unlit/Color")) { color = new Color(.25f, .25f, .25f) };
            return parede;
        }

        /// <summary>Renderiza o lugar do teste de frente (câmera em -Z olhando +Z) ou por trás e devolve os pixels.</summary>
        static Color32[] Renderizar(bool deTras)
        {
            const int lado = 256;
            var camera = new GameObject("Câmera de teste (placas)").AddComponent<Camera>();
            camera.enabled = false;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 10f;
            camera.fieldOfView = 60f;
            var sentido = deTras ? Vector3.forward : Vector3.back;
            camera.transform.SetPositionAndRotation(LugarDoTeste + sentido * 2.5f, Quaternion.LookRotation(-sentido));
            var alvo = new RenderTexture(lado, lado, 24);
            camera.targetTexture = alvo;
            camera.Render();
            var anterior = RenderTexture.active;
            RenderTexture.active = alvo;
            var imagem = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
            imagem.ReadPixels(new Rect(0, 0, lado, lado), 0, 0);
            imagem.Apply();
            RenderTexture.active = anterior;
            var pixels = imagem.GetPixels32();
            camera.targetTexture = null;
            alvo.Release();
            Object.Destroy(alvo);
            Object.Destroy(imagem);
            Object.Destroy(camera.gameObject);
            return pixels;
        }

        /// <summary>Pixels quase brancos: o texto é branco; o corpo é azul, a moldura dourada e o fundo preto.</summary>
        static int ContarBrancos(Color32[] pixels) => pixels.Count(p => p.r > 230 && p.g > 230 && p.b > 230);

        static int Diferentes(Color32[] a, Color32[] b) =>
            Enumerable.Range(0, a.Length).Count(i => a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b);
    }
}
