using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
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
                // Passos executados no mesmo iterador: uma exceção chega ao finally e o relatório sai mesmo assim.
                foreach (var passo in new Func<ManagerMission, Relatorio, IEnumerator>[] { Desenho, Paredes, Parada })
                {
                    var e = passo(mission, r);
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
            jogador.SetPositionAndRotation(new Vector3(24f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
            for (int i = 0; i < 10; i++) yield return null;
            r.Info($"na ala de atividades: {UnityStats.batches} lotes, {UnityStats.drawCalls} chamadas de desenho, {UnityStats.setPassCalls} trocas de material");
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

        static IEnumerator Parada(ManagerMission mission, Relatorio r)
        {
            r.Secao("Parada");
            yield return null;
            var placas = Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None).Where(t => t.name == "Placa").Select(t => t.transform).ToList();
            var botoes = Object.FindObjectsByType<MissionButton>(FindObjectsSortMode.None)
                .Select(b => b.GetComponentInParent<Canvas>().transform).Distinct().ToList();
            var todos = placas.Concat(botoes).ToList();
            var antes = todos.Select(t => t.rotation).ToList();
            var cabeca = mission.playerHead;
            var posicao = cabeca.position;
            var rotacao = cabeca.rotation;
            cabeca.SetPositionAndRotation(posicao + new Vector3(3f, 0f, 3f), rotacao * Quaternion.Euler(0f, 90f, 0f));
            yield return null;
            yield return null;
            int giraram = Enumerable.Range(0, todos.Count).Count(i => Quaternion.Angle(antes[i], todos[i].rotation) > .01f);
            cabeca.SetPositionAndRotation(posicao, rotacao);

            r.Check(placas.Count > 0 && botoes.Count > 0, $"{placas.Count} placas e {botoes.Count} botões na cena");
            r.Check(giraram == 0, $"nada gira com a cabeça ({giraram} de {todos.Count} giraram)");
            int foraDoEixo = placas.Count(t => Mathf.Abs(Mathf.DeltaAngle(t.eulerAngles.y, Mathf.Round(t.eulerAngles.y / 90f) * 90f)) > .5f);
            r.Check(foraDoEixo == 0, $"toda placa parada num eixo (0°, 90°, 180° ou 270°): {foraDoEixo} fora");
            var primeiroDia = placas.FirstOrDefault(t => t.GetComponent<TextMesh>().text.StartsWith("PRIMEIRO DIA"));
            r.Check(primeiroDia && Mathf.Abs(Mathf.DeltaAngle(primeiroDia.eulerAngles.y, 90f)) < .5f,
                $"PRIMEIRO DIA a 90°, de frente para quem nasce na recepção ({(primeiroDia ? primeiroDia.eulerAngles.y : float.NaN):0.#}°)");
            var galeria = placas.FirstOrDefault(t => t.GetComponent<TextMesh>().text.StartsWith("SALA 2"));
            r.Check(galeria && Mathf.Abs(Mathf.DeltaAngle(galeria.eulerAngles.y, 0f)) < .5f,
                $"SALA 2 → ALA DE AULAS a 0°, sobre a galeria ({(galeria ? galeria.eulerAngles.y : float.NaN):0.#}°)");
            var rearmar = GameObject.Find("REARMAR BOLICHE");
            r.Check(rearmar && Mathf.Abs(Mathf.DeltaAngle(rearmar.transform.eulerAngles.y, 90f)) < .5f,
                $"botão REARMAR BOLICHE a 90° ({(rearmar ? rearmar.transform.eulerAngles.y : float.NaN):0.#}°)");
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
