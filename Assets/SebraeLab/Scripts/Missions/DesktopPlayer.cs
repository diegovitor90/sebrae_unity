#if UNITY_EDITOR || UNITY_STANDALONE
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SebraeLab.Missions
{
    public class DesktopPlayer : MonoBehaviour
    {
        ManagerMission mission;
        CharacterController body;
        Transform head;
        XRGrabInteractable held;
        Rigidbody heldBody;
        bool previousKinematic;
        float pitch, verticalSpeed;

        public void Initialize(ManagerMission owner)
        {
            mission = owner;
            var oldRig = owner.playerRoot;
            var start = oldRig.position;
            var facing = owner.playerHead.eulerAngles.y;
            oldRig.gameObject.SetActive(false);
            var simulator = GameObject.Find("Simulador PC - excluido da build");
            if (simulator) simulator.SetActive(false);
            var rig = new GameObject("Jogador PC");
            rig.transform.SetPositionAndRotation(start, Quaternion.Euler(0, facing, 0));
            body = rig.AddComponent<CharacterController>();
            body.height = 1.72f;
            body.center = Vector3.up * .86f;
            body.radius = .22f;
            body.stepOffset = .22f;
            head = new GameObject("Camera PC").transform;
            head.SetParent(rig.transform, false);
            head.localPosition = Vector3.up * 1.62f;
            var camera = head.gameObject.AddComponent<Camera>();
            camera.nearClipPlane = .05f;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.tag = "MainCamera";
            head.gameObject.AddComponent<AudioListener>();
            owner.playerRoot = rig.transform;
            owner.playerHead = head;
        }

        void Update()
        {
            var mouse = Mouse.current;
            var keys = Keyboard.current;
            if (mouse == null || keys == null || !body) return;
            if (keys.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (mouse.leftButton.wasPressedThisFrame) Cursor.lockState = CursorLockMode.Locked;
                return;
            }
            var look = mouse.delta.ReadValue() * .12f;
            body.transform.Rotate(0, look.x, 0);
            pitch = Mathf.Clamp(pitch - look.y, -85, 85);
            head.localRotation = Quaternion.Euler(pitch, 0, 0);
            var movement = new Vector3(keys.dKey.ReadValue()-keys.aKey.ReadValue(), 0, keys.wKey.ReadValue()-keys.sKey.ReadValue());
            verticalSpeed = body.isGrounded && verticalSpeed < 0 ? -2 : verticalSpeed + Physics.gravity.y * Time.deltaTime;
            body.Move((body.transform.TransformDirection(Vector3.ClampMagnitude(movement, 1))*2.4f + Vector3.up*verticalSpeed)*Time.deltaTime);

            UsePointer(mouse.leftButton.wasPressedThisFrame, mouse.rightButton.wasPressedThisFrame);
        }

        public void UsePointer(bool primary, bool secondary)
        {
            if (primary && UseAimedStation()) return;
            if (held)
            {
                float reach = 1.35f;
                if (Physics.Raycast(head.position, head.forward, out var obstacle, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    reach = Mathf.Max(.3f, obstacle.distance - .18f);
                heldBody.position = head.position + head.forward * reach;
                heldBody.rotation = Quaternion.Euler(0, head.eulerAngles.y, 0);
                if (held.name == "Arco arcade")
                {
                    heldBody.rotation = head.rotation;
                    if (secondary) held.activated.Invoke(null);
                }
                if (primary || (secondary && held.name != "Arco arcade"))
                    Release(secondary);
                return;
            }
            if (!primary || !Physics.Raycast(head.position, head.forward, out var hit, 2.8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            var item = hit.collider.GetComponentInParent<XRGrabInteractable>();
            if (!item)
            {
                if (!mission.day.UseStation(hit.collider.gameObject)) mission.Interact();
                return;
            }
            held = item;
            heldBody = item.GetComponent<Rigidbody>();
            previousKinematic = heldBody.isKinematic;
            heldBody.isKinematic = true;
            heldBody.detectCollisions = false;
            held.selectEntered.Invoke(null);
        }

        public bool UseAimedStation()
        {
            if (!Physics.Raycast(head.position,head.forward,out var hit,3.2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                || !mission.day.IsStation(hit.collider.gameObject)) return false;
            if (held) Release(false);
            return mission.day.UseStation(hit.collider.gameObject);
        }

        void Release(bool throwItem)
        {
            if (!throwItem && (held.name=="Livro" || held.name=="Cadeira" || held.name=="Notebook"))
            {
                foreach(var marker in GameObject.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    if(marker.name!="Destino • "+held.name) continue;
                    var goal=marker.transform.position+Vector3.up*held.transform.localScale.y*.5f;
                    var toGoal=goal-head.position;
                    if(toGoal.magnitude>2.2f || Vector3.Angle(toGoal,head.forward)>10) continue;
                    if(Physics.Raycast(head.position,toGoal,out var obstruction,toGoal.magnitude,~0,QueryTriggerInteraction.Ignore)
                        && Vector3.Distance(obstruction.point,goal)>.35f) continue;
                    heldBody.position=goal;
                    break;
                }
            }
            heldBody.detectCollisions = true;
            heldBody.isKinematic = previousKinematic;
            if (!previousKinematic) heldBody.linearVelocity = throwItem ? head.forward * 8 : Vector3.zero;
            if (throwItem && held.name == "Bola de boliche")
            {
                var roll = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
                if (Physics.Raycast(heldBody.position, Vector3.down, out var floor, 2.5f, ~0, QueryTriggerInteraction.Ignore)
                    && floor.collider.name == "Pista de boliche")
                {
                    float radius = held.GetComponent<SphereCollider>().radius * held.transform.lossyScale.x;
                    heldBody.position = floor.point + Vector3.up * (radius + .01f);
                    heldBody.linearVelocity = roll * 8f;
                    heldBody.maxAngularVelocity = 60f;
                    heldBody.angularVelocity = Vector3.Cross(Vector3.up, heldBody.linearVelocity) / radius;
                }
            }
            held.selectExited.Invoke(null);
            held = null;
        }

        void OnGUI()
        {
            GUI.Box(new Rect(8, 8, Mathf.Min(440, Screen.width-16), 48),
                Cursor.lockState != CursorLockMode.Locked ? "Clique para jogar com mouse e teclado" :
                "WASD andar   •   Mouse olhar   •   F conversar / abrir porta\nClique pegar / soltar / usar   •   Direito lançar / atirar   •   Esc cursor");
            if (Cursor.lockState == CursorLockMode.Locked)
                GUI.Label(new Rect(Screen.width/2-4, Screen.height/2-10, 18, 22), "+");
        }

        void OnDestroy()
        {
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
#endif
