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
        Transform hinge, handleHand;
        float initialAngle, initialHand;
        public bool DoorOpen => hinge && Quaternion.Angle(Quaternion.identity, hinge.localRotation) > 45;
        readonly Color navy = new Color(.04f,.13f,.22f), teal = new Color(.04f,.58f,.59f), gold = new Color(1,.68f,.14f);
        readonly MissionSurfaces surfaces = new MissionSurfaces();
        readonly List<Action> ticks = new List<Action>();
        readonly List<Action> pinResets = new List<Action>();
        readonly List<Action> pending = new List<Action>();
        Transform area;
        TextMesh status, mural;
        GameObject confirm, entry, lounge;
        GameObject loungeGate;
        int stage, placed, score;
        const int ItemCount = 6;
        public void Initialize(ManagerMission manager)
        {
            mission = manager;
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
            PassageFloor("Galeria da praça",new Vector3(14,-.11f,5),new Vector3(4,.20f,14));
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
            grip.selectEntered.AddListener(e => { if(mission.State != ManagerMissionState.FindRoom)return; handleHand=e.interactorObject.GetAttachTransform(grip); initialHand=HandAngle(); initialAngle=Mathf.DeltaAngle(0,hinge.localEulerAngles.y); });
            grip.selectExited.AddListener(e=>handleHand=null);
            entry = Station("ENTRAR NA ALA DE ATIVIDADES",office.TransformPoint(new Vector3(1.85f,1.15f,-3.65f)),()=>Move(new Vector3(24,0,0)));
            entry.SetActive(false);
            manager.onCompleted.AddListener(()=>{ entry.SetActive(true); stage=1; });
            gameObject.AddComponent<LabPlaza>().Build();
            BuildWing();
            BuildLounge();
            loungeGate=Shape("Porta da descompressão",new Vector3(41,1.2f,0),new Vector3(.12f,2.4f,2.4f),teal);
            Sign("DESCOMPRESSÃO   •   ABRE AO CONCLUIR O EXPEDIENTE",new Vector3(40.8f,2.6f,0),.067f);
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
            foreach(float height in new[]{.70f,1.16f})
            {
                Shape("Prancha de carvalho",new Vector3(1.31f,height,-1.42f),new Vector3(.42f,.05f,1.14f),gold,parent:office);
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
            if (mission.State==ManagerMissionState.FindRoom)
                return DoorOpen?"Atravesse a porta e entre na sala.":Near(hinge.TransformPoint(new Vector3(.99f,1,.12f)))?"[F] Abrir a porta":"Siga as marcas azuis até a porta da sala.";
            return entry.activeSelf && Near(entry.transform.position)?"[F] Entrar na ala de atividades":null;
        }
        public void UseRoom()
        {
            if(mission.State==ManagerMissionState.FindRoom && !DoorOpen && Near(hinge.TransformPoint(new Vector3(.99f,1,.12f))))
                hinge.localRotation=Quaternion.Euler(0,90,0);
            else if(entry.activeSelf && Near(entry.transform.position)) Move(new Vector3(24,0,0));
        }
        float HandAngle(){var d=handleHand.position-hinge.position;return Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;}
        void Update()
        {
            if(handleHand) hinge.localRotation=Quaternion.Euler(0,Mathf.Clamp(initialAngle+Mathf.DeltaAngle(initialHand,HandAngle()),-100,100),0);
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
        readonly Dictionary<GameObject, Action> desktopActions = new Dictionary<GameObject, Action>();
        public bool IsStation(GameObject g) => desktopActions.ContainsKey(g);
        public bool UseStation(GameObject station)
        {
            if (!desktopActions.TryGetValue(station, out var action)) return false;
            pending.Add(action);
            return true;
        }
        GameObject Station(string caption,Vector3 p,Action action)
        {
            var button=Shape(caption,p,new Vector3(1.55f,.45f,.22f),navy);
            var label=Sign(caption,p+Vector3.up*.36f,.055f);label.transform.SetParent(button.transform,true);
            var hitBox = button.GetComponent<BoxCollider>();
            hitBox.center = new Vector3(0,.25f,0);
            hitBox.size = new Vector3(1.2f,1.6f,1);
            desktopActions[button] = action; button.AddComponent<XRSimpleInteractable>().selectEntered.AddListener(e=>pending.Add(action));return button;
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
        void BuildWing()
        {
            Room("Ala de atividades",26,12,10);
            Sign("SALA DE AULA   •   BIBLIOTECA E INFORMÁTICA",new Vector3(28,2.35f,1),.07f);
            Sign("DESCOMPRESSÃO  →",new Vector3(31.9f,2.2f,0),.09f);
            status=Sign("Visite o Mural do Conhecimento",new Vector3(26,2.6f,0),.105f);
            mural=Sign("MURAL DO CONHECIMENTO",new Vector3(22,1.95f,2),.085f);
            Station("LER O MURAL",new Vector3(22,1,2),()=>{if(stage!=1)return;mural.text="SEBRAE LAB\nUm espaço para experimentar ideias,\ntrocar experiências e aprender em grupo.";confirm.SetActive(true);});
            confirm=Station("ENTENDIDO",new Vector3(23.8f,1,2),()=>{if(stage!=1)return;stage=2;confirm.SetActive(false);mural.text="MURAL DO CONHECIMENTO";status.text="Encontre a copa e pegue o café.";});confirm.SetActive(false);
            Shape("Balcão da copa",new Vector3(26,.45f,3),new Vector3(2,.9f,.8f),teal);
            var cup=Grab(Shape("Copo de café",new Vector3(26,1.02f,3),new Vector3(.13f,.1f,.13f),gold,PrimitiveType.Cylinder));
            cup.selectEntered.AddListener(e=>{if(stage==2){stage=3;status.text="Coworking • guarde os livros, alinhe as cadeiras\ne leve os notebooks às mesas da sala de aula.";}});Sign("COPA",new Vector3(26,1.9f,3),.09f);
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
        int furniture;
        void Delivery(string name,Vector3 p,Vector3 target,Vector3 size,Color color,int kind)
        {
            var g=Shape(name,p,size,color);
            if(kind==1)Shape("Encosto",new Vector3(0,.7f,-.4f),new Vector3(1,.8f,.16f),navy,parent:g.transform);
            if(kind==2)Shape("Tela",new Vector3(0,4.9f,-.46f),new Vector3(1,8.8f,.027f),navy,parent:g.transform);
            var marker=Shape("Destino • "+name,target-Vector3.up*(size.y/2-.005f),new Vector3(size.x+.08f,.012f,size.z+.08f),gold);marker.GetComponent<Collider>().enabled=false;
            var grab=Grab(g);bool delivered=false;
            var deliveryBody = g.GetComponent<Rigidbody>();
            grab.selectExited.AddListener(e=>{
                var releasePosition = deliveryBody.position;
                Debug.Log($"Entrega {name}: {releasePosition} -> {target} (etapa {stage})");
                pending.Add(()=>{
                if(stage!=3||delivered||Vector3.Distance(releasePosition,target)>.58f)return;
                if(kind==2 && furniture<4){status.text="Antes dos notebooks, guarde os dois livros e alinhe as duas cadeiras.";return;}
                delivered=true;var body=g.GetComponent<Rigidbody>();body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;g.transform.SetPositionAndRotation(target,Quaternion.identity);marker.SetActive(false);
                if(kind<2)furniture++;placed++;status.text="Organização • "+placed+" de "+ItemCount+" itens";
                if(placed==ItemCount){stage=4;loungeGate.SetActive(false);status.text="EXPEDIENTE CONCLUÍDO\nEntre na sala de descompressão.";lounge.SetActive(true);}
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
            for(int r=0;r<4;r++)for(int c=0;c<=r;c++){
                var p=new Vector3(45+(c-r*.5f)*.27f,.32f,3+r*.27f);var pin=Shape("Pino",p,new Vector3(.16f,.24f,.16f),Color.white,PrimitiveType.Capsule);
                var rb=pin.AddComponent<Rigidbody>();rb.mass=.22f;pinResets.Add(()=>ResetBody(rb,p,Quaternion.identity));}
            Sign("BOLICHE • lance a bola pela pista",new Vector3(45,1.9f,-3.5f),.085f);
            var ballPos=new Vector3(45,.85f,-3.8f);
            Shape("Suporte da bola",ballPos-Vector3.up*.48f,new Vector3(.65f,.68f,.65f),navy);
            foreach(float side in new[]{-1f,1f})
                Shape("Trilho do berço",ballPos+new Vector3(side*.104f,-.123f,0),new Vector3(.053f,.055f,.49f),gold);
            var ball=Grab(Shape("Bola de boliche",ballPos,Vector3.one*.31f,teal,PrimitiveType.Sphere));
            pinResets.Add(()=>ResetBody(ball.GetComponent<Rigidbody>(),ballPos,Quaternion.identity));
            Station("REARMAR BOLICHE",new Vector3(43,1,-3),()=>{if(!ball.isSelected)foreach(var reset in pinResets)reset();});
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
            var hand=Grab(bow);Sign("Segure o arco e use o gatilho\npara lançar uma flecha de luz.",new Vector3(51,2,-2),.073f);
            hand.activated.AddListener(e=>{
                var star=Shape("Estrela arcade",bow.transform.position+bow.transform.forward*.4f,Vector3.one*.11f,gold,PrimitiveType.Sphere);
                star.name="Flecha de luz";star.transform.rotation=bow.transform.rotation;
                Shape("Rastro",new Vector3(0,0,-1),new Vector3(.23f,.23f,2),teal,parent:star.transform).GetComponent<Collider>().enabled=false;
                var rb=star.AddComponent<Rigidbody>();rb.useGravity=false;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.linearVelocity=bow.transform.forward*7;
                star.AddComponent<ArcadeStar>().Setup(target,()=>counter.text="ARCO ARCADE • "+(++score)+" pontos");Destroy(star,3);
            });
            Station("VOLTAR À ALA",new Vector3(48,1,-6),()=>Move(new Vector3(24,0,0)));
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
