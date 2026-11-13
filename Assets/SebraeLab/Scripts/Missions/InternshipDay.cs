using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace SebraeLab.Missions
{
    public class InternshipDay : MonoBehaviour
    {
        ManagerMission mission;
        Transform hinge;
        float doorProgress;
        bool opening;
        public bool DoorOpen => hinge && Quaternion.Angle(Quaternion.identity, hinge.localRotation) > 45;
        readonly Color navy = new Color(.04f,.13f,.22f), teal = new Color(.04f,.58f,.59f), gold = new Color(1,.68f,.14f);
        readonly MissionSurfaces surfaces = new MissionSurfaces();
        readonly List<Action> ticks = new List<Action>();
        readonly List<Action> pinResets = new List<Action>();
        readonly List<Action> pending = new List<Action>();
        Transform area;
        TextMesh status, mural;
        GameObject confirm, entry, lounge;
        GameObject loungeGate, coffeeSpot;
        XRGrabInteractable coffee;
        bool coffeeCollected;
        readonly Dictionary<GameObject,GameObject> assigned=new Dictionary<GameObject,GameObject>();
        public GameObject Destination(GameObject item) => assigned.TryGetValue(item,out var spot) && spot.activeInHierarchy ? spot : null;
        public bool Reading => confirm && confirm.activeSelf;
        public void ConfirmReading(){if(Reading)UseStation(confirm);}
        public void ReturnToStart(){Move(new Vector3(5.7f,0,7.1f));}
        int placed, score;
        const int ItemCount = 6;
        // Etapa atual, objetivo e avisos. Aparece igual na placa de status (Quest) e no HUD do PC.
        MissionManager etapas;
        public MissionManager Etapas => etapas;
        public void Initialize(ManagerMission manager)
        {
            mission = manager;
            if (!TryGetComponent(out etapas)) etapas = gameObject.AddComponent<MissionManager>();
            area = new GameObject("Missão Estágio • percurso").transform;
            Sign("PRIMEIRO DIA\nProcure o gestor do espaço",new Vector3(3.9f,1.9f,7.4f),.067f);
            Music();
            hinge = new GameObject("Dobradiça da sala").transform;
            var office = new GameObject("Sala do estagiário").transform;
            office.SetParent(area,false);
            office.SetPositionAndRotation(new Vector3(16,0,0), Quaternion.Euler(0,-90,0));
            mission.room.transform.position = new Vector3(17,0,0);
            mission.roomMarker.transform.position += mission.room.transform.position - new Vector3(-3,0,-5);
            foreach(Transform mark in mission.routeMarkers.transform) mark.gameObject.SetActive(false);
            foreach(var waypoint in new[]{new Vector3(5.7f,0,8),new Vector3(6,0,10),new Vector3(9,0,11),new Vector3(12,0,11),new Vector3(14,0,10),new Vector3(14,0,7),new Vector3(14,0,4),new Vector3(14,0,1),new Vector3(15,0,0)})
                Shape("Marca de percurso",waypoint+Vector3.up*.015f,new Vector3(.30f,.018f,.30f),teal,parent:mission.routeMarkers.transform);
            foreach(var panel in FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
                if(panel.name=="Porta Entrada") panel.transform.position+=Vector3.left*3.2f;
            PassageFloor("Galeria da praça",new Vector3(14,-.11f,2.5f),new Vector3(4,.20f,9));
            PassageFloor("Piso do escritório",new Vector3(18,-.1f,0),new Vector3(4,.2f,5.2f));
            PassageFloor("Galeria de descompressão",new Vector3(36.5f,-.1f,0),new Vector3(9,.2f,3.2f));
            foreach(int side in new[]{-1,1})
                Shape("Parede lateral",new Vector3(36.5f,1.45f,side*1.60f),new Vector3(9,2.9f,.12f),teal);
            Sign("ESCRITÓRIO   •   SALA 2",new Vector3(15.9f,2.38f,0),.09f);
            Sign("SALA 2   →   ALA DE AULAS",new Vector3(13.8f,1.9f,9),.09f);
            BuildOffice(office);
            hinge.SetParent(office,false);
            hinge.localPosition = Vector3.left * .56f;
            foreach(float x in new[]{-.055f,1.17f})
                Shape("Batente",hinge.localPosition+new Vector3(x,1.09f,0),new Vector3(.10f,2.18f,.15f),navy,parent:office);
            Shape("Batente",hinge.localPosition+new Vector3(.56f,2.14f,0),new Vector3(1.32f,.11f,.15f),navy,parent:office);
            var door = Shape("Porta • sala do estagiário", new Vector3(.56f,1.04f,0),new Vector3(1.12f,2.08f,.07f),teal,parent:hinge);
            var knob = Shape("Maçaneta",new Vector3(.99f,1,.12f),new Vector3(.22f,.09f,.17f),gold,parent:hinge);
            var insideHandle=Shape("Maçaneta interna",new Vector3(0,0,-1.42f),Vector3.one,gold,parent:knob.transform);
            var grip = knob.AddComponent<XRSimpleInteractable>();
            grip.selectEntered.AddListener(e => ToggleDoor());
            desktopActions[knob]=ToggleDoor;
            desktopActions[insideHandle]=ToggleDoor;
            entry = Station("ENTRAR NA ALA DE ATIVIDADES",office.TransformPoint(new Vector3(1.85f,1.15f,-3.65f)),()=>Move(new Vector3(24,0,0)));
            entry.SetActive(false);
            manager.onCompleted.AddListener(()=>{ entry.SetActive(true); etapas.Concluir(EtapaExpediente.Sala); });
            gameObject.AddComponent<LabPlaza>().Build();
            AddLitRoof(new Vector3(18,2.78f,0),new Vector3(4,.12f,5.2f));
            AddLitRoof(new Vector3(26,3.23f,0),new Vector3(12,.12f,10));
            AddLitRoof(new Vector3(36.5f,3,0),new Vector3(9,.12f,3.2f));
            BuildWing();
            BuildLounge();
            loungeGate=Shape("Porta da descompressão",new Vector3(41,1.2f,0),new Vector3(.12f,2.4f,2.4f),teal);
            var gateSign=Sign("DESCOMPRESSÃO   •   ABRE AO CONCLUIR O EXPEDIENTE",new Vector3(40.8f,2.6f,0),.067f);
            ticks.Add(()=>{if(etapas.Em(EtapaExpediente.Descompressao))gateSign.text="DESCOMPRESSÃO   •   ENTRADA LIBERADA";});
            Station("VOLTAR À RECEPÇÃO",new Vector3(23,1,-4),()=>Move(new Vector3(5.7f,0,7.1f)));
            Station("RECOMEÇAR O DIA",new Vector3(50,1,-6),()=>UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.name));
        }
        void BuildOffice(Transform office)
        {
            foreach (int side in new[]{-1,1})
            {
                Shape("Parede lateral",new Vector3(side*1.65f,1.32f,0),new Vector3(1.9f,2.64f,.12f),teal,parent:office);
                Shape("Parede lateral",new Vector3(side*2.60f,1.32f,-2),new Vector3(.12f,2.64f,4),teal,parent:office);
            }
            Shape("Parede lateral",new Vector3(0,2.45f,0),new Vector3(1.4f,.38f,.12f),teal,parent:office);
            Shape("Mesa do escritório",new Vector3(-1.85f,.44f,-2.45f),new Vector3(1.25f,.88f,.7f),gold,parent:office);
            var chair=Shape("Cadeira do escritório",new Vector3(-1.8f,.25f,-1.62f),new Vector3(.48f,.5f,.48f),teal,parent:office);
            Shape("Encosto",new Vector3(0,.72f,.43f),new Vector3(1,.85f,.13f),navy,parent:chair.transform);
            Shape("Quadro Redes",new Vector3(-2.49f,1.65f,-2.45f),new Vector3(.04f,.8f,.95f),navy,parent:office);
            Sign("SALA DE AULA   •   SIGA EM FRENTE",office.TransformPoint(new Vector3(0,2.25f,-3.9f)),.077f);
            foreach(float z in new[]{-2.02f,-.82f})
                Shape("Prancha de carvalho",new Vector3(1.31f,.61f,z),new Vector3(.44f,1.22f,.07f),gold,parent:office);
            foreach(float height in new[]{.70f,1.16f})
            {
                Shape("Prancha de carvalho",new Vector3(1.31f,height,-1.42f),new Vector3(.42f,.05f,1.18f),gold,parent:office);
                for(int i=0;i<6;i++)
                {
                    int id=(height<1?24:30)+i;
                    var volume=Shape("Volume "+id,new Vector3(1.31f,height+.16f,-1.84f+i*.078f),new Vector3(.26f,.062f,.21f),gold,parent:office);
                    volume.transform.localRotation=Quaternion.Euler(0,90,90);
                    Grab(volume).GetComponent<Rigidbody>().mass=.31f;
                }
            }
        }

        bool Near(Vector3 target)
        {
            var delta=mission.playerHead.position-target;
            return Mathf.Abs(delta.y)<1.6f && new Vector2(delta.x,delta.z).sqrMagnitude<2.25f;
        }
        public string RoomHint()
        {
            if (Near(hinge.TransformPoint(new Vector3(.99f,1,0)))) return opening?"[F] Fechar a porta":"[F] Abrir a porta";
            if (mission.State==ManagerMissionState.FindRoom)
                return DoorOpen?"Atravesse a porta e entre na sala.":Near(hinge.TransformPoint(new Vector3(.99f,1,.12f)))?"[F] Abrir a porta":"Siga as marcas azuis até a porta da sala.";
            return entry.activeSelf && Near(entry.transform.position)?"[F] Entrar na ala de atividades":null;
        }
        public void UseRoom()
        {
            if(Near(hinge.TransformPoint(new Vector3(.99f,1,0)))) ToggleDoor();
            else if(entry.activeSelf && Near(entry.transform.position)) Move(new Vector3(24,0,0));
        }
        void ToggleDoor() { opening = !opening; }
        void Update()
        {
            float target=opening?1f:0f;
            if(mission.playerRoot.position.y < -2f) Move(new Vector3(5.7f,.05f,7.1f));
            if (doorProgress!=target){
                doorProgress=Mathf.MoveTowards(doorProgress,target,Time.deltaTime/.65f);
                hinge.localRotation=Quaternion.Euler(0,Mathf.SmoothStep(0,95,doorProgress),0);
            }
            foreach(var action in ticks) action();
            foreach(var action in pending) action();
            pending.Clear();
        }
        void Move(Vector3 p)
        {
            var rig=mission.playerRoot; var head=mission.playerHead;
            var cc=rig.GetComponent<CharacterController>(); bool enabled=cc && cc.enabled;
            if(enabled)cc.enabled=false;
            rig.position+=new Vector3(p.x-head.position.x,p.y-rig.position.y,p.z-head.position.z);
            if(enabled)cc.enabled=true;
        }
        GameObject Shape(string name,Vector3 pos,Vector3 size,Color color,PrimitiveType type=PrimitiveType.Cube,Transform parent=null)
        {
            var g=GameObject.CreatePrimitive(type); g.name=name; g.transform.SetParent(parent?parent:area,false);
            g.transform.localPosition=pos; g.transform.localScale=size;
            surfaces.Apply(g,name);
            return g;
        }
        TextMesh Sign(string value,Vector3 pos,float size)
        {
            var g=new GameObject("Placa");g.transform.SetParent(area);g.transform.position=pos;
            var t=g.AddComponent<TextMesh>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            g.GetComponent<MeshRenderer>().sharedMaterial = t.font.material;
            t.fontSize=72;t.characterSize=size*.1f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.text=value;
            ticks.Add(()=>{var d=t.transform.position-mission.playerHead.position;d.y=0;if(d.sqrMagnitude>.001f)t.transform.rotation=Quaternion.LookRotation(d);});return t;
        }
        // Objetos clicáveis no modo PC que não são botões de missão (maçanetas).
        readonly Dictionary<GameObject, Action> desktopActions = new Dictionary<GameObject, Action>();
        // Botões de missão, pela raiz (que tem o colisor da mira do PC) e pelo painel do mural.
        readonly Dictionary<GameObject, MissionButton> stations = new Dictionary<GameObject, MissionButton>();
        public bool IsStation(GameObject g) => g && (stations.ContainsKey(g) || desktopActions.ContainsKey(g));
        public bool UseStation(GameObject station)
        {
            if (!station) return false;
            // O botão decide: bloqueado mostra o motivo; liberado põe a ação na fila.
            if (stations.TryGetValue(station, out var button)) { button.Pressionar(); return true; }
            if (!desktopActions.TryGetValue(station, out var action)) return false;
            pending.Add(action);
            return true;
        }
        /// <summary>
        /// Botão de missão em Canvas World Space. No Quest responde ao raio com o gatilho; no PC, à mira
        /// com clique ou F. Com etapa, fica cinza antes dela e explica o motivo se apertado.
        /// </summary>
        GameObject Station(string caption,Vector3 p,Action action,EtapaExpediente? etapa=null)
        {
            // A ação entra na fila e roda no Update: evita mexer na cena dentro do evento de UI.
            var button=MissionButton.Criar(caption,area,p+Vector3.up*.25f,new Vector2(1.1f,.3f),()=>pending.Add(action),etapas,etapa);
            var root=button.transform.parent.gameObject;
            ticks.Add(()=>{
                var direction=root.transform.position-mission.playerHead.position;direction.y=0;
                if(direction.sqrMagnitude>.001f)root.transform.rotation=Quaternion.LookRotation(direction);
            });
            stations[root]=button;
            return root;
        }
        void PassageFloor(string name, Vector3 position, Vector3 size)
        {
            var floor=Shape(name,position,size,navy);
            floor.AddComponent<TeleportationArea>().interactionLayers=(UnityEngine.XR.Interaction.Toolkit.InteractionLayerMask)(1<<31);
        }
        void Room(string name,float x,float width,float depth)
        {
            var floor=Shape(name,new Vector3(x,-.1f,0),new Vector3(width,.2f,depth),new Color(.25f,.31f,.36f));
            if (name == "Sala de descompressão")
            {
                floor.SetActive(false);
                foreach(var slab in new[]{
                    new Rect(41,-8,2.52f,16),new Rect(46.48f,-8,8.52f,16),
                    new Rect(43.52f,-8,2.96f,4.5f),new Rect(43.52f,5.5f,2.96f,2.5f)})
                {
                    var tile=Shape(name,new Vector3(slab.center.x,-.1f,slab.center.y),new Vector3(slab.width,.2f,slab.height),navy);
                    tile.AddComponent<TeleportationArea>().interactionLayers=(UnityEngine.XR.Interaction.Toolkit.InteractionLayerMask)(1<<31);
                }
            }
            floor.AddComponent<TeleportationArea>().interactionLayers=(UnityEngine.XR.Interaction.Toolkit.InteractionLayerMask)(1<<31);
            foreach(int s in new[]{-1,1}){
                bool passage=name=="Ala de atividades" || s==-1;
                if(passage){
                    float segment=(depth-2.4f)*.5f;
                    foreach(int end in new[]{-1,1})
                        Shape("Parede lateral",new Vector3(x+s*width/2,1.6f,end*(1.2f+segment*.5f)),new Vector3(.14f,3.2f,segment),teal);
                }else Shape("Parede lateral",new Vector3(x+s*width/2,1.6f,0),new Vector3(.14f,3.2f,depth),teal);
                Shape("Parede de fundo",new Vector3(x,1.6f,s*depth/2),new Vector3(width,3.2f,.14f),navy);}
        }
        XRGrabInteractable Grab(GameObject g)
        {
            var rb=g.AddComponent<Rigidbody>();rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            var grab=g.AddComponent<XRGrabInteractable>();var start=g.transform.position; var rot=g.transform.rotation;
            ticks.Add(()=>{if(!grab.isSelected && g.transform.position.y < -1)ResetBody(rb,start,rot);});return grab;
        }
        void ResetBody(Rigidbody rb,Vector3 p,Quaternion r)
        {if(!rb.isKinematic){rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;}rb.position=p;rb.rotation=r;rb.Sleep();}
        void AddLitRoof(Vector3 center,Vector3 size)
        {
            Shape("Teto preto",center,size,navy);
            foreach(float offset in new[]{-.25f,.25f}){
                var panel=Shape("Luminária",center+new Vector3(offset*size.x,-.14f,0),new Vector3(.84f,.06f,.28f),Color.white);
                var lamp=panel.AddComponent<Light>();lamp.color=new Color(1,.94f,.84f);lamp.intensity=1.5f;lamp.range=7;
            }
        }
        void BuildWing()
        {
            Room("Ala de atividades",26,12,10);
            Sign("SALA DE AULA   •   BIBLIOTECA E INFORMÁTICA",new Vector3(28,2.35f,1),.07f);
            Sign("DESCOMPRESSÃO  →",new Vector3(31.9f,2.2f,0),.09f);
            status=Sign(etapas.Texto,new Vector3(26,2.6f,0),.105f);
            etapas.TextoMudou+=texto=>status.text=texto;
            mural=Sign("MURAL DO CONHECIMENTO",new Vector3(22,1.95f,2),.085f);
            // Liberado a partir do Mural: pode ser relido depois, como antes.
            var muralButton=Station("LER O MURAL",new Vector3(22,1,2),()=>{if(!etapas.Alcancou(EtapaExpediente.Mural))return;mural.text="SEBRAE LAB\nUm espaço para experimentar ideias,\ntrocar experiências e aprender em grupo.";confirm.SetActive(true);Cursor.lockState=CursorLockMode.None;},EtapaExpediente.Mural);
            var panelHit=mural.gameObject.AddComponent<BoxCollider>();
            panelHit.size=new Vector3(2.1f,.8f,.1f);
            stations[mural.gameObject]=stations[muralButton];
            confirm=Station("ENTENDIDO",new Vector3(23.8f,1,2),()=>{confirm.SetActive(false);Cursor.lockState=CursorLockMode.Locked;if(!etapas.Em(EtapaExpediente.Mural))return;etapas.Concluir(EtapaExpediente.Mural);coffee.enabled=true;coffeeSpot.SetActive(true);mural.text="MURAL DO CONHECIMENTO";});confirm.SetActive(false);
            Shape("Balcão da copa",new Vector3(26,.45f,3),new Vector3(2,.9f,.8f),teal);
            coffee=Grab(Shape("Copo de café",new Vector3(26,1.02f,3),new Vector3(.13f,.1f,.13f),gold,PrimitiveType.Cylinder));
            coffee.enabled=false;
            var saucer=new Vector3(18.45f,.9f,-1.85f);
            coffeeSpot=Shape("Pires • café",saucer,new Vector3(.32f,.012f,.32f),gold,PrimitiveType.Cylinder);
            coffeeSpot.GetComponent<Collider>().enabled=false;
            assigned[coffee.gameObject]=coffeeSpot;
            var coffeeLabel=Sign("CAFÉ   •   ENTREGUE NO PIRES",saucer+Vector3.up*.27f,.065f);
            coffeeLabel.transform.SetParent(coffeeSpot.transform,true);
            coffeeSpot.SetActive(false);
            coffee.selectEntered.AddListener(e=>{if(etapas.Em(EtapaExpediente.Cafe))coffeeCollected=true;});
            coffee.selectExited.AddListener(e=>{
                if(!etapas.Em(EtapaExpediente.Cafe) || Vector3.Distance(coffee.GetComponent<Rigidbody>().position,saucer+Vector3.up*.11f)>.35f)return;
                pending.Add(()=>{
                    if(!etapas.Em(EtapaExpediente.Cafe))return;
                    coffee.enabled=false;
                    var rb=coffee.GetComponent<Rigidbody>();rb.isKinematic=true;rb.position=saucer+Vector3.up*.11f;
                    coffeeLabel.gameObject.SetActive(false);
                    etapas.Concluir(EtapaExpediente.Cafe);ShowDeliveryTargets();
                });
            });
            Sign("COPA   •   RETIRE O CAFÉ",new Vector3(26,1.9f,3),.09f);
            Shape("Mesa de inovação",new Vector3(22,.45f,-2),new Vector3(2,.9f,1),teal);
            Grab(Shape("Cartilha de inovação",new Vector3(21.7f,.94f,-2),new Vector3(.25f,.04f,.32f),gold));
            Grab(Shape("Protótipo",new Vector3(22.4f,1.06f,-2),Vector3.one*.25f,teal,PrimitiveType.Sphere));
            Sign("MESA DE INOVAÇÃO",new Vector3(22,1.65f,-2),.07f);
            for(int i=0;i<2;i++){
                float x=27+i*1.6f;
                Shape("Mesa de coworking",new Vector3(x,.45f,-2),new Vector3(1.45f,.9f,1),navy);
                Shape("Prateleira",new Vector3(x,.55f,-4),new Vector3(1.45f,1.1f,.65f),teal);
                Shape("Mesa da sala de aula",new Vector3(x,.45f,1),new Vector3(1.45f,.9f,.8f),teal);
                var monitor=Shape("Tela",new Vector3(x,.0f+1.31f,1.27f),new Vector3(.53f,.32f,.035f),navy);
                Shape("Pé de aço",new Vector3(x,1.04f,1.27f),new Vector3(.07f,.24f,.09f),navy);
                for(int row=0;row<2;row++)
                    for(int j=0;j<6;j++){
                        int id=i*12+row*6+j;
                        var pos=new Vector3(x-.59f+j*.062f,.089f+row*.506f+.13f,-3.94f);
                        var volume=Shape("Volume "+id,pos,new Vector3(.25f,.042f+.003f*(j%3),.21f+.012f*(j%4)),gold);
                        volume.transform.rotation=Quaternion.Euler(0,180,90);
                        Grab(volume).GetComponent<Rigidbody>().mass=.31f;
                    }
                Delivery("Livro",new Vector3(x-.3f,.94f,-2),new Vector3(x,1.14f,-4),new Vector3(.25f,.08f,.32f),gold,0);
                Delivery("Cadeira",new Vector3(x+.55f,.26f,-.6f),new Vector3(x,.26f,.24f),new Vector3(.46f,.5f,.45f),teal,1);
                Delivery("Notebook",new Vector3(x+.3f,.94f,-2),new Vector3(x,.94f,1),new Vector3(.4f,.025f,.28f),Color.gray,2);}
            Sign("ESTANTE",new Vector3(28,1.7f,-4),.075f);Sign("SALA DE AULA",new Vector3(28,1.8f,1),.075f);
            lounge=Station("SALA DE DESCOMPRESSÃO",new Vector3(30,1,2),()=>Move(new Vector3(46,0,0)));lounge.SetActive(false);
        }
        readonly List<GameObject>[] destinations={new List<GameObject>(),new List<GameObject>(),new List<GameObject>()};
        string[] jobs={"Guarde os livros na estante","Alinhe as cadeiras nas marcas","Leve os notebooks às mesas"};
        void ShowDeliveryTargets(){
            for(int kind=0;kind<destinations.Length;kind++)
                foreach(var spot in destinations[kind]) spot.SetActive(etapas.Em(EtapaExpediente.Organizacao) && kind==placed/2);
        }
        /// <summary>Texto do HUD do modo PC. Avisos vêm do MissionManager, os mesmos da placa do Quest.</summary>
        public string Objective(){
            if(etapas.AvisoAtivo)return etapas.Texto;
            switch(mission.State){
                case ManagerMissionState.FindManager:return "1/5   •   Recepção\nAproxime-se do gerente e pressione F.";
                case ManagerMissionState.Dialogue:return "1/5   •   Conversa\nPressione F para continuar e aceitar o estágio.";
                case ManagerMissionState.FindRoom:return DoorOpen?"2/5   •   Sala 2\nAtravesse a porta para conhecer seu escritório.":"2/5   •   Sala 2\nSiga as marcas pela praça e clique na maçaneta.";
            }
            if(etapas.Em(EtapaExpediente.Mural))return confirm.activeSelf?"3/5   •   Mural\nLeia o painel e clique em Entendido.":"3/5   •   Mural\nAtravesse o escritório e clique em Ler o mural.";
            if(etapas.Em(EtapaExpediente.Cafe) && coffeeCollected)return "4/5   •   Entregue o café na Sala 2\nMire no pires da mesa do escritório e clique para soltar.";
            if(etapas.Em(EtapaExpediente.Cafe))return "4/5   •   Copa\nPegue o copo no balcão com o clique esquerdo.";
            if(etapas.Em(EtapaExpediente.Organizacao))return "5/5   •   "+jobs[placed/2]+"   "+(placed%2)+"/2\nPegue o objeto e solte na marca com o mesmo número.";
            return "Expediente concluído\nSiga o corredor até a sala de descompressão.";
        }
        void Delivery(string name,Vector3 p,Vector3 target,Vector3 size,Color color,int kind)
        {
            var g=Shape(name,p,size,color);
            if(kind==1)Shape("Encosto",new Vector3(0,.7f,-.4f),new Vector3(1,.8f,.16f),navy,parent:g.transform);
            if(kind==2)Shape("Tela",new Vector3(0,4.9f,-.46f),new Vector3(1,8.8f,.027f),navy,parent:g.transform);
            var marker=Shape("Destino • "+name,target-Vector3.up*(size.y/2-.005f),new Vector3(size.x+.08f,.012f,size.z+.08f),gold);marker.GetComponent<Collider>().enabled=false;
            int number=destinations[kind].Count+1;
            string itemLabel=name+" "+number;
            var tag=Sign(itemLabel,p+Vector3.up*(size.y*.5f+.18f),.063f);
            ticks.Add(()=>tag.transform.position=g.transform.position+Vector3.up*(size.y*.5f+.18f));
            var destinationLabel=Sign((kind==0?"ESTANTE":kind==1?"LUGAR":"MESA")+" "+number+"   •   "+itemLabel,target+Vector3.up*.22f,.065f);
            destinationLabel.transform.SetParent(marker.transform,true);
            assigned[g]=marker;
            destinations[kind].Add(marker);marker.SetActive(false);
            var grab=Grab(g);bool delivered=false;
            var deliveryBody = g.GetComponent<Rigidbody>();
            grab.selectExited.AddListener(e=>{
                var releasePosition = deliveryBody.position;
                Debug.Log($"Entrega {name}: {releasePosition} -> {target} (etapa {etapas.Atual})");
                pending.Add(()=>{
                if(delivered)return;
                bool near=Vector3.Distance(releasePosition,target)<.45f;
                // Fora da etapa ou da ordem: só avisa se o jogador tentou entregar de fato (soltou perto da marca).
                if(!etapas.Em(EtapaExpediente.Organizacao)){if(near)etapas.Exigir(EtapaExpediente.Organizacao);return;}
                if(kind!=placed/2){if(near)etapas.Avisar(jobs[placed/2]+" primeiro.");return;}
                var slot=near ? marker : null;
                if(!slot){etapas.Avisar("Destino de "+itemLabel+": "+destinationLabel.text+". Mire na marca e solte.");return;}
                delivered=true;tag.gameObject.SetActive(false);
                deliveryBody.isKinematic=true;
                grab.enabled=false;
                g.transform.SetPositionAndRotation(slot.transform.position+Vector3.up*(size.y/2),Quaternion.identity);
                destinations[kind].Remove(slot);slot.SetActive(false);
                placed++;
                if(placed==ItemCount){etapas.Concluir(EtapaExpediente.Organizacao);loungeGate.SetActive(false);lounge.SetActive(true);}
                else etapas.Progresso("Organização • "+placed+" de "+ItemCount+" itens");
                ShowDeliveryTargets();
            });});
        }
        void BuildLounge()
        {
            Room("Sala de descompressão",48,14,16);
            Sign("SALA DE DESCOMPRESSÃO",new Vector3(48,2.6f,0),.13f);
            Shape("Pista de boliche",new Vector3(45,.04f,1),new Vector3(2,.08f,9),gold);
            foreach(float side in new[]{-1f,1f})
            {
                float center=45+side*1.24f;
                Shape("Fundo da calha",new Vector3(center,-.19f,1),new Vector3(.49f,.08f,9),navy);
                foreach(float edge in new[]{-1f,1f}){
                    var lip=Shape("Lateral da calha",new Vector3(center+edge*.19f,-.075f,1),new Vector3(.09f,.28f,9),navy);
                    lip.transform.rotation=Quaternion.Euler(0,0,-edge*25);
                }
            }
            var ceiling=Shape("Teto preto",new Vector3(48,3.23f,0),new Vector3(14,.12f,16),navy);
            var tint=new MaterialPropertyBlock();tint.SetColor("_Color",new Color(.055f,.058f,.065f));ceiling.GetComponent<Renderer>().SetPropertyBlock(tint);
            var fixture=Resources.Load<GameObject>("LuminariaDescompressao");
            if(fixture) foreach(float x in new[]{44.7f,51f}) foreach(float z in new[]{-4f,0,4f}){
                var lamp=Instantiate(fixture,area);
                var meshes=lamp.GetComponentsInChildren<Renderer>();
                var bounds=meshes[0].bounds;foreach(var mesh in meshes)bounds.Encapsulate(mesh.bounds);
                lamp.transform.localScale*=.85f/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
                bounds=meshes[0].bounds;foreach(var mesh in meshes)bounds.Encapsulate(mesh.bounds);
                lamp.transform.position+=new Vector3(x,3.16f,z)-new Vector3(bounds.center.x,bounds.max.y,bounds.center.z);
                var light=new GameObject("Luz quente").AddComponent<Light>();light.transform.SetParent(lamp.transform,true);
                light.transform.position=new Vector3(x,2.9f,z);light.color=new Color(1,.89f,.73f);light.range=5;light.intensity=1.3f;
            }
            var board=Sign("BOLICHE   •   0 / 10",new Vector3(45,2.2f,5.4f),.12f);
            var pins=new List<Rigidbody>();
            int displayed=0;
            ticks.Add(()=>{
                int down=0;
                foreach(var pin in pins) if(Vector3.Angle(pin.rotation*Vector3.up,Vector3.up)>35) down++;
                if(down!=displayed){displayed=down;board.text="BOLICHE   •   "+down+" / 10";}
            });
            for(int r=0;r<4;r++)for(int c=0;c<=r;c++){
                var p=new Vector3(45+(c-r*.5f)*.27f,.32f,3+r*.27f);var pin=Shape("Pino",p,new Vector3(.16f,.24f,.16f),Color.white,PrimitiveType.Capsule);
                // A flat sole supports the upright pin between throws.
                pin.GetComponent<CapsuleCollider>().enabled=false;
                var foot=pin.AddComponent<BoxCollider>();
                foot.center=new Vector3(0,-.75f,0);foot.size=new Vector3(.69f,.5f,.69f);
                var belly=pin.AddComponent<SphereCollider>();belly.center=new Vector3(0,-.27f,0);belly.radius=.47f;
                var neck=pin.AddComponent<BoxCollider>();neck.center=new Vector3(0,.48f,0);neck.size=new Vector3(.40f,.98f,.40f);
                var rb=pin.AddComponent<Rigidbody>();rb.mass=.22f;pins.Add(rb);pinResets.Add(()=>ResetBody(rb,p,Quaternion.identity));}
            Sign("BOLICHE • lance a bola pela pista",new Vector3(45,1.9f,-3.5f),.085f);
            var ballPos=new Vector3(45,.85f,-3.8f);
            Shape("Suporte da bola",ballPos-Vector3.up*.48f,new Vector3(.65f,.68f,.65f),navy);
            foreach(float side in new[]{-1f,1f})
                Shape("Trilho do berço",ballPos+new Vector3(side*.104f,-.123f,0),new Vector3(.053f,.055f,.49f),gold);
            var ball=Grab(Shape("Bola de boliche",ballPos,Vector3.one*.31f,teal,PrimitiveType.Sphere));
            pinResets.Insert(0,()=>ResetBody(ball.GetComponent<Rigidbody>(),ballPos,Quaternion.identity));
            Station("REARMAR BOLICHE",new Vector3(43,1,-3),()=>{if(!ball.isSelected){foreach(var reset in pinResets)reset();Physics.SyncTransforms();}});
            var counter=Sign("ARCO ARCADE • 0 pontos",new Vector3(51,2.3f,5),.09f);
            var target=Shape("Alvo luminoso",new Vector3(51,1.5f,5),new Vector3(1.4f,1.4f,.15f),gold,PrimitiveType.Sphere);
            Shape("Suporte do arco",new Vector3(51,.45f,-2),new Vector3(1,.9f,1),navy);
            var bow=Shape("Arco arcade",new Vector3(51,1.33f,-2),new Vector3(.08f,.7f,.09f),teal);
            var visual=new GameObject("Arco recurvo").transform;
            visual.SetParent(bow.transform,false);visual.localScale=new Vector3(12.5f,1/.7f,1/.09f);
            foreach(int side in new[]{-1,1}){
                var last=new Vector3(0,side*.22f,0);
                foreach(var point in new[]{new Vector2(.36f,.045f),new Vector2(.50f,.012f),new Vector2(.62f,-.10f),new Vector2(.71f,-.14f),new Vector2(.77f,-.12f)}){
                    var next=new Vector3(0,point.x*side,point.y);
                    var limb=Shape("Lâmina laminada",(last+next)/2,new Vector3(.045f,(next-last).magnitude+.012f,.023f),gold,parent:visual);
                    limb.transform.localRotation=Quaternion.FromToRotation(Vector3.up,next-last);limb.GetComponent<Collider>().enabled=false;last=next;
                }
            }
            Shape("Corda do arco",new Vector3(0,0,-.12f),new Vector3(.005f,1.54f,.005f),navy,parent:visual).GetComponent<Collider>().enabled=false;
            var hand=Grab(bow);
            Sign("ARCO • mire e use o botão direito",new Vector3(51,2,-2),.073f);
            var loaded=ArcadeArrow(visual);
            float reloadAt=0;
            ticks.Add(()=>{if(!loaded && Time.time>=reloadAt)loaded=ArcadeArrow(visual);});
            hand.activated.AddListener(e=>{
                if(!loaded)return;
                var arrow=loaded;loaded=null;reloadAt=Time.time+.35f;
                arrow.transform.SetParent(area,true);
                arrow.transform.SetPositionAndRotation(bow.transform.position+bow.transform.forward*.38f,bow.transform.rotation);
                var collider=arrow.AddComponent<BoxCollider>();collider.center=new Vector3(0,0,.48f);collider.size=new Vector3(.055f,.055f,.12f);
                var rb=arrow.AddComponent<Rigidbody>();rb.useGravity=false;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                rb.linearVelocity=arrow.transform.forward*9;
                arrow.AddComponent<ArcadeStar>().Setup(target,()=>counter.text="ARCO ARCADE • "+(++score)+" pontos");
                Destroy(arrow,3);
            });
            Station("VOLTAR À ALA",new Vector3(48,1,-6),()=>Move(new Vector3(24,0,0)));
        }
        GameObject ArcadeArrow(Transform holder)
        {
            var arrow=new GameObject("Flecha de luz");
            arrow.transform.SetParent(holder,false);
            arrow.transform.localPosition=new Vector3(.032f,.035f,-.13f);
            void Part(string finish,Vector3 position,Vector3 scale,Quaternion rotation){
                var piece=Shape(finish,position,scale,gold,parent:arrow.transform);
                piece.transform.localRotation=rotation;
                piece.GetComponent<Collider>().enabled=false;
            }
            Part("Rastro",new Vector3(0,0,.23f),new Vector3(.022f,.022f,.5f),Quaternion.identity);
            Part("Estrela arcade",new Vector3(0,0,.50f),new Vector3(.065f,.045f,.065f),Quaternion.Euler(0,45,0));
            for(int i=0;i<3;i++){
                var twist=Quaternion.Euler(0,0,120*i);
                Part("Ponta do arco",twist*new Vector3(0,.029f,.035f),new Vector3(.012f,.058f,.13f),twist*Quaternion.Euler(-12,0,0));
            }
            return arrow;
        }
        void Music()
        {
            int rate=16000;var samples=new float[rate*8];int[] notes={60,64,67,71,69,65,62,67};
            for(int i=0;i<samples.Length;i++){float time=(float)i/rate;float beat=time%1;float hz=440*Mathf.Pow(2,(notes[(int)time]-69)/12f);samples[i]=.12f*Mathf.Sin(2*Mathf.PI*hz*beat)*Mathf.Sin(Mathf.PI*beat)*Mathf.Exp(-beat*3);}
            var clip=AudioClip.Create("Primeiro dia",samples.Length,1,rate,false);clip.SetData(samples,0);var source=gameObject.AddComponent<AudioSource>();source.clip=clip;source.loop=true;source.volume=.25f;source.Play();
        }
    }
    public class ArcadeStar : MonoBehaviour
    {
        GameObject target;Action hit;
        public void Setup(GameObject t,Action action){target=t;hit=action;}
        void OnCollisionEnter(Collision collision){if(collision.gameObject==target)hit();Destroy(gameObject);}
    }
}
