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

        void Start() { RestartMission(); }
        void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + .1f;
            EvaluatePlayer();
        }

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
            if (State == ManagerMissionState.FindRoom && playerHead && room && room.Contains(playerHead.position))
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
            if (completionPanel) completionPanel.SetActive(State == ManagerMissionState.Completed);
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
                    if (dialogueText) dialogueText.text = "Siga as marcações azuis pelo corredor. Entre na sala sinalizada para concluir sua primeira missão.";
                    break;
                case ManagerMissionState.Completed:
                    if (objectiveText) objectiveText.text = "MISSÃO CONCLUÍDA";
                    if (dialogueText) dialogueText.text = "Você encontrou sua sala. Bem-vindo ao Sebrae Lab!";
                    break;
            }
        }
    }
}
