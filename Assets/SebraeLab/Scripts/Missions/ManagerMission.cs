using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SebraeLab.Missions
{
    public enum ManagerMissionState { FindManager, Dialogue, FindRoom, Completed }

    public class ManagerMission : MonoBehaviour
    {
        [Header("Referências da missão")]
        public Transform playerHead;
        public Transform playerRoot;
        public Transform manager;
        public MissionRoomZone room;
        public float conversationDistance = 2.2f;
        [Header("Interface no mundo")]
        public Text objectiveText;
        public Text dialogueText;
        public Text actionText;
        public Text proximityText;
        public Button conversationButton;
        public GameObject routeMarkers;
        public GameObject roomMarker;
        public GameObject completionPanel;
        public UnityEvent onCompleted = new UnityEvent();
        [TextArea(3, 6)]
        public string[] dialogue = {
            "Olá! Bem-vindo ao Sebrae Lab. Sou o gestor do espaço. Vamos começar seu primeiro dia?",
            "Aqui você encontra espaços de trabalho, troca de ideias e aprendizado. Seu primeiro desafio é conhecer sua sala.",
            "Sua sala está sinalizada ao fundo, à esquerda do corredor. Siga as marcações azuis e entre na área indicada."
        };

        public ManagerMissionState State { get; private set; }
        public int DialogueIndex { get; private set; }
        public int CompletionCount { get; private set; }
        float nextCheck;

        public InternshipDay day { get; private set; }
        void Start() {
#if UNITY_EDITOR || UNITY_STANDALONE
            gameObject.AddComponent<DesktopPlayer>().Initialize(this);
#endif
            // Retorno de hover e clique no botão do painel. Configurado na cena pelo Inspector, é respeitado.
            if (conversationButton && !conversationButton.GetComponent<MissionButton>())
                conversationButton.gameObject.AddComponent<MissionButton>();
            day =gameObject.AddComponent<InternshipDay>(); day.Initialize(this); dialogue[dialogue.Length-1]="Seu escritório fica na galeria ao lado da praça. Siga as marcas até a porta da Sala 2. A sala de aula fica logo depois do escritório."; RestartMission(); }
        void Update()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (UnityEngine.InputSystem.Keyboard.current?.fKey.wasPressedThisFrame == true && !(TryGetComponent<DesktopPlayer>(out var pc) && pc.UseAimedStation()))
                Interact();
#endif
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + .1f;
            EvaluatePlayer();
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        void OnGUI()
        {
            if (!playerHead || !playerHead.TryGetComponent<Camera>(out var camera) || !camera.enabled) return;
            if (State == ManagerMissionState.FindRoom || State == ManagerMissionState.Completed)
            {
                var hint=day.RoomHint();
                if(hint!=null) GUI.Box(new Rect((Screen.width-340)/2,Screen.height-58,340,36),hint);
                return;
            }
            float width = Mathf.Min(Screen.width - 20, 540);
            var box = new Rect((Screen.width - width) / 2, Screen.height - 112, width, 100);
            GUILayout.BeginArea(box, GUI.skin.box);
            if (!CanConverse())
                GUILayout.Label("Primeiro dia • Aproxime-se do gerente com W, A, S e D.");
            else
            {
                GUILayout.Label(State == ManagerMissionState.Dialogue ? dialogue[DialogueIndex] : "Gestor do espaço");
                if (GUILayout.Button("[F]  " + actionText.text, GUILayout.Height(32)))
                    Interact();
            }
            GUILayout.EndArea();
        }
#endif
        public bool CanConverse()
        {
            if (!playerHead || !manager) return false;
            var target = manager.position + Vector3.up * 1.35f;
            var delta = playerHead.position - target;
            if (new Vector2(delta.x, delta.z).sqrMagnitude > conversationDistance * conversationDistance || Mathf.Abs(delta.y) > 1.5f) return false;
            foreach (var hit in Physics.RaycastAll(playerHead.position, target - playerHead.position, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(manager) || hit.transform == manager) continue;
                if (playerRoot && (hit.transform.IsChildOf(playerRoot) || hit.transform == playerRoot)) continue;
                return false;
            }
            return true;
        }

        public bool TryBeginConversation()
        {
            if (State != ManagerMissionState.FindManager || !CanConverse()) return false;
            State = ManagerMissionState.Dialogue;
            DialogueIndex = 0;
            RefreshUI();
            Debug.Log("SEBRAE_MISSION_DIALOGUE_STARTED");
            return true;
        }

        public void Interact()
        {
            if (State == ManagerMissionState.FindManager) TryBeginConversation();
            else if (State == ManagerMissionState.Dialogue) TryAdvanceDialogue();
            else if(day) day.UseRoom();
        }

        public bool TryAdvanceDialogue()
        {
            if (State != ManagerMissionState.Dialogue || !CanConverse()) return false;
            if (DialogueIndex < dialogue.Length - 1) DialogueIndex++;
            else
            {
                State = ManagerMissionState.FindRoom;
                Debug.Log("SEBRAE_MISSION_ROOM_ACCEPTED");
            }
            RefreshUI();
            return true;
        }

        public void EvaluatePlayer()
        {
            if (State == ManagerMissionState.FindRoom && playerHead && room && day && day.DoorOpen && room.Contains(playerHead.position))
            {
                State = ManagerMissionState.Completed;
                CompletionCount++;
                RefreshUI();
                onCompleted.Invoke();
                Debug.Log("SEBRAE_MISSION_COMPLETED");
            }
            if (conversationButton)
                conversationButton.interactable = (State == ManagerMissionState.FindManager || State == ManagerMissionState.Dialogue) && CanConverse();
            if (proximityText)
                proximityText.text = State == ManagerMissionState.FindManager || State == ManagerMissionState.Dialogue
                    ? (CanConverse() ? "Aponte para o botão e aperte o gatilho." : "Aproxime-se do gerente para conversar.")
                    : "";
        }

        public void RestartMission()
        {
            State = ManagerMissionState.FindManager;
            DialogueIndex = 0;
            CompletionCount = 0;
            RefreshUI();
            EvaluatePlayer();
        }

        void RefreshUI()
        {
            if (routeMarkers) routeMarkers.SetActive(State == ManagerMissionState.FindRoom);
            if (roomMarker) roomMarker.SetActive(State == ManagerMissionState.FindRoom || State == ManagerMissionState.Completed);
            if (completionPanel) completionPanel.SetActive(false);
            if (conversationButton) conversationButton.gameObject.SetActive(State == ManagerMissionState.FindManager || State == ManagerMissionState.Dialogue);
            switch (State)
            {
                case ManagerMissionState.FindManager:
                    if (objectiveText) objectiveText.text = "MISSÃO 01 | BUSQUE O GERENTE";
                    if (dialogueText) dialogueText.text = "Bem-vindo ao seu primeiro dia! Encontre o gestor e converse com ele para receber sua missão.";
                    if (actionText) actionText.text = "CONVERSAR";
                    break;
                case ManagerMissionState.Dialogue:
                    if (objectiveText) objectiveText.text = "CONVERSA COM O GESTOR";
                    if (dialogueText) dialogueText.text = dialogue[DialogueIndex];
                    if (actionText) actionText.text = DialogueIndex == dialogue.Length - 1 ? "ACEITAR MISSÃO" : "CONTINUAR";
                    break;
                case ManagerMissionState.FindRoom:
                    if (objectiveText) objectiveText.text = "OBJETIVO | ENCONTRE SUA SALA";
                    if (dialogueText) dialogueText.text = "Siga as marcações azuis pelo corredor. Segure a maçaneta amarela, abra a porta e entre na sala.";
                    break;
                case ManagerMissionState.Completed:
                    if (objectiveText) objectiveText.text = "MISSÃO CONCLUÍDA";
                    if (dialogueText) dialogueText.text = "Atravesse o escritório e siga a placa Sala de aula até o mural.";
                    break;
            }
        }
    }
}
