using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using SebraeLab.Missions;

namespace SebraeLab.Editor
{
    [InitializeOnLoad]
    public static class ManagerMissionSetup
    {
        const string Root = "Assets/SebraeLab/Missions/Manager";
        public const string MissionScene = "Assets/SebraeLab/Scenes/SebraeLab_MissaoGerente.unity";
        static readonly Vector3 NpcPosition = new Vector3(2.2f, 0, 6.5f);
        static readonly Vector3 RoomPosition = new Vector3(-3, 0, -5);
        static Material blue, skin, dark, white, hair, gold, marker;
        static ManagerMissionSetup() { EditorApplication.delayCall += Auto; }
        static void Auto()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Auto; return; }
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !File.Exists(MissionScene)) Create();
        }

        static Material Mat(string name, Color color)
        {
            var path = Root + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, path); }
            mat.color = color;
            mat.SetFloat("_Glossiness", .12f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }
        static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }
        static Text Text(Transform parent, string name, string value, Vector2 pos, Vector2 size, int font, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>(); t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = value; t.fontSize = font; t.color = color; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = size; rt.anchoredPosition = pos;
            return t;
        }
        static GameObject Panel(Transform parent, string name, Vector3 pos, Vector2 size, float yaw)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(Image), typeof(TrackedDeviceGraphicRaycaster));
            go.transform.SetParent(parent, false); go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0); go.transform.localScale = Vector3.one * .0015f;
            go.GetComponent<RectTransform>().sizeDelta = size; go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var image = go.GetComponent<Image>(); image.color = new Color(.025f, .065f, .12f, .98f); image.raycastTarget = false;
            return go;
        }
        static Button Button(Transform parent, string name, Vector2 pos, string caption, out Text label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(850, 90);
            go.GetComponent<Image>().color = new Color(.04f, .46f, .78f);
            var button = go.GetComponent<Button>(); var colors = button.colors; colors.highlightedColor = new Color(.45f, .9f, 1); colors.disabledColor = new Color(.4f, .4f, .4f, .7f); button.colors = colors;
            label = Text(go.transform, "Texto", caption, Vector2.zero, new Vector2(830, 80), 32, Color.white);
            return button;
        }
        static GameObject MakeNpc()
        {
            var root = new GameObject("Gerente - personagem estilizado");
            Part(root.transform, "Blazer", PrimitiveType.Capsule, new Vector3(0, 1.05f, 0), new Vector3(.5f, .42f, .29f), blue);
            Part(root.transform, "Camisa", PrimitiveType.Cube, new Vector3(0, 1.18f, .145f), new Vector3(.17f, .4f, .018f), white);
            Part(root.transform, "Gravata", PrimitiveType.Cube, new Vector3(0, 1.2f, .16f), new Vector3(.045f, .24f, .016f), gold);
            Part(root.transform, "Cracha", PrimitiveType.Cube, new Vector3(-.14f, 1.24f, .145f), new Vector3(.09f, .065f, .018f), gold);
            Part(root.transform, "Pescoco", PrimitiveType.Cylinder, new Vector3(0, 1.47f, 0), new Vector3(.14f, .06f, .14f), skin);
            Part(root.transform, "Cabeca", PrimitiveType.Sphere, new Vector3(0, 1.66f, 0), Vector3.one * .39f, skin);
            Part(root.transform, "Cabelo", PrimitiveType.Sphere, new Vector3(0, 1.82f, -.008f), new Vector3(.405f, .13f, .39f), hair);
            for (int side = -1; side <= 1; side += 2)
            {
                var arm = Part(root.transform, side < 0 ? "Braco esquerdo" : "Braco direito", PrimitiveType.Capsule, new Vector3(side * .34f, 1.04f, 0), new Vector3(.14f, .36f, .14f), blue);
                arm.transform.localRotation = Quaternion.Euler(0, 0, side * 7);
                Part(root.transform, "Mao", PrimitiveType.Sphere, new Vector3(side * .39f, .65f, .02f), Vector3.one * .16f, skin);
                Part(root.transform, "Perna", PrimitiveType.Capsule, new Vector3(side * .14f, .36f, 0), new Vector3(.18f, .3f, .19f), dark);
                Part(root.transform, "Sapato", PrimitiveType.Cube, new Vector3(side * .14f, .07f, .045f), new Vector3(.22f, .14f, .34f), dark);
                Part(root.transform, "Olho", PrimitiveType.Sphere, new Vector3(side * .07f, 1.71f, .179f), new Vector3(.035f, .04f, .025f), dark);
            }
            Part(root.transform, "Sorriso", PrimitiveType.Cube, new Vector3(0, 1.61f, .188f), new Vector3(.1f, .015f, .018f), dark);
            var body = root.AddComponent<CapsuleCollider>(); body.center = Vector3.up * .93f; body.height = 1.86f; body.radius = .25f;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/Gerente.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        }

        [MenuItem("Sebrae Lab/Missao 01/1 - Montar missao do gerente")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Pare o Play antes de montar a missão.");
            if (File.Exists(MissionScene)) { Open(); return; }
            foreach (var sub in new[] { "Materials", "Prefabs" }) Directory.CreateDirectory(Root + "/" + sub);
            AssetDatabase.Refresh();
            var previous = SceneManager.GetActiveScene();
            if (previous.isDirty && !File.Exists("Assets/SebraeLab/Scenes/Local_Backup_AntesMissao.unity"))
            {
                EditorSceneManager.SaveScene(previous, "Assets/SebraeLab/Scenes/Local_Backup_AntesMissao.unity", true);
                Debug.Log("Cópia local da cena anterior preservada em Local_Backup_AntesMissao.unity");
            }
            if (!File.Exists(MissionScene) && !AssetDatabase.CopyAsset(EnvironmentSetup.ScenePath, MissionScene)) throw new Exception("Não foi possível copiar a cena de exploração.");
            EditorSceneManager.OpenScene(MissionScene);
            blue = Mat("Blazer_Azul", new Color(.04f, .23f, .46f));
            skin = Mat("Pele", new Color(.62f, .36f, .23f)); dark = Mat("Azul_Escuro", new Color(.035f, .055f, .075f));
            white = Mat("Camisa", new Color(.94f, .96f, 1)); hair = Mat("Cabelo", new Color(.09f, .055f, .04f));
            gold = Mat("Cracha", new Color(1, .65f, .12f)); marker = Mat("Sinalizacao_Azul", new Color(.02f, .62f, .95f));
            var quest = new GameObject("Missao 01 - A busca pelo gerente"); var mission = quest.AddComponent<ManagerMission>();
            var rig = GameObject.Find("Jogador VR - Recepcao"); mission.playerRoot = rig.transform; mission.playerHead = rig.GetComponentInChildren<Camera>(true).transform;
            var npc = MakeNpc(); npc.transform.SetParent(quest.transform, false); npc.transform.SetPositionAndRotation(NpcPosition, Quaternion.Euler(0, 90, 0));
            mission.manager = npc.transform; npc.AddComponent<XRSimpleInteractable>(); npc.AddComponent<ManagerNpcInteraction>().mission = mission;
            Part(quest.transform, "Ponto do gerente", PrimitiveType.Cylinder, NpcPosition + Vector3.up * .008f, new Vector3(.9f, .008f, .9f), marker);
            var name = Panel(quest.transform, "Identificacao do gerente", NpcPosition + Vector3.up * 2.12f, new Vector2(900, 150), -90);
            Text(name.transform, "Nome", "GESTOR DO ESPAÇO", Vector2.zero, new Vector2(870, 130), 40, Color.white);
            var panel = Panel(quest.transform, "Conversa com o gerente", new Vector3(2.7f, 1.48f, 4.85f), new Vector2(1200, 680), -90);
            mission.objectiveText = Text(panel.transform, "Objetivo", "MISSÃO 01", new Vector2(0, 235), new Vector2(1120, 100), 33, new Color(.3f, .85f, 1));
            mission.dialogueText = Text(panel.transform, "Dialogo", "Encontre o gestor e converse com ele para receber sua primeira missão.", new Vector2(0, 25), new Vector2(1060, 280), 34, Color.white);
            mission.conversationButton = Button(panel.transform, "Conversar e avancar", new Vector2(0, -190), "CONVERSAR", out var action);
            mission.actionText = action; UnityEventTools.AddPersistentListener(mission.conversationButton.onClick, mission.Interact);
            mission.proximityText = Text(panel.transform, "Proximidade", "Aproxime-se do gerente para conversar.", new Vector2(0, -292), new Vector2(1120, 75), 25, new Color(.8f, .86f, .92f));
            var zone = new GameObject("Sala do estagiario - area de conclusao", typeof(BoxCollider), typeof(MissionRoomZone)); zone.transform.SetParent(quest.transform, false); zone.transform.position = RoomPosition;
            var box = zone.GetComponent<BoxCollider>(); box.isTrigger = true; box.center = new Vector3(0, 1.25f, 0); box.size = new Vector3(1.6f, 2.5f, 1.4f); mission.room = zone.GetComponent<MissionRoomZone>();
            var roomMarker = new GameObject("Sinalizacao - Sua sala"); roomMarker.transform.SetParent(quest.transform, false); mission.roomMarker = roomMarker;
            var label = Panel(roomMarker.transform, "Placa da sala", RoomPosition + Vector3.up * 2.12f, new Vector2(800, 150), -90);
            Text(label.transform, "Nome", "SUA SALA", Vector2.zero, new Vector2(780, 130), 48, Color.white);
            foreach (var offset in new[] { new Vector3(0, 0, -.7f), new Vector3(0, 0, .7f) })
                Part(roomMarker.transform, "Limite da sala", PrimitiveType.Cube, RoomPosition + offset + Vector3.up * .012f, new Vector3(1.6f, .012f, .04f), marker);
            foreach (var offset in new[] { new Vector3(-.8f, 0, 0), new Vector3(.8f, 0, 0) })
                Part(roomMarker.transform, "Limite da sala", PrimitiveType.Cube, RoomPosition + offset + Vector3.up * .012f, new Vector3(.04f, .012f, 1.4f), marker);
            var done = Panel(quest.transform, "Missao concluida - painel", new Vector3(-4.65f, 1.5f, -4.9f), new Vector2(1100, 620), -90);
            mission.completionPanel = done;
            Text(done.transform, "Titulo", "MISSÃO CONCLUÍDA!", new Vector2(0, 190), new Vector2(1050, 100), 45, new Color(.35f, 1, .65f));
            Text(done.transform, "Mensagem", "Você encontrou sua sala.\nBem-vindo ao seu primeiro dia no Sebrae Lab!", new Vector2(0, 20), new Vector2(980, 180), 35, Color.white);
            var restart = Button(done.transform, "Reiniciar", new Vector2(0, -180), "REINICIAR MISSÃO", out var unused); UnityEventTools.AddPersistentListener(restart.onClick, mission.RestartMission);
            if (!UnityEngine.Object.FindFirstObjectByType<EventSystem>())
            {
                var es = new GameObject("EventSystem XR", typeof(EventSystem), typeof(XRUIInputModule));
                es.GetComponent<XRUIInputModule>().enableXRInput = true; es.GetComponent<XRUIInputModule>().enableMouseInput = true;
            }
            var simulator = GameObject.Find("Simulador PC - excluido da build"); if (simulator) simulator.SetActive(false);
            Physics.SyncTransforms();
            var approach = new Vector3(3.4f, 0, 6.7f);
            var startPath = FindPath(rig.transform.position, approach);
            var pathToRoom = FindPath(approach, RoomPosition);
            var route = new GameObject("Marcacoes azuis - caminho ate a sala"); route.transform.SetParent(quest.transform, false); mission.routeMarkers = route;
            Vector3 last = pathToRoom[0] - Vector3.one * 10;
            for (int i = 0; i < pathToRoom.Count; i++)
                if (Vector3.Distance(last, pathToRoom[i]) > 1.1f || i == pathToRoom.Count - 1)
                {
                    last = pathToRoom[i]; Part(route.transform, "Marca de percurso", PrimitiveType.Cube, last + Vector3.up * .012f, new Vector3(.2f, .012f, .2f), marker);
                }
            route.SetActive(false); roomMarker.SetActive(false); done.SetActive(false);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), MissionScene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MissionScene, true) };
            AssetDatabase.SaveAssets(); Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/manager-mission-route.txt", "Recepção até gerente: " + startPath.Count + " pontos. Gerente até sala: " + pathToRoom.Count + " pontos. Caminhos verificados com cápsula de raio 0,22 m.");
            Debug.Log("SEBRAE_MANAGER_MISSION_READY");
        }

        static List<Vector3> FindPath(Vector3 from, Vector3 to)
        {
            const float step = .3f; const int width = 48, height = 59; var origin = new Vector3(-6.3f, 0, -8.4f);
            var free = new Dictionary<Vector2Int, bool>();
            var playerRoot = GameObject.Find("Jogador VR - Recepcao").transform;
            Func<Vector2Int, Vector3> world = p => origin + new Vector3(p.x * step, 0, p.y * step);
            Func<Vector2Int, bool> clear = p =>
            {
                if (p.x < 0 || p.x >= width || p.y < 0 || p.y >= height) return false;
                if (free.TryGetValue(p, out var result)) return result;
                var pos = world(p);
                bool onFloor = Physics.RaycastAll(pos + Vector3.up * .2f, Vector3.down, .4f, ~0, QueryTriggerInteraction.Ignore).Any(h => h.collider.GetComponent<TeleportationArea>() && Mathf.Abs(h.point.y) < .04f);
                result = onFloor && !Physics.OverlapCapsule(pos + Vector3.up * .25f, pos + Vector3.up * 1.65f, .22f, ~0, QueryTriggerInteraction.Ignore).Any(c => !c.transform.IsChildOf(playerRoot) && c.transform != playerRoot);
                free[p] = result; return result;
            };
            Func<Vector3, Vector2Int> cell = p => new Vector2Int(Mathf.RoundToInt((p.x - origin.x) / step), Mathf.RoundToInt((p.z - origin.z) / step));
            var start = cell(from); var end = cell(to);
            if (!clear(start) || !clear(end)) throw new Exception("Ponto da rota bloqueado: " + from + " -> " + to);
            var queue = new Queue<Vector2Int>(); var previous = new Dictionary<Vector2Int, Vector2Int>(); queue.Enqueue(start); previous[start] = start;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue(); if (current == end) break;
                foreach (var offset in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                {
                    var next = current + offset; if (previous.ContainsKey(next) || !clear(next)) continue;
                    previous[next] = current; queue.Enqueue(next);
                }
            }
            if (!previous.ContainsKey(end)) throw new Exception("Não há passagem livre até a sala. Ajuste o destino antes de continuar.");
            var path = new List<Vector3>(); for (var cur = end; ; cur = previous[cur]) { path.Add(world(cur)); if (cur == start) break; }
            path.Reverse(); return path;
        }

        [MenuItem("Sebrae Lab/Missao 01/2 - Abrir missao do gerente")]
        public static void Open() { EditorSceneManager.OpenScene(MissionScene); }

        [MenuItem("Sebrae Lab/Entrega/Gerar APK do expediente completo")]
        public static void Build()
        {
            Open();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MissionScene, true) };
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Instale Android Build Support para esta versão do editor.");
            Directory.CreateDirectory("Builds/Quest");
            var report = BuildPipeline.BuildPlayer(new[] { MissionScene }, "Builds/Quest/MissaoEstagio.apk", BuildTarget.Android, BuildOptions.Development);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Falha na build da missão: " + report.summary.result);
        }
    }
}
