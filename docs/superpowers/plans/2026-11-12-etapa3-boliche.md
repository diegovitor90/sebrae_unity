# Etapa 3: boliche. Plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** transformar o boliche da sala de descompressão em um jogo de rodadas de duas bolas, com física crível, ajuda leve no arremesso do Quest, detecção por triggers e placar em tempo real.

**Architecture:** as regras da rodada ficam numa classe C# pura (`BowlingRound`), testada em EditMode. Um componente `BowlingLane` recebe do `InternshipDay` as peças já montadas e cuida de materiais físicos, massas, triggers, ajuda no arremesso, ciclo da jogada, placar, varredura e rearme. Uma verificação em Play (`BowlingCheck`) simula arremessos com controle de VR e é escrita antes do componente.

**Tech Stack:** Unity 6000.0.83f1, Built-in Render Pipeline, XR Interaction Toolkit 3.0.11, PhysX, C#.

Spec: `docs/superpowers/specs/2026-11-12-etapa3-boliche-design.md`.

## Global Constraints

- Unity 6000.0.83f1; XRI 3.0.11; Built-in Render Pipeline; alvo Android (Quest 2).
- Não editar nada em `Assets/Samples/`.
- Textos de jogo e de relatório em português. Mensagens de commit em português, sem travessão, sem primeira pessoa, sem assinatura de ferramenta.
- Não commitar `Library/`, `Logs/`, `Builds/`, `Temp/` nem APK.
- `InternshipDay.cs` é editado também pelo Richardson: mudanças nele se limitam ao que a Task 2 lista.
- Valores do spec, usados literalmente: ângulo máximo 10°, velocidade 3 a 12 m/s, apoio até 0,6 m, derrubado acima de 35° ou 15 cm, bola 6 kg, pino 1,5 kg, rotação máxima 60 rad/s, espera de velocidade 1,5 s, parada 1 s, rolando no máximo 8 s, assentando no máximo 3 s, pausa de rearme 2 s, destaque 2 s; materiais `Pista` (0,05/0,05, quique 0, atrito mínimo), `Bola` (0,2/0,2, quique 0), `Pino` (0,4/0,4, quique 0,3).

## Comandos usados em todas as tarefas

Rodar a partir da raiz do projeto, no Git Bash, com o Unity fechado neste projeto. Cada chamada de shell começa definindo:

```bash
UNITY="/c/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe"
PROJ="$(pwd -W)"
mkdir -p Logs
```

**Compilar** (batchmode):

```bash
"$UNITY" -batchmode -projectPath "$PROJ" -buildTarget Android -logFile Logs/compile.log -quit; echo "saída $?"
grep -E "error CS|warning CS" Logs/compile.log | grep SebraeLab | sort -u
```

Esperado ao final de cada tarefa: `saída 0` e nenhuma linha.

**Regras do boliche** (EditMode, batchmode; fecha com 0 ou 1):

```bash
"$UNITY" -batchmode -projectPath "$PROJ" -buildTarget Android -executeMethod SebraeLab.Editor.BowlingRoundCheck.RunFromCommandLine -logFile Logs/bowling-round.log; echo "saída $?"
cat Logs/bowling-round-check.txt
```

**Materiais físicos** (batchmode; cria ou atualiza os 3 assets):

```bash
"$UNITY" -batchmode -projectPath "$PROJ" -buildTarget Android -executeMethod SebraeLab.Editor.BowlingPhysicsMaterials.CriarPelaLinhaDeComando -logFile Logs/bowling-materials.log; echo "saída $?"
grep -E "Material físico|error" Logs/bowling-materials.log
```

**Verificação do boliche** (Play, editor com janela; fecha sozinho com o pedido `sair`):

```bash
printf 'sair' > Logs/bowling-check.request
"$UNITY" -projectPath "$PROJ" -buildTarget Android -executeMethod SebraeLab.Editor.ManagerMissionSetup.Open -logFile Logs/editor-bowling.log; echo "saída $?"
cat Logs/bowling-check.txt
```

**Verificação de navegação**, **Teste da porta**, **Percurso do Richardson**, **Limpar ruído do editor** e **APK**: iguais aos do plano da Etapa 2 (`docs/superpowers/plans/2026-11-12-etapa2-navegacao-iluminacao.md`, seção "Comandos usados em todas as tarefas"). O APK precisa do Unity criado via WMI (PowerShell), porque processos abertos pelo app do Claude não rodam o Gradle.

Nunca abrir o editor com janela sobre uma compilação que falhou. Antes de abrir o Unity, garantir que nenhum `Unity.exe` com `sebrae_unity` na linha de comando está rodando; encerrar só esses, nunca o Unity Hub nem o `unity.exe serve`.

---

### Task 1: regras da rodada (`BowlingRound`) com teste em EditMode

**Files:**
- Create: `Assets/SebraeLab/Scripts/Missions/BowlingRound.cs`
- Create: `Assets/SebraeLab/Editor/Validation/BowlingRoundCheck.cs`

**Interfaces:**
- Consumes: nada.
- Produces: `SebraeLab.Missions.BowlingRound` com `int Rodada`, `int Bola`, `int PinosNestaBola`, `int PontosDaRodada`, `int Total`, `string Destaque`, `bool RodadaEncerrada`, `void RegistrarBola(int pinos, bool calha)`, `void ProximaRodada()`, `void RecomecarRodada()`.

- [ ] **Step 1: escrever o teste**

`Assets/SebraeLab/Editor/Validation/BowlingRoundCheck.cs`:

```csharp
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
```

- [ ] **Step 2: rodar e ver falhar**

Rodar **Compilar**. Esperado: `saída 1` com `error CS0246` (`BowlingRound` não encontrado) em `BowlingRoundCheck.cs`.

- [ ] **Step 3: implementar `BowlingRound`**

`Assets/SebraeLab/Scripts/Missions/BowlingRound.cs`:

```csharp
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
```

- [ ] **Step 4: rodar e ver passar**

Rodar **Compilar** (esperado `saída 0`, sem avisos em `SebraeLab`) e **Regras do boliche**. Esperado: `saída 0` e `Logs/bowling-round-check.txt` com 16 linhas `OK` e `RESULTADO: OK`.

- [ ] **Step 5: commit**

```bash
git add Assets/SebraeLab/Scripts/Missions/BowlingRound.cs Assets/SebraeLab/Scripts/Missions/BowlingRound.cs.meta Assets/SebraeLab/Editor/Validation/BowlingRoundCheck.cs Assets/SebraeLab/Editor/Validation/BowlingRoundCheck.cs.meta
git commit -m "Cria as regras da rodada de boliche, com teste em EditMode" -m "Rodada de até duas bolas com strike, spare e calha, pontos sem bônus e total por rodada encerrada. Recomeçar uma rodada já encerrada avança para a próxima, para não contar os mesmos pontos duas vezes."
```

---

### Task 2: componente `BowlingLane`, materiais físicos e ligação na cena

**Files:**
- Create: `Assets/SebraeLab/Editor/Validation/BowlingCheck.cs`
- Create: `Assets/SebraeLab/Editor/Setup/BowlingPhysicsMaterials.cs`
- Create (gerados pelo menu): `Assets/SebraeLab/Resources/Fisica/Pista`, `Bola` e `Pino` (extensão definida pela Unity) e a pasta `Fisica`
- Create: `Assets/SebraeLab/Scripts/Missions/BowlingLane.cs`
- Modify: `Assets/SebraeLab/Scripts/Missions/InternshipDay.cs` (campo `pinResets` e `BuildLounge`)
- Modify: `Assets/SebraeLab/Editor/Validation/DesktopWalkCheck.cs:136-141`

**Interfaces:**
- Consumes: `BowlingRound` (Task 1).
- Produces: `SebraeLab.Missions.BowlingLane : MonoBehaviour` com `enum Fase { Pronta, Solta, Rolando, Assentando, AguardandoRearme }`, `BowlingRound Partida`, `Fase FaseAtual`, `Collider Pista`, `IReadOnlyList<Collider> Calhas`, `IReadOnlyList<Rigidbody> Pinos`, `XRGrabInteractable Bola`, `Vector3 SuporteBola`, `Collider AreaDeLancamento`, `TextMesh Placar`, `float RaioBola`, `int PinosDerrubadosNestaJogada`, `bool Derrubado(int i)`, `void Configurar(Collider pista, IEnumerable<Collider> fundosDasCalhas, IEnumerable<Collider> bordasDasCalhas, IEnumerable<Rigidbody> pinosDaPista, XRGrabInteractable bola, TextMesh placar)`, `void Rearmar()`; `SebraeLab.Missions.BowlingTrigger`.

- [ ] **Step 1: gerador dos materiais físicos (preparação, antes do teste)**

`Assets/SebraeLab/Editor/Setup/BowlingPhysicsMaterials.cs`:

```csharp
using UnityEditor;
using UnityEngine;

namespace SebraeLab.Editor
{
    /// <summary>
    /// Cria ou atualiza os materiais físicos do boliche em Assets/SebraeLab/Resources/Fisica.
    /// Os valores podem ser ajustados depois no Inspector; rodar de novo restaura os do spec.
    /// </summary>
    public static class BowlingPhysicsMaterials
    {
        const string Pai = "Assets/SebraeLab/Resources";
        const string Pasta = Pai + "/Fisica";
        const string Extensao = ".physicsMaterial";

        [MenuItem("Sebrae Lab/Boliche/Criar materiais físicos")]
        public static void Criar()
        {
            if (!AssetDatabase.IsValidFolder(Pasta)) AssetDatabase.CreateFolder(Pai, "Fisica");
            Salvar("Pista", .05f, .05f, 0f, PhysicsMaterialCombine.Minimum);
            Salvar("Bola", .2f, .2f, 0f, PhysicsMaterialCombine.Average);
            Salvar("Pino", .4f, .4f, .3f, PhysicsMaterialCombine.Average);
            AssetDatabase.SaveAssets();
        }

        public static void CriarPelaLinhaDeComando() { Criar(); EditorApplication.Exit(0); }

        static void Salvar(string nome, float dinamico, float estatico, float quique, PhysicsMaterialCombine atrito)
        {
            var caminho = Pasta + "/" + nome + Extensao;
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(caminho);
            bool novo = !material;
            if (novo) material = new PhysicsMaterial(nome);
            material.dynamicFriction = dinamico;
            material.staticFriction = estatico;
            material.bounciness = quique;
            material.frictionCombine = atrito;
            material.bounceCombine = PhysicsMaterialCombine.Average;
            if (novo) AssetDatabase.CreateAsset(material, caminho);
            else EditorUtility.SetDirty(material);
            Debug.Log($"Material físico {caminho}: atrito {dinamico}/{estatico}, quique {quique}, combinação {atrito}");
        }
    }
}
```

Rodar **Compilar** (limpo) e **Materiais físicos**. Esperado: 3 linhas `Material físico ...` no log e três arquivos novos em `Assets/SebraeLab/Resources/Fisica/`, cada um com `.meta`. Se o log acusar extensão inválida para `PhysicsMaterial`, trocar `Extensao` para `.physicMaterial`, rodar de novo e registrar no relatório da tarefa.

- [ ] **Step 2: escrever a verificação em Play**

`Assets/SebraeLab/Editor/Validation/BowlingCheck.cs`:

```csharp
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
```

- [ ] **Step 3: rodar e ver falhar**

Rodar **Compilar**. Esperado: `saída 1` com `error CS0246` (`BowlingLane` não encontrado) em `BowlingCheck.cs`.

- [ ] **Step 4: implementar `BowlingLane`**

`Assets/SebraeLab/Scripts/Missions/BowlingLane.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SebraeLab.Missions
{
    /// <summary>
    /// Regras do boliche da sala de descompressão: materiais físicos, massas, triggers, ajuda leve no
    /// arremesso do Quest, ciclo da jogada, rodadas de duas bolas (<see cref="BowlingRound"/>), placar,
    /// varredura e rearme. O InternshipDay monta as peças e chama <see cref="Configurar"/>.
    /// </summary>
    public class BowlingLane : MonoBehaviour
    {
        public enum Fase { Pronta, Solta, Rolando, Assentando, AguardandoRearme }

        [Header("Ajuda no arremesso (Quest)")]
        public float anguloMaximo = 10f;
        public float velocidadeMinima = 3f;
        public float velocidadeMaxima = 12f;
        public float alturaMaximaDeApoio = .6f;

        [Header("Contagem")]
        public float inclinacaoDerrubado = 35f;
        public float deslocamentoDerrubado = .15f;

        [Header("Tempos (s)")]
        public float esperaVelocidade = 1.5f;
        public float tempoParada = 1f;
        public float tempoMaximoRolando = 8f;
        public float tempoMaximoAssentando = 3f;
        public float pausaRearme = 2f;
        public float duracaoDestaque = 2f;

        [Header("Massas (kg)")]
        public float massaBola = 6f;
        public float massaPino = 1.5f;

        public BowlingRound Partida { get; } = new BowlingRound();
        public Fase FaseAtual { get; private set; }
        public Collider Pista { get; private set; }
        public IReadOnlyList<Collider> Calhas => calhas;
        public IReadOnlyList<Rigidbody> Pinos => pinos;
        public XRGrabInteractable Bola { get; private set; }
        public Vector3 SuporteBola { get; private set; }
        public Collider AreaDeLancamento { get; private set; }
        public TextMesh Placar { get; private set; }
        public float RaioBola { get; private set; }

        readonly List<Collider> calhas = new List<Collider>();
        readonly List<Rigidbody> pinos = new List<Rigidbody>();
        readonly List<Vector3> origens = new List<Vector3>();
        bool[] emPeNoInicio = new bool[0];
        Rigidbody corpoBola;
        Collider colisorBola;
        bool ajudaPendente, tocouCalha, chegouAoFundo;
        float tempoFase, tempoLento, destaqueAte;

        public void Configurar(Collider pista, IEnumerable<Collider> fundosDasCalhas, IEnumerable<Collider> bordasDasCalhas,
            IEnumerable<Rigidbody> pinosDaPista, XRGrabInteractable bola, TextMesh placar)
        {
            Pista = pista;
            Placar = placar;
            Bola = bola;
            corpoBola = bola.GetComponent<Rigidbody>();
            colisorBola = bola.GetComponent<Collider>();
            SuporteBola = bola.transform.position;
            var esfera = colisorBola as SphereCollider;
            RaioBola = esfera ? esfera.radius * bola.transform.lossyScale.x : colisorBola.bounds.extents.y;
            foreach (var pino in pinosDaPista) { pinos.Add(pino); origens.Add(pino.position); }
            emPeNoInicio = new bool[pinos.Count];

            var materialPista = MaterialFisico("Pista", .05f, .05f, 0f, PhysicsMaterialCombine.Minimum);
            var materialBola = MaterialFisico("Bola", .2f, .2f, 0f, PhysicsMaterialCombine.Average);
            var materialPino = MaterialFisico("Pino", .4f, .4f, .3f, PhysicsMaterialCombine.Average);
            pista.sharedMaterial = materialPista;
            foreach (var c in fundosDasCalhas) { c.sharedMaterial = materialPista; calhas.Add(c); }
            foreach (var c in bordasDasCalhas) c.sharedMaterial = materialPista;
            colisorBola.sharedMaterial = materialBola;
            corpoBola.mass = massaBola;
            corpoBola.interpolation = RigidbodyInterpolation.Interpolate;
            corpoBola.maxAngularVelocity = 60f;
            foreach (var pino in pinos)
            {
                pino.mass = massaPino;
                foreach (var c in pino.GetComponents<Collider>()) c.sharedMaterial = materialPino;
            }

            // Triggers a partir da pista: lançamento de z min-2,5 a min+1,5; fundo de z max-0,3 a max+2,5.
            var b = pista.bounds;
            float largura = b.size.x + .96f;
            AreaDeLancamento = CriarTrigger("Área de lançamento", BowlingTrigger.Tipo.Lancamento,
                new Vector3(b.center.x, 1f, b.min.z - .5f), new Vector3(largura, 2f, 4f));
            foreach (var c in calhas)
            {
                var cb = c.bounds;
                CriarTrigger("Calha", BowlingTrigger.Tipo.Calha, new Vector3(cb.center.x, -.1f, cb.center.z), new Vector3(cb.size.x, .2f, cb.size.z));
            }
            CriarTrigger("Fundo da pista", BowlingTrigger.Tipo.Fundo,
                new Vector3(b.center.x, .4f, b.max.z + 1.1f), new Vector3(largura, 1.2f, 2.8f));

            bola.selectExited.AddListener(AoSoltar);
            AtualizarPlacar();
        }

        /// <summary>Recomeça a rodada atual: bola no suporte, 10 pinos em pé, bola 1, total mantido.</summary>
        public void Rearmar()
        {
            if (Bola.isSelected) return;
            ajudaPendente = false;
            DevolverBola();
            LevantarPinos();
            Partida.RecomecarRodada();
            FaseAtual = Fase.Pronta;
            destaqueAte = 0f;
            AtualizarPlacar();
        }

        /// <summary>Pino ativo inclinado além do limite ou deslocado da origem no plano horizontal.</summary>
        public bool Derrubado(int i)
        {
            var pino = pinos[i];
            if (!pino.gameObject.activeInHierarchy) return false;
            var deslocamento = pino.position - origens[i];
            deslocamento.y = 0f;
            return Vector3.Angle(pino.rotation * Vector3.up, Vector3.up) > inclinacaoDerrubado || deslocamento.magnitude > deslocamentoDerrubado;
        }

        /// <summary>Pinos que estavam em pé no início da jogada e agora estão derrubados.</summary>
        public int PinosDerrubadosNestaJogada
        {
            get
            {
                int n = 0;
                for (int i = 0; i < pinos.Count; i++) if (emPeNoInicio[i] && Derrubado(i)) n++;
                return n;
            }
        }

        internal void AoEntrarNoTrigger(BowlingTrigger.Tipo tipo, Collider outro)
        {
            if (outro.attachedRigidbody != corpoBola || FaseAtual != Fase.Rolando) return;
            if (tipo == BowlingTrigger.Tipo.Calha) tocouCalha = true;
            else if (tipo == BowlingTrigger.Tipo.Fundo) chegouAoFundo = true;
        }

        void AoSoltar(SelectExitEventArgs args)
        {
            // Sem interactor: modo PC ou validador. A ajuda é só para quem solta com o controle.
            var interactor = args != null ? args.interactorObject as Object : null;
            if (interactor && AreaDeLancamento.bounds.Contains(corpoBola.position))
            {
                ajudaPendente = true;
                var vibrador = args.interactorObject.transform.GetComponentInParent<HapticImpulsePlayer>();
                if (vibrador) vibrador.SendHapticImpulse(.4f, .08f);
            }
            if (FaseAtual == Fase.Pronta) { FaseAtual = Fase.Solta; tempoFase = 0f; }
        }

        void FixedUpdate()
        {
            if (!ajudaPendente) return;
            ajudaPendente = false;
            AplicarAjuda();
        }

        void AplicarAjuda()
        {
            var velocidade = corpoBola.linearVelocity;
            var posicao = corpoBola.position;
            RaycastHit? apoio = null;
            foreach (var hit in Physics.RaycastAll(posicao, Vector3.down, RaioBola + alturaMaximaDeApoio, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider != colisorBola && (apoio == null || hit.distance < apoio.Value.distance)) apoio = hit;
            if (apoio.HasValue)
            {
                posicao.y = apoio.Value.point.y + RaioBola;
                corpoBola.position = posicao;
                velocidade.y = 0f;
            }
            var eixo = Vector3.ProjectOnPlane(Pista.transform.forward, Vector3.up).normalized;
            var horizontal = new Vector3(velocidade.x, 0f, velocidade.z);
            float angulo = horizontal.sqrMagnitude > 1e-4f
                ? Mathf.Clamp(Vector3.SignedAngle(eixo, horizontal, Vector3.up), -anguloMaximo, anguloMaximo)
                : 0f;
            float rapidez = Mathf.Clamp(horizontal.magnitude, velocidadeMinima, velocidadeMaxima);
            var direcao = Quaternion.AngleAxis(angulo, Vector3.up) * eixo;
            corpoBola.linearVelocity = direcao * rapidez + Vector3.up * velocidade.y;
            corpoBola.angularVelocity = Vector3.Cross(Vector3.up, direcao * rapidez) / RaioBola; // já sai rolando
        }

        void Update()
        {
            float dt = Time.deltaTime;
            switch (FaseAtual)
            {
                case Fase.Solta:
                    tempoFase += dt;
                    if (Bola.isSelected) FaseAtual = Fase.Pronta;
                    else if (corpoBola.linearVelocity.magnitude > .5f) ComecarJogada();
                    else if (tempoFase > esperaVelocidade) FaseAtual = Fase.Pronta;
                    break;
                case Fase.Rolando:
                    tempoFase += dt;
                    tempoLento = corpoBola.linearVelocity.magnitude < .1f ? tempoLento + dt : 0f;
                    if (chegouAoFundo || tempoLento > tempoParada || tempoFase > tempoMaximoRolando)
                    {
                        FaseAtual = Fase.Assentando;
                        tempoFase = 0f;
                    }
                    break;
                case Fase.Assentando:
                    tempoFase += dt;
                    if (tempoFase > tempoMaximoAssentando || pinos.All(p => !p.gameObject.activeInHierarchy || p.IsSleeping())) Contar();
                    break;
                case Fase.AguardandoRearme:
                    tempoFase += dt;
                    if (tempoFase > pausaRearme)
                    {
                        LevantarPinos();
                        Partida.ProximaRodada();
                        FaseAtual = Fase.Pronta;
                    }
                    break;
            }
            AtualizarPlacar();
        }

        void ComecarJogada()
        {
            for (int i = 0; i < pinos.Count; i++) emPeNoInicio[i] = pinos[i].gameObject.activeInHierarchy && !Derrubado(i);
            tocouCalha = false;
            chegouAoFundo = false;
            tempoFase = 0f;
            tempoLento = 0f;
            FaseAtual = Fase.Rolando;
        }

        void Contar()
        {
            Partida.RegistrarBola(PinosDerrubadosNestaJogada, tocouCalha);
            if (!string.IsNullOrEmpty(Partida.Destaque)) destaqueAte = Time.time + duracaoDestaque;
            DevolverBola();
            if (Partida.RodadaEncerrada)
            {
                FaseAtual = Fase.AguardandoRearme;
                tempoFase = 0f;
                return;
            }
            for (int i = 0; i < pinos.Count; i++) if (Derrubado(i)) pinos[i].gameObject.SetActive(false); // varredura
            FaseAtual = Fase.Pronta;
        }

        void AtualizarPlacar()
        {
            if (!Placar) return;
            bool emJogada = FaseAtual == Fase.Rolando || FaseAtual == Fase.Assentando;
            int nestaBola = emJogada ? PinosDerrubadosNestaJogada : Partida.PinosNestaBola;
            string titulo = Time.time < destaqueAte && !string.IsNullOrEmpty(Partida.Destaque) ? Partida.Destaque : "BOLICHE";
            string texto = $"{titulo}  •  RODADA {Partida.Rodada}  •  BOLA {Partida.Bola}\n" +
                           $"Nesta bola: {nestaBola}   Rodada: {Partida.PontosDaRodada}   Total: {Partida.Total}";
            if (Placar.text != texto) Placar.text = texto;
        }

        void DevolverBola() => Repor(corpoBola, SuporteBola, Quaternion.identity);

        void LevantarPinos()
        {
            for (int i = 0; i < pinos.Count; i++)
            {
                pinos[i].gameObject.SetActive(true);
                Repor(pinos[i], origens[i], Quaternion.identity);
            }
            Physics.SyncTransforms();
        }

        static void Repor(Rigidbody corpo, Vector3 posicao, Quaternion rotacao)
        {
            if (!corpo.isKinematic)
            {
                corpo.linearVelocity = Vector3.zero;
                corpo.angularVelocity = Vector3.zero;
            }
            corpo.transform.SetPositionAndRotation(posicao, rotacao);
            corpo.position = posicao;
            corpo.rotation = rotacao;
            corpo.Sleep();
        }

        Collider CriarTrigger(string nome, BowlingTrigger.Tipo tipo, Vector3 centro, Vector3 tamanho)
        {
            var objeto = new GameObject("Trigger • " + nome) { layer = 2 }; // Ignore Raycast: raios do XR e do PC não param nele
            objeto.transform.SetParent(transform, true);
            objeto.transform.position = centro;
            var caixa = objeto.AddComponent<BoxCollider>();
            caixa.isTrigger = true;
            caixa.size = tamanho;
            objeto.AddComponent<BowlingTrigger>().Configurar(this, tipo);
            return caixa;
        }

        static PhysicsMaterial MaterialFisico(string nome, float dinamico, float estatico, float quique, PhysicsMaterialCombine atrito)
        {
            var material = Resources.Load<PhysicsMaterial>("Fisica/" + nome);
            if (material) return material;
            Debug.LogWarning($"BowlingLane: Resources/Fisica/{nome} não encontrado; usando os valores padrão do spec.");
            return new PhysicsMaterial(nome) { dynamicFriction = dinamico, staticFriction = estatico, bounciness = quique, frictionCombine = atrito };
        }
    }

    /// <summary>Repassa ao BowlingLane a entrada da bola num trigger da pista.</summary>
    public class BowlingTrigger : MonoBehaviour
    {
        public enum Tipo { Lancamento, Calha, Fundo }

        BowlingLane dono;
        Tipo tipo;

        public void Configurar(BowlingLane lane, Tipo t)
        {
            dono = lane;
            tipo = t;
        }

        void OnTriggerEnter(Collider outro)
        {
            if (dono) dono.AoEntrarNoTrigger(tipo, outro);
        }
    }
}
```

Conferir as medidas geradas contra o spec: a área de lançamento cobre x 43,52 a 46,48 e z -6 a -2; as calhas, y -0,2 a 0; o fundo, z 5,2 a 8 e y -0,2 a 1. Com a pista em x 44 a 46 e z -3,5 a 5,5, os centros e tamanhos acima dão exatamente esses intervalos.

- [ ] **Step 5: ligar no `InternshipDay`**

Remover o campo que fica sem uso:

```csharp
        readonly List<Action> pinResets = new List<Action>();
```

Em `BuildLounge`, substituir:

```csharp
            Shape("Pista de boliche",new Vector3(45,.04f,1),new Vector3(2,.08f,9),gold);
            foreach(float side in new[]{-1f,1f})
            {
                float center=45+side*1.24f;
                Shape("Fundo da calha",new Vector3(center,-.19f,1),new Vector3(.49f,.08f,9),navy);
                foreach(float edge in new[]{-1f,1f}){
                    var lip=Shape("Lateral da calha",new Vector3(center+edge*.19f,-.075f,1),new Vector3(.09f,.28f,9),navy);
                    lip.transform.rotation=Quaternion.Euler(0,0,-edge*25);
                }
```

por:

```csharp
            var lane=Shape("Pista de boliche",new Vector3(45,.04f,1),new Vector3(2,.08f,9),gold);
            var gutterFloors=new List<Collider>();var gutterLips=new List<Collider>();
            foreach(float side in new[]{-1f,1f})
            {
                float center=45+side*1.24f;
                gutterFloors.Add(Shape("Fundo da calha",new Vector3(center,-.19f,1),new Vector3(.49f,.08f,9),navy).GetComponent<Collider>());
                foreach(float edge in new[]{-1f,1f}){
                    var lip=Shape("Lateral da calha",new Vector3(center+edge*.19f,-.075f,1),new Vector3(.09f,.28f,9),navy);
                    lip.transform.rotation=Quaternion.Euler(0,0,-edge*25);
                    gutterLips.Add(lip.GetComponent<Collider>());
                }
```

Substituir:

```csharp
            var board=Sign("BOLICHE   •   0 / 10",new Vector3(45,2.2f,5.4f),.12f);
            var pins=new List<Rigidbody>();
            int displayed=0;
            ticks.Add(()=>{
                int down=0;
                foreach(var pin in pins) if(Vector3.Angle(pin.rotation*Vector3.up,Vector3.up)>35) down++;
                if(down!=displayed){displayed=down;board.text="BOLICHE   •   "+down+" / 10";}
            });
```

por:

```csharp
            // Placar, rodadas e contagem ficam no BowlingLane.
            var board=Sign("BOLICHE",new Vector3(45,2.2f,5.4f),.12f);
            var pins=new List<Rigidbody>();
```

Substituir:

```csharp
                var rb=pin.AddComponent<Rigidbody>();rb.mass=.22f;pins.Add(rb);pinResets.Add(()=>ResetBody(rb,p,Quaternion.identity));}
```

por:

```csharp
                pins.Add(pin.AddComponent<Rigidbody>());}
```

Substituir:

```csharp
            pinResets.Insert(0,()=>ResetBody(ball.GetComponent<Rigidbody>(),ballPos,Quaternion.identity));
            Station("REARMAR BOLICHE",new Vector3(43,1,-3),()=>{if(!ball.isSelected){foreach(var reset in pinResets)reset();Physics.SyncTransforms();}});
```

por:

```csharp
            var bowling=gameObject.AddComponent<BowlingLane>();
            bowling.Configurar(lane.GetComponent<Collider>(),gutterFloors,gutterLips,pins,ball,board);
            Station("REARMAR BOLICHE",new Vector3(43,1,-3),bowling.Rearmar);
```

Conferir que não sobrou uso de `pinResets` nem de `displayed`:

```bash
grep -n -E "pinResets|displayed" Assets/SebraeLab/Scripts/Missions/InternshipDay.cs || echo "nenhum"
```

- [ ] **Step 6: atualizar o `DesktopWalkCheck`**

Substituir:

```csharp
        yield return new WaitForSeconds(3);
        var pins=UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None)
            .Where(b=>b.name=="Pino").Count(b=>Vector3.Angle(b.transform.up,Vector3.up)>35);
        Require(pins>0,"Bola não derrubou pinos, posição "+ball.position);
        Require(UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None).Any(t=>t.text=="BOLICHE   •   "+pins+" / 10"),"Placar de boliche incorreto");
        Note("Boliche: "+pins+" pinos derrubados");
```

por:

```csharp
        yield return new WaitForSeconds(3);
        // O BowlingLane conta ao fim da jogada e varre os derrubados; espera a contagem antes de conferir.
        var lane=UnityEngine.Object.FindFirstObjectByType<BowlingLane>();
        for(float wait=0;wait<12 && lane.FaseAtual!=BowlingLane.Fase.Pronta && lane.FaseAtual!=BowlingLane.Fase.AguardandoRearme;wait+=Time.deltaTime)yield return null;
        var pins=lane.Partida.PinosNestaBola;
        Require(pins>0,"Bola não derrubou pinos, posição "+ball.position);
        Require(lane.Placar.text.Contains("Nesta bola: "+pins),"Placar de boliche incorreto: "+lane.Placar.text);
        Note("Boliche: "+pins+" pinos derrubados");
```

- [ ] **Step 7: verificar**

Rodar **Compilar** (limpo), **Regras do boliche** (OK), **Verificação do boliche** e **Percurso do Richardson**. Esperado:
- `Logs/bowling-check.txt` com `RESULTADO: OK`: seções Física (6 OK), Ajuda no arremesso (3 OK), Arremesso reto (6 OK), Calha (2 OK) e Rearme (3 OK);
- `desktop-walk.txt` até `Recomeçar o dia: recepção e objetivo inicial`, com a linha `Boliche: n pinos derrubados` e sem `FALHA`.

Se o arremesso reto não derrubar pinos ou a ajuda falhar, ler `Logs/editor-bowling.log`, corrigir dentro desta tarefa e explicar no relatório. Não mudar as verificações para passarem. Depois, **Limpar ruído do editor**.

- [ ] **Step 8: commits**

```bash
git add Assets/SebraeLab/Editor/Setup/BowlingPhysicsMaterials.cs Assets/SebraeLab/Editor/Setup/BowlingPhysicsMaterials.cs.meta Assets/SebraeLab/Resources/Fisica Assets/SebraeLab/Resources/Fisica.meta
git commit -m "Cria os materiais físicos do boliche" -m "Pista encerada, bola sem quique e pinos que se espalham ao bater, como assets em Resources/Fisica para ajuste no Inspector. O menu Sebrae Lab/Boliche recria os valores do spec."
git add Assets/SebraeLab/Scripts/Missions/BowlingLane.cs Assets/SebraeLab/Scripts/Missions/BowlingLane.cs.meta Assets/SebraeLab/Scripts/Missions/InternshipDay.cs Assets/SebraeLab/Editor/Validation/BowlingCheck.cs Assets/SebraeLab/Editor/Validation/BowlingCheck.cs.meta Assets/SebraeLab/Editor/Validation/DesktopWalkCheck.cs
git commit -m "Transforma o boliche em rodadas com física, triggers e placar" -m "O BowlingLane recebe as peças do InternshipDay e cuida de materiais, massas, ajuda leve no arremesso do Quest, eventos por trigger, contagem por inclinação ou deslocamento, varredura, devolução da bola e rearme. O placar mostra rodada, bola, pinos e total em tempo real."
```

---

### Task 3: verificação final e APK

**Files:** nenhum arquivo de código. Só verificação e APK.

**Interfaces:**
- Consumes: tudo das Tasks 1 e 2.
- Produces: APK `Builds/Quest/MissaoEstagio.apk`.

- [ ] **Step 1: rodar tudo em sequência**

Rodar, nesta ordem e um Unity por vez: **Compilar**, **Regras do boliche**, **Teste da porta**, **Verificação do boliche**, **Verificação de navegação** e **Percurso do Richardson**. Esperado: compilação limpa; `RESULTADO: OK` em `bowling-round-check.txt`, `hand-door-check.txt`, `bowling-check.txt` e `navigation-check.txt`; `desktop-walk.txt` até o fim, sem `FALHA`. **Limpar ruído do editor** depois de cada execução com janela.

- [ ] **Step 2: APK**

Rodar **APK** (PowerShell, via WMI). Esperado: `Build Finished, Result: Success`. **Limpar ruído do editor**. `git status --short` deve ficar vazio.
