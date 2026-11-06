using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SebraeLab.Missions
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class ManagerNpcInteraction : MonoBehaviour
    {
        public ManagerMission mission;
        XRSimpleInteractable interactable;
        void OnEnable()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(OnSelected);
        }
        void OnDisable()
        {
            if (interactable) interactable.selectEntered.RemoveListener(OnSelected);
        }
        void OnSelected(SelectEnterEventArgs args)
        {
            if (mission) mission.TryBeginConversation();
        }
    }
}
