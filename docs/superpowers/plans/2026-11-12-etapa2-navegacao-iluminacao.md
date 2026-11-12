# Etapa 2: locomoção livre, tetos, iluminação e porta. Plano de implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** deixar o expediente pronto para a P2 com locomoção livre no Quest 2, teto em todo ambiente coberto, iluminação em tempo real barata e porta da Sala 2 seguindo a mão.

**Architecture:** a lógica da porta sai do `InternshipDay` para um componente `HandDoor`, testado em EditMode. Ajustes de luz e teto entram como ligações pequenas no `InternshipDay` e no `MissionSurfaces`. A cena só perde um override e ganha um modo de luz. Um validador novo em Play (`NavigationCheck`) mede rota, degraus, tetos, luzes e custo por ambiente, e é escrito antes das correções para servir de teste que falha.

**Tech Stack:** Unity 6000.0.83f1, Built-in Render Pipeline, XR Interaction Toolkit 3.0.11 (Starter Assets), OpenXR, C#.

Spec: `docs/superpowers/specs/2026-11-12-etapa2-navegacao-iluminacao-design.md`.

## Global Constraints

- Unity 6000.0.83f1; XRI 3.0.11; Built-in Render Pipeline; alvo Android (perfil de qualidade Medium).
- Não editar nada em `Assets/Samples/`. Ajustes do rig entram como override na cena.
- Sem bake, sem light probes, sem reflection probes.
- Textos de jogo e de relatório em português. Mensagens de commit em português, sem travessão, sem primeira pessoa, sem assinatura de ferramenta.
- Não commitar `Library/`, `Logs/`, `Builds/`, `Temp/` nem APK.
- `InternshipDay.cs` é editado também pelo Richardson: mudanças nele se limitam ao que cada tarefa lista.

## Comandos usados em todas as tarefas

Rodar a partir da raiz do projeto, no Git Bash, com o Unity fechado neste projeto.

```bash
UNITY="/c/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe"
PROJ="$(pwd -W)"
mkdir -p Logs
```

**Compilar** (batchmode; sai sozinho):

```bash
"$UNITY" -batchmode -projectPath "$PROJ" -buildTarget Android -logFile Logs/compile.log -quit; echo "saída $?"
grep -E "error CS|warning CS" Logs/compile.log | grep SebraeLab | sort -u
```

Esperado ao final de cada tarefa: `saída 0` e nenhuma linha de `error CS` ou `warning CS` em arquivos `SebraeLab`.

**Teste da porta** (EditMode, batchmode; o método fecha o Unity com código 0 ou 1):

```bash
"$UNITY" -batchmode -projectPath "$PROJ" -buildTarget Android -executeMethod SebraeLab.Editor.HandDoorCheck.RunFromCommandLine -logFile Logs/hand-door.log; echo "saída $?"
cat Logs/hand-door-check.txt
```

**Verificação de navegação** (Play; precisa do editor com janela; o validador fecha o editor ao terminar quando o pedido contém `sair`):

```bash
printf 'sair' > Logs/navigation-check.request
"$UNITY" -projectPath "$PROJ" -buildTarget Android -executeMethod SebraeLab.Editor.ManagerMissionSetup.Open -logFile Logs/editor-navigation.log; echo "saída $?"
cat Logs/navigation-check.txt
```

**Percurso do Richardson** (`DesktopWalkCheck`, Play; não fecha sozinho):

```bash
rm -f Logs/desktop-walk.txt; : > Logs/desktop-walk.request
"$UNITY" -projectPath "$PROJ" -buildTarget Android -executeMethod SebraeLab.Editor.ManagerMissionSetup.Open -logFile Logs/editor-walk.log &
until grep -q -E "Recomeçar o dia|FALHA" Logs/desktop-walk.txt 2>/dev/null; do sleep 15; done; sleep 5
powershell.exe -NoProfile -Command "Get-CimInstance Win32_Process -Filter \"Name='Unity.exe'\" | Where-Object { \$_.CommandLine -like '*sebrae_unity*' } | ForEach-Object { Stop-Process -Id \$_.ProcessId -Force }"
cat Logs/desktop-walk.txt
```

**Limpar ruído do editor** (abrir a cena regrava estes arquivos; não fazem parte de nenhuma tarefa):

```bash
git checkout -- Assets/SebraeLab/Environment/Materials/Paredes.mat Assets/SebraeLab/Environment/Materials/Vidro.mat Previews/ 2>/dev/null
git checkout -- Assets/Plugins/Android/AndroidManifest.xml ProjectSettings/ProjectSettings.asset 2>/dev/null
rmdir Assets/Resources 2>/dev/null && rm -f Assets/Resources.meta
git status --short
```

**APK** (Tarefa 7). Quando o Unity é aberto por um processo filho do app do Claude (pacote MSIX), o Gradle falha com `Unable to establish loopback connection`. O processo precisa nascer fora do pacote, via WMI:

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.83f1\Editor\Unity.exe'; $proj = (Get-Location).Path
$cmd = "`"$unity`" -batchmode -projectPath `"$proj`" -buildTarget Android -executeMethod SebraeLab.Editor.ManagerMissionSetup.Build -logFile `"$proj\Logs\build-apk.log`" -quit"
$r = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = $cmd; CurrentDirectory = $proj }
while (Get-Process -Id $r.ProcessId -ErrorAction SilentlyContinue) { Start-Sleep 20 }
Select-String "$proj\Logs\build-apk.log" -Pattern 'Build Finished, Result' | Select-Object -Last 1
Get-Item "$proj\Builds\Quest\MissaoEstagio.apk" | Select-Object Length, LastWriteTime
```

---

### Task 1: componente `HandDoor` com teste em EditMode

**Files:**
- Create: `Assets/SebraeLab/Scripts/Missions/HandDoor.cs`
- Create: `Assets/SebraeLab/Editor/Validation/HandDoorCheck.cs`

**Interfaces:**
- Consumes: nada de outras tarefas.
- Produces: `SebraeLab.Missions.HandDoor : MonoBehaviour` com `const float MaxAngle = 95f`, `const float OpenThreshold = 45f`, `float Angle { get; }`, `bool IsOpen { get; }`, `bool Following { get; }`, `void Bind(XRBaseInteractable handle)`, `void Toggle()`, `void BeginFollow(Transform follower)`, `void EndFollow()`, `void Tick(float deltaTime)`. `SebraeLab.Editor.HandDoorCheck` com `static int Run()` e `static void RunFromCommandLine()`.

- [ ] **Step 1: escrever o teste**

`Assets/SebraeLab/Editor/Validation/HandDoorCheck.cs`:

```csharp
using System;
using System.IO;
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
    /// Teste em EditMode da porta da Sala 2. Monta uma dobradiça igual à do InternshipDay
    /// (sala girada -90°), simula um controle e confere seguir a mão, soltar, Toggle e limites.
    /// Resultado em Logs/hand-door-check.txt.
    /// </summary>
    public static class HandDoorCheck
    {
        const string Report = "Logs/hand-door-check.txt";

        [MenuItem("Sebrae Lab/Verificação/Porta da Sala 2 (mão)")]
        static void Menu() => Debug.Log("HandDoorCheck: " + (Run() == 0 ? "OK" : "FALHA") + ". Detalhes em " + Report);

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

            var sala = new GameObject("Teste porta • sala");
            sala.transform.SetPositionAndRotation(new Vector3(16, 0, 0), Quaternion.Euler(0, -90, 0));
            var dobradica = new GameObject("Dobradiça de teste").transform;
            dobradica.SetParent(sala.transform, false);
            dobradica.localPosition = Vector3.left * .56f;
            var macaneta = GameObject.CreatePrimitive(PrimitiveType.Cube);
            macaneta.transform.SetParent(dobradica, false);
            macaneta.transform.localPosition = new Vector3(.99f, 1, .12f);
            var interativo = macaneta.AddComponent<XRSimpleInteractable>();
            var mao = new GameObject("Mão de teste").AddComponent<XRDirectInteractor>();

            void Pegar(IXRSelectInteractor quem) =>
                interativo.selectEntered.Invoke(new SelectEnterEventArgs { interactorObject = quem, interactableObject = interativo });
            void Soltar(IXRSelectInteractor quem) =>
                interativo.selectExited.Invoke(new SelectExitEventArgs { interactorObject = quem, interactableObject = interativo });
            void Girar(float graus) => mao.transform.RotateAround(dobradica.position, Vector3.up, graus);

            try
            {
                var porta = dobradica.gameObject.AddComponent<HandDoor>();
                porta.Bind(interativo);
                Check(porta.Angle == 0f && !porta.IsOpen, $"começa fechada ({porta.Angle:0.#}°)");

                mao.transform.position = macaneta.transform.position;
                Pegar(mao);
                Check(porta.Following, "pegar com controle passa a seguir a mão");
                Girar(60f); porta.Tick(.02f);
                Check(Mathf.Abs(porta.Angle - 60f) < 5f && porta.IsOpen, $"girou o controle 60°, porta em {porta.Angle:0.#}°");
                Check(Quaternion.Angle(dobradica.localRotation, Quaternion.Euler(0, porta.Angle, 0)) < .01f, "rotação local da dobradiça acompanha o ângulo");

                Soltar(mao);
                Girar(-60f); porta.Tick(.02f);
                Check(!porta.Following && Mathf.Abs(porta.Angle - 60f) < 5f, $"solta e fica onde parou ({porta.Angle:0.#}°)");

                porta.Toggle(); for (int i = 0; i < 60; i++) porta.Tick(.02f);
                Check(porta.Angle == 0f && Quaternion.Angle(dobradica.localRotation, Quaternion.identity) < .01f, $"Toggle a partir de 60° fecha ({porta.Angle:0.#}°)");

                porta.Toggle(); for (int i = 0; i < 60; i++) porta.Tick(.02f);
                Check(Mathf.Approximately(porta.Angle, HandDoor.MaxAngle), $"Toggle abre até {HandDoor.MaxAngle}° ({porta.Angle:0.#}°)");

                Pegar(null); for (int i = 0; i < 60; i++) porta.Tick(.02f);
                Check(porta.Angle == 0f && !porta.Following, $"seleção sem controle (modo PC) alterna e fecha ({porta.Angle:0.#}°)");

                mao.transform.position = macaneta.transform.position;
                Pegar(mao);
                Girar(150f); porta.Tick(.02f);
                Check(porta.Angle <= HandDoor.MaxAngle + .01f && porta.Angle >= HandDoor.MaxAngle - 1f, $"não passa de {HandDoor.MaxAngle}° ({porta.Angle:0.#}°)");
                Girar(-200f); porta.Tick(.02f);
                Check(porta.Angle >= 0f && porta.Angle < 1f, $"não passa de 0° ({porta.Angle:0.#}°)");
                Soltar(mao);
            }
            catch (Exception e)
            {
                falhas++;
                texto.AppendLine("FALHA exceção: " + e);
            }
            finally
            {
                Object.DestroyImmediate(sala);
                Object.DestroyImmediate(mao.gameObject);
            }

            texto.Insert(0, "Porta da Sala 2 (HandDoor) • " + DateTime.Now + "\n");
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

Rodar **Compilar**. Esperado: `saída 1` e `error CS0246: The type or namespace name 'HandDoor' could not be found` em `HandDoorCheck.cs`.

- [ ] **Step 3: implementar `HandDoor`**

`Assets/SebraeLab/Scripts/Missions/HandDoor.cs`:

```csharp
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SebraeLab.Missions
{
    /// <summary>
    /// Porta que gira no eixo Y local deste objeto (a dobradiça).
    /// No Quest, segurar a maçaneta faz a porta seguir o ângulo do controle em volta da dobradiça.
    /// No modo PC e nos validadores, <see cref="Toggle"/> anima até o extremo oposto.
    /// </summary>
    [DisallowMultipleComponent]
    public class HandDoor : MonoBehaviour
    {
        public const float MaxAngle = 95f;
        public const float OpenThreshold = 45f;

        [Tooltip("Segundos para ir de fechada a totalmente aberta pelo Toggle.")]
        public float animationSeconds = .65f;

        public float Angle { get; private set; }
        public bool IsOpen => Angle > OpenThreshold;
        public bool Following => hand;

        Transform hand;
        float handStartYaw, doorStartAngle;
        bool animating;
        float animFrom, animTo, animTime, animDuration;

        /// <summary>Liga a maçaneta: pegar com controle segue a mão; seleção sem controle alterna.</summary>
        public void Bind(XRBaseInteractable handle)
        {
            handle.selectEntered.AddListener(OnSelectEntered);
            handle.selectExited.AddListener(OnSelectExited);
        }

        /// <summary>Anima até fechada, se estiver aberta, ou até totalmente aberta. Parte do ângulo atual.</summary>
        public void Toggle()
        {
            hand = null;
            animFrom = Angle;
            animTo = IsOpen ? 0f : MaxAngle;
            animDuration = animationSeconds * Mathf.Abs(animTo - animFrom) / MaxAngle;
            animTime = 0f;
            animating = animDuration > 0f;
            if (!animating) Apply(animTo);
        }

        public void BeginFollow(Transform follower)
        {
            animating = false;
            hand = follower;
            handStartYaw = YawAroundHinge(follower.position);
            doorStartAngle = Angle;
        }

        public void EndFollow() => hand = null;

        public void Tick(float deltaTime)
        {
            if (hand)
            {
                Apply(doorStartAngle + Mathf.DeltaAngle(handStartYaw, YawAroundHinge(hand.position)));
            }
            else if (animating)
            {
                animTime += deltaTime;
                if (animTime >= animDuration) { animating = false; Apply(animTo); }
                else Apply(Mathf.SmoothStep(animFrom, animTo, animTime / animDuration));
            }
        }

        void Update() => Tick(Time.deltaTime);

        void Apply(float angle)
        {
            Angle = Mathf.Clamp(angle, 0f, MaxAngle);
            transform.localRotation = Quaternion.Euler(0f, Angle, 0f);
        }

        /// <summary>Ângulo de um ponto do mundo em volta da dobradiça, no espaço do pai (a sala).</summary>
        float YawAroundHinge(Vector3 world)
        {
            var parent = transform.parent;
            var local = (parent ? parent.InverseTransformPoint(world) : world) - transform.localPosition;
            return Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            // Sem interactor: chamada do modo PC ou de validador.
            var interactor = args != null ? args.interactorObject as Object : null;
            if (!interactor) { Toggle(); return; }
            var follower = args.interactorObject.transform;
            BeginFollow(follower);
            var haptics = follower.GetComponentInParent<HapticImpulsePlayer>();
            if (haptics) haptics.SendHapticImpulse(.3f, .05f);
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            var interactor = args != null ? args.interactorObject as Object : null;
            if (hand && interactor && args.interactorObject.transform == hand) EndFollow();
        }
    }
}
```

- [ ] **Step 4: rodar e ver passar**

Rodar **Compilar** (esperado `saída 0`, sem avisos em `SebraeLab`) e depois **Teste da porta**. Esperado: `saída 0` e `Logs/hand-door-check.txt` com 10 linhas `OK` e `RESULTADO: OK`.

- [ ] **Step 5: commit**

```bash
git add Assets/SebraeLab/Scripts/Missions/HandDoor.cs Assets/SebraeLab/Scripts/Missions/HandDoor.cs.meta Assets/SebraeLab/Editor/Validation/HandDoorCheck.cs Assets/SebraeLab/Editor/Validation/HandDoorCheck.cs.meta
git commit -m "Cria a porta que segue a mão, com teste em EditMode" -m "HandDoor concentra a lógica da porta da Sala 2: segue o ângulo do controle em volta da dobradiça enquanto a maçaneta está segura e anima com Toggle no modo PC. HandDoorCheck roda em batchmode."
```

---

### Task 2: `InternshipDay` usa o `HandDoor`

**Files:**
- Modify: `Assets/SebraeLab/Scripts/Missions/InternshipDay.cs` (campos da porta, criação da maçaneta, `RoomHint`, `ToggleDoor`, `Update`)

**Interfaces:**
- Consumes: `HandDoor` da Task 1 (`Bind`, `Toggle`, `IsOpen`).
- Produces: `InternshipDay.DoorOpen` continua público (usado por `ManagerMission`, `DesktopWalkCheck`, `ExpedienteValidation`, `DemoVisivel`), agora delegando a `HandDoor.IsOpen`.

- [ ] **Step 1: trocar os campos da porta**

Substituir:

```csharp
        Transform hinge;
        float doorProgress;
        bool opening;
        public bool DoorOpen => hinge && Quaternion.Angle(Quaternion.identity, hinge.localRotation) > 45;
```

por:

```csharp
        Transform hinge;
        HandDoor door;
        public bool DoorOpen => door && door.IsOpen;
```

- [ ] **Step 2: criar o `HandDoor` e ligar a maçaneta**

Substituir:

```csharp
            hinge = new GameObject("Dobradiça da sala").transform;
```

por:

```csharp
            hinge = new GameObject("Dobradiça da sala").transform;
            door = hinge.gameObject.AddComponent<HandDoor>();
```

E substituir:

```csharp
            grip.selectEntered.AddListener(e => ToggleDoor());
```

por:

```csharp
            door.Bind(grip);
```

- [ ] **Step 3: `RoomHint`, `ToggleDoor` e `Update`**

Em `RoomHint`, substituir `return opening?"[F] Fechar a porta":"[F] Abrir a porta";` por `return door.IsOpen?"[F] Fechar a porta":"[F] Abrir a porta";`.

Substituir `void ToggleDoor() { opening = !opening; }` por `void ToggleDoor() => door.Toggle();`.

No `Update`, remover a animação (o `HandDoor` anima sozinho). Substituir:

```csharp
            float target=opening?1f:0f;
            if(mission.playerRoot.position.y < -2f) Move(new Vector3(5.7f,.05f,7.1f));
            if (doorProgress!=target){
                doorProgress=Mathf.MoveTowards(doorProgress,target,Time.deltaTime/.65f);
                hinge.localRotation=Quaternion.Euler(0,Mathf.SmoothStep(0,95,doorProgress),0);
            }
```

por:

```csharp
            if(mission.playerRoot.position.y < -2f) Move(new Vector3(5.7f,.05f,7.1f));
```

- [ ] **Step 4: verificar**

Confirmar que não sobrou uso de `opening` nem `doorProgress`:

```bash
grep -n -E "\bopening\b|doorProgress" Assets/SebraeLab/Scripts/Missions/InternshipDay.cs || echo "nenhum"
```

Rodar **Compilar**, **Teste da porta** e **Percurso do Richardson**. Esperado: compilação limpa, `RESULTADO: OK` na porta e, no `desktop-walk.txt`, as linhas `2 OK sala`, `Porta abriu, fechou e reabriu depois de concluir a missão` e `Recomeçar o dia: recepção e objetivo inicial`, sem `FALHA`. Depois, **Limpar ruído do editor**.

- [ ] **Step 5: commit**

```bash
git add Assets/SebraeLab/Scripts/Missions/InternshipDay.cs
git commit -m "Liga a porta da Sala 2 ao HandDoor" -m "No Quest a porta passa a seguir a mão ao segurar a maçaneta. No modo PC, F e clique continuam abrindo e fechando com animação. DoorOpen delega a HandDoor.IsOpen."
```

---

### Task 3: validador `NavigationCheck` (teste que falha antes das correções)

**Files:**
- Create: `Assets/SebraeLab/Editor/Validation/NavigationCheck.cs`

**Interfaces:**
- Consumes: `HandDoor.IsOpen`, `HandDoor.Toggle()` (Task 1); `ManagerMission.day`, `ManagerMission.playerRoot`, `ManagerMission.playerHead`; `ControllerInputActionManager.smoothMotionEnabled` (Starter Assets).
- Produces: `Logs/navigation-check.txt` com seções `Locomoção`, `Iluminação`, `Tetos`, `Rota`, `Custo por ambiente` e a última linha `RESULTADO: OK` ou `RESULTADO: FALHA (n)`. As Tasks 4 a 7 usam esse relatório como critério de aceite.

- [ ] **Step 1: escrever o validador**

`Assets/SebraeLab/Editor/Validation/NavigationCheck.cs`:

```csharp
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

            Locomocao(r);
            Iluminacao(r);
            Tetos(r);

            r.Secao("Rota");
            var rig = Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.gameObject.name == "Jogador VR - Recepcao");
            if (!rig) r.Falha("CharacterController do rig 'Jogador VR - Recepcao' não encontrado");
            else
            {
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

            r.Secao("Custo por ambiente (editor, referência)");
            yield return Custo(mission, r);

            r.Texto.AppendLine().AppendLine(r.Falhas == 0 ? "RESULTADO: OK" : $"RESULTADO: FALHA ({r.Falhas})");
            Directory.CreateDirectory("Logs");
            File.WriteAllText(Report, r.Texto.ToString());
            Debug.Log("NavigationCheck\n" + r.Texto);
            if (SessionState.GetBool(ExitKey, false)) EditorApplication.Exit(r.Falhas == 0 ? 0 : 1);
        }

        static void Locomocao(Relatorio r)
        {
            r.Secao("Locomoção");
            var esquerdo = Object.FindObjectsByType<ControllerInputActionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.gameObject.name == "Left Controller");
            if (!esquerdo) r.Falha("ControllerInputActionManager do Left Controller não encontrado");
            else if (esquerdo.smoothMotionEnabled) r.Ok("movimento contínuo ligado no controle esquerdo");
            else r.Falha("movimento contínuo desligado no controle esquerdo (override na cena)");
        }

        static void Iluminacao(Relatorio r)
        {
            r.Secao("Iluminação");
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

            var nivel = QualitySettings.names[QualitySettings.GetQualityLevel()];
            if (QualitySettings.antiAliasing == 4) r.Ok($"MSAA 4x no nível {nivel}");
            else r.Falha($"MSAA {QualitySettings.antiAliasing}x no nível {nivel} (esperado 4x)");
        }

        static void Tetos(Relatorio r)
        {
            r.Secao("Tetos");
            var colisores = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
            foreach (var nome in PisosCobertos)
            {
                var pisos = colisores.Where(c => c.name == nome && !c.isTrigger).ToArray();
                if (pisos.Length == 0) { r.Falha($"{nome}: piso não encontrado"); continue; }
                int total = 0;
                var descobertos = new List<Vector3>();
                foreach (var piso in pisos)
                {
                    var b = piso.bounds;
                    for (float x = b.min.x + .25f; x < b.max.x; x += .75f)
                    for (float z = b.min.z + .25f; z < b.max.z; z += .75f)
                    {
                        if (!piso.Raycast(new Ray(new Vector3(x, b.max.y + 1f, z), Vector3.down), out var chao, 3f)) continue;
                        total++;
                        var coberto = Physics.RaycastAll(chao.point + Vector3.up * 1.2f, Vector3.up, 6f, ~0, QueryTriggerInteraction.Ignore)
                            .Any(h => h.point.y >= chao.point.y + 2.2f);
                        if (!coberto) descobertos.Add(chao.point);
                    }
                }
                if (descobertos.Count == 0) r.Ok($"{nome}: {total} pontos, todos cobertos");
                else r.Falha($"{nome}: {descobertos.Count} de {total} pontos sem teto, por exemplo " + string.Join(" ", descobertos.Take(3).Select(Fmt)));
            }
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
```

- [ ] **Step 2: rodar e registrar a linha de base**

Rodar **Compilar** (esperado limpo) e **Verificação de navegação**. Esperado: `RESULTADO: FALHA` com, no mínimo, estas falhas, que as próximas tarefas corrigem:
- `Locomoção`: `movimento contínuo desligado no controle esquerdo (override na cena)`;
- `Iluminação`: luz direcional fora de Important, luzes pontuais fora do modo por vértice, luminárias sem `LuminariaAcesa`, MSAA 0x;
- `Tetos`: `Galeria da praça: ... sem teto`.

Qualquer outra falha (outro piso sem teto, rota sem caminho, queda, travamento) deve ser registrada no relatório da tarefa e levada ao Gilberto antes de mudar o escopo. Ajustar um ponto da `Rota` só é permitido quando o ponto cai dentro de um objeto (mesa, parede) e o trecho seguinte continua passando pelo mesmo ambiente. Depois, **Limpar ruído do editor**.

- [ ] **Step 3: commit**

```bash
git add Assets/SebraeLab/Editor/Validation/NavigationCheck.cs Assets/SebraeLab/Editor/Validation/NavigationCheck.cs.meta
git commit -m "Cria a verificação de navegação, tetos e luzes" -m "Em Play, anda por todos os ambientes com um corpo do tamanho do rig VR, mede o maior degrau, procura pisos cobertos sem teto, confere o modo das luzes, o MSAA e a locomoção do controle esquerdo, e anota o custo de cada ambiente."
```

---

### Task 4: locomoção livre no controle esquerdo

**Files:**
- Modify: `Assets/SebraeLab/Scenes/SebraeLab_MissaoGerente.unity` (override do rig)

**Interfaces:**
- Consumes: `NavigationCheck`, seção `Locomoção` (Task 3).
- Produces: nada para outras tarefas.

- [ ] **Step 1: confirmar a falha**

No relatório da Task 3, a seção `Locomoção` mostra `FALHA movimento contínuo desligado no controle esquerdo`.

- [ ] **Step 2: remover o override**

Na cena, remover exatamente estas 4 linhas (entrada de `m_Modifications` do prefab do rig, guid `f6336ac4ac8b4d34bc5072418cdc62a0`):

```yaml
    - target: {fileID: 4778211696441940833, guid: f6336ac4ac8b4d34bc5072418cdc62a0, type: 3}
      propertyPath: m_SmoothMotionEnabled
      value: 0
      objectReference: {fileID: 0}
```

Conferir:

```bash
grep -c "m_SmoothMotionEnabled" Assets/SebraeLab/Scenes/SebraeLab_MissaoGerente.unity
```

Esperado: `0`. Sem o override, vale o valor do prefab do Left Controller (`1`).

No Inspector, a mesma mudança fica em: **Jogador VR - Recepcao → Camera Offset → Left Controller → Controller Input Action Manager → Smooth Motion Enabled** marcado.

- [ ] **Step 3: verificar**

Rodar **Compilar** e **Verificação de navegação**. Esperado: `OK    movimento contínuo ligado no controle esquerdo`; as demais falhas da linha de base continuam. **Limpar ruído do editor**.

- [ ] **Step 4: commit**

```bash
git add Assets/SebraeLab/Scenes/SebraeLab_MissaoGerente.unity
git commit -m "Liga a locomoção livre no controle esquerdo" -m "A cena desligava o movimento contínuo do Left Controller por override. Sem ele, o analógico esquerdo anda e o direito continua com giro de 45 graus e teleporte."
```

---

### Task 5: teto na Galeria da praça

**Files:**
- Modify: `Assets/SebraeLab/Scripts/Missions/InternshipDay.cs` (bloco das chamadas `AddLitRoof` em `Initialize`)

**Interfaces:**
- Consumes: `NavigationCheck`, seção `Tetos` (Task 3); `AddLitRoof(Vector3 center, Vector3 size)` existente.
- Produces: nada para outras tarefas.

- [ ] **Step 1: confirmar a falha**

No último relatório, `FALHA Galeria da praça: ... sem teto`.

- [ ] **Step 2: cobrir a galeria**

Depois de:

```csharp
            AddLitRoof(new Vector3(36.5f,3,0),new Vector3(9,.12f,3.2f));
```

acrescentar:

```csharp
            AddLitRoof(new Vector3(14,3,2.5f),new Vector3(4,.12f,9)); // Galeria da praça
```

- [ ] **Step 3: verificar**

Rodar **Compilar** e **Verificação de navegação**. Esperado: `OK    Galeria da praça: ... todos cobertos` e a seção `Rota` sem queda nem travamento na galeria. **Limpar ruído do editor**.

- [ ] **Step 4: commit**

```bash
git add Assets/SebraeLab/Scripts/Missions/InternshipDay.cs
git commit -m "Cobre a Galeria da praça" -m "Era o único ambiente de passagem sem teto. Usa o mesmo AddLitRoof das outras galerias, com duas luminárias."
```

---

### Task 6: iluminação em tempo real para o Quest

**Files:**
- Create: `Assets/SebraeLab/Resources/Acabamentos/LuminariaAcesa.mat`
- Modify: `Assets/SebraeLab/Scripts/Missions/MissionSurfaces.cs:113`
- Modify: `Assets/SebraeLab/Scripts/Missions/InternshipDay.cs` (`AddLitRoof` e luzes "Luz quente" em `BuildLounge`)
- Modify: `Assets/SebraeLab/Scenes/SebraeLab_MissaoGerente.unity` (modo da luz direcional)
- Modify: `ProjectSettings/QualitySettings.asset` (MSAA do nível Medium)

**Interfaces:**
- Consumes: `NavigationCheck`, seção `Iluminação` (Task 3).
- Produces: material `Acabamentos/LuminariaAcesa`, carregado por `Resources.Load<Material>("Acabamentos/LuminariaAcesa")` em `MissionSurfaces.Get`.

- [ ] **Step 1: confirmar as falhas**

No último relatório, a seção `Iluminação` tem 4 `FALHA`: direcional, pontuais, luminárias e MSAA.

- [ ] **Step 2: luzes pontuais por vértice**

Em `AddLitRoof`, substituir:

```csharp
                var lamp=panel.AddComponent<Light>();lamp.color=new Color(1,.94f,.84f);lamp.intensity=1.5f;lamp.range=7;
```

por:

```csharp
                var lamp=panel.AddComponent<Light>();lamp.color=new Color(1,.94f,.84f);lamp.intensity=1.5f;lamp.range=7;lamp.renderMode=LightRenderMode.ForceVertex;
```

Em `BuildLounge`, substituir:

```csharp
                light.transform.position=new Vector3(x,2.9f,z);light.color=new Color(1,.89f,.73f);light.range=5;light.intensity=1.3f;
```

por:

```csharp
                light.transform.position=new Vector3(x,2.9f,z);light.color=new Color(1,.89f,.73f);light.range=5;light.intensity=1.3f;light.renderMode=LightRenderMode.ForceVertex;
```

- [ ] **Step 3: material emissivo das luminárias**

Criar `Assets/SebraeLab/Resources/Acabamentos/LuminariaAcesa.mat`:

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: LuminariaAcesa
  m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}
  m_Parent: {fileID: 0}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords:
  - _EMISSION
  m_InvalidKeywords: []
  m_LightmapFlags: 2
  m_EnableInstancingVariants: 1
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {}
  disabledShaderPasses: []
  m_LockedProperties: 
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _BumpMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _DetailAlbedoMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _DetailMask:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _DetailNormalMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _EmissionMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _MainTex:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _MetallicGlossMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _OcclusionMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _ParallaxMap:
        m_Texture: {fileID: 0}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    m_Ints: []
    m_Floats:
    - _BumpScale: 1
    - _Cutoff: 0.5
    - _DetailNormalMapScale: 1
    - _DstBlend: 0
    - _GlossMapScale: 1
    - _Glossiness: 0.2
    - _GlossyReflections: 1
    - _Metallic: 0
    - _Mode: 0
    - _OcclusionStrength: 1
    - _Parallax: 0.02
    - _SmoothnessTextureChannel: 0
    - _SpecularHighlights: 1
    - _SrcBlend: 1
    - _UVSec: 0
    - _ZWrite: 1
    m_Colors:
    - _Color: {r: 1, g: 0.96, b: 0.88, a: 1}
    - _EmissionColor: {r: 1.6, g: 1.45, b: 1.2, a: 1}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
```

Em `MissionSurfaces.cs`, substituir:

```csharp
                case "Luminária":case "Pé de aço":finish="Aco";break;
```

por:

```csharp
                case "Luminária":finish="LuminariaAcesa";break;
                case "Pé de aço":finish="Aco";break;
```

- [ ] **Step 4: luz direcional Important**

Na cena, no bloco `Light` da luz direcional (`--- !u!108 &600421089`), substituir:

```yaml
  m_RenderMode: 0
  m_CullingMask:
```

por:

```yaml
  m_RenderMode: 1
  m_CullingMask:
```

Conferir que só esse bloco mudou:

```bash
git diff -U0 Assets/SebraeLab/Scenes/SebraeLab_MissaoGerente.unity | grep "^[-+] " 
```

Esperado: apenas `-  m_RenderMode: 0` e `+  m_RenderMode: 1`, além das linhas removidas na Task 4 se ela ainda não foi commitada.

- [ ] **Step 5: MSAA 4x no nível Medium**

```bash
awk '/^    name: /{nivel=$2} nivel=="Medium" && /^    antiAliasing: /{sub(/antiAliasing: [0-9]+/,"antiAliasing: 4")} {print}' ProjectSettings/QualitySettings.asset > Logs/qs.tmp && mv Logs/qs.tmp ProjectSettings/QualitySettings.asset
git diff ProjectSettings/QualitySettings.asset
```

Esperado: uma única linha alterada, `antiAliasing: 0` para `antiAliasing: 4`, dentro do bloco `name: Medium`.

- [ ] **Step 6: verificar**

Rodar **Compilar** e **Verificação de navegação**. Esperado: a seção `Iluminação` com 4 `OK`. **Limpar ruído do editor**, sem descartar `QualitySettings.asset`.

- [ ] **Step 7: commit**

```bash
git add Assets/SebraeLab/Resources/Acabamentos/LuminariaAcesa.mat Assets/SebraeLab/Resources/Acabamentos/LuminariaAcesa.mat.meta Assets/SebraeLab/Scripts/Missions/MissionSurfaces.cs Assets/SebraeLab/Scripts/Missions/InternshipDay.cs Assets/SebraeLab/Scenes/SebraeLab_MissaoGerente.unity ProjectSettings/QualitySettings.asset
git commit -m "Ajusta a iluminação em tempo real para o Quest 2" -m "A luz direcional fica como a única luz por pixel. As luzes pontuais das luminárias passam a ser por vértice e as luminárias ganham material emissivo para parecerem acesas sem custo de luz. O perfil Android passa a usar MSAA 4x."
```

---

### Task 7: degrau do jogador, verificação final e APK

**Files:**
- Modify (condicional): `Assets/SebraeLab/Scenes/SebraeLab_MissaoGerente.unity` (override de `m_StepOffset` do rig)

**Interfaces:**
- Consumes: `NavigationCheck`, seção `Rota`, linha `Maior subida em um passo` (Task 3).
- Produces: APK `Builds/Quest/MissaoEstagio.apk`.

- [ ] **Step 1: decidir o degrau pela medição**

Ler no último relatório a linha `Maior subida em um passo: X m`.

Se X for **até 0,25 m**: na cena, acrescentar esta entrada em `m_Modifications` do prefab do rig, logo antes da linha `    m_RemovedComponents: []` que antecede `m_SourcePrefab: {fileID: 100100000, guid: f6336ac4ac8b4d34bc5072418cdc62a0, type: 3}`:

```yaml
    - target: {fileID: 6232745470614056090, guid: f6336ac4ac8b4d34bc5072418cdc62a0, type: 3}
      propertyPath: m_StepOffset
      value: 0.3
      objectReference: {fileID: 0}
```

No Inspector, é o campo **Step Offset** do **Character Controller** em **Jogador VR - Recepcao**.

Se X for **maior que 0,25 m**: não mudar nada e registrar no relatório da tarefa o valor e a posição.

- [ ] **Step 2: verificação final**

Rodar, nesta ordem: **Compilar**, **Teste da porta**, **Verificação de navegação** e **Percurso do Richardson**. Esperado:
- compilação sem erros e sem avisos em `SebraeLab`;
- `hand-door-check.txt` com `RESULTADO: OK`;
- `navigation-check.txt` com `RESULTADO: OK` e, se o Step 1 mudou o degrau, `degrau 0.3 m` na linha do corpo;
- `desktop-walk.txt` até `Recomeçar o dia: recepção e objetivo inicial`, sem `FALHA`.

**Limpar ruído do editor**.

- [ ] **Step 3: gerar o APK**

Rodar **APK** (PowerShell, via WMI). Esperado: `Build Finished, Result: Success` e `MissaoEstagio.apk` com data atual. Depois, **Limpar ruído do editor** (a build mexe em `AndroidManifest.xml`, `ProjectSettings.asset` e cria `Assets/Resources`).

- [ ] **Step 4: commit (se o Step 1 mudou a cena)**

```bash
git add Assets/SebraeLab/Scenes/SebraeLab_MissaoGerente.unity
git commit -m "Reduz o degrau do jogador para 30 cm" -m "Com 50 cm o jogador subia em bancos e sofás andando com o analógico. A maior subida medida na rota completa cabe em 30 cm."
```
