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
                var pointer=mouse.position.ReadValue();
                if (!mission.day.Reading && mouse.leftButton.wasPressedThisFrame && !MenuRect.Contains(new Vector2(pointer.x,Screen.height-pointer.y))) Cursor.lockState = CursorLockMode.Locked;
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
                    var aimPoint=head.position+head.forward*12;
                    if(Physics.Raycast(head.position,head.forward,out var aimHit,35f,~0,QueryTriggerInteraction.Ignore))aimPoint=aimHit.point;
                    var gripPosition=head.position+head.forward*Mathf.Min(reach,.82f)+head.right*.24f-head.up*.22f;
                    heldBody.position=gripPosition;
                    heldBody.rotation=Quaternion.LookRotation(aimPoint-gripPosition,head.up);
                    if (secondary) held.activated.Invoke(null);
                }
                if (primary || (secondary && held.name != "Arco arcade"))
                    Release(secondary || held.name == "Bola de boliche");
                return;
            }
            if (!primary || !Physics.Raycast(head.position, head.forward, out var hit, 2.8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            var item = hit.collider.GetComponentInParent<XRGrabInteractable>();
            if (item && !item.isActiveAndEnabled) return;
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
            var marker=mission.day.Destination(held.gameObject);
            if(!throwItem && marker){
                var goal=marker.transform.position+Vector3.up*(held.name=="Copo de café"?.11f:held.transform.localScale.y*.5f);
                var delta=goal-head.position;
                if(delta.magnitude<2.6f && Vector3.Angle(delta,head.forward)<15){
                    bool clear=true;
                    foreach(var hit in Physics.RaycastAll(head.position,delta,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
                        if(hit.collider.name.Contains("Parede") || hit.collider.name.StartsWith("Porta •"))clear=false;
                    if(clear)heldBody.position=goal;
                }
            }
            heldBody.detectCollisions = true;
            heldBody.isKinematic = previousKinematic;
            if (!previousKinematic) heldBody.linearVelocity = throwItem ? head.forward * 8 : Vector3.zero;
            if (throwItem && held.name == "Bola de boliche")
            {
                var roll = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
                var origin=heldBody.position;
                if(roll.sqrMagnitude<.01f)roll=body.transform.forward;
                heldBody.linearVelocity=roll*8f+Vector3.up*1.1f;
                if(origin.z>=-6f && origin.z< -3.35f && Mathf.Abs(origin.x-45)<1 && roll.z>.3f)
                    origin.z=-3.35f;
                if (Physics.Raycast(origin+Vector3.up*.2f,Vector3.down,out var floor,3f,~0,QueryTriggerInteraction.Ignore)
                    && floor.collider.name=="Pista de boliche")
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

        Rect MenuRect {
            get {
                float width=Mathf.Min(430,Screen.width-20);
                return new Rect((Screen.width-width)/2,(Screen.height-210)/2,width,210);
            }
        }

        void Menu()
        {
            var rect=MenuRect;
            var saved=GUI.color;
            GUI.color=new Color(.045f,.12f,.16f,.98f);
            GUI.DrawTexture(rect,Texture2D.whiteTexture);
            GUI.color=new Color(.24f,.85f,.72f);
            GUI.DrawTexture(new Rect(rect.x,rect.y,rect.width,4),Texture2D.whiteTexture);
            GUI.color=Color.white;
            GUILayout.BeginArea(new Rect(rect.x+20,rect.y+16,rect.width-40,rect.height-26));
            bool reading=mission.day.Reading;
            var title=new GUIStyle(GUI.skin.label){fontSize=21,fontStyle=FontStyle.Bold};
            var copy=new GUIStyle(GUI.skin.label){fontSize=15,wordWrap=true};
            GUILayout.Label(reading?"Mural do Conhecimento":"Seu expediente",title);
            GUILayout.Space(5);
            GUILayout.Label(reading?"No Sebrae Lab você pode experimentar ideias, trocar experiências e aprender em grupo.":"Volte à recepção a qualquer momento. Seu progresso fica salvo nesta partida.",copy);
            GUILayout.Space(10);
            GUI.backgroundColor=new Color(.30f,.85f,.72f);
            if(GUILayout.Button(reading?"Entendido":"Continuar o expediente",GUILayout.Height(34))){
                if(reading)mission.day.ConfirmReading();
                Cursor.lockState=CursorLockMode.Locked;
            }
            GUI.backgroundColor=Color.white;
            if(!reading && GUILayout.Button("Voltar ao início  •  Recepção",GUILayout.Height(32))){
                if(held)Release(false);
                mission.day.ReturnToStart();Cursor.lockState=CursorLockMode.Locked;
            }
            GUILayout.EndArea();GUI.color=saved;
        }
        void OnGUI()
        {
            if(mission && mission.day && Cursor.lockState!=CursorLockMode.Locked)Menu();
            if(mission && mission.day){
                float width=Mathf.Min(460,Screen.width-16);
                GUI.Box(new Rect(8,59,width,50),mission.day.Objective());
            }
            GUI.Box(new Rect(8, 8, Mathf.Min(440, Screen.width-16), 48),
                Cursor.lockState != CursorLockMode.Locked ? "Clique para jogar com mouse e teclado" :
                "WASD andar   •   Mouse olhar   •   F conversar / abrir porta\nClique pegar / soltar / usar   •   Direito lançar / atirar   •   Esc cursor");
            if (Cursor.lockState==CursorLockMode.Locked && mission && mission.day && head && Physics.Raycast(head.position,head.forward,out var station,3.2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) && mission.day.IsStation(station.collider.gameObject))
                GUI.Box(new Rect((Screen.width-330)/2,Screen.height*.59f,330,32),"[F]   "+station.collider.name);
            if(held && (held.name=="Bola de boliche" || held.name=="Arco arcade"))
                GUI.Box(new Rect((Screen.width-330)/2,Screen.height-100,330,28),held.name=="Bola de boliche"?"Mire nos pinos e clique para arremessar":"Botão direito: disparar flecha");
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
