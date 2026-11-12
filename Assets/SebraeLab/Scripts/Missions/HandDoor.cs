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
