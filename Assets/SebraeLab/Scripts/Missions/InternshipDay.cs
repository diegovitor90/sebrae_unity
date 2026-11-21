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
        HandDoor door;
        public bool DoorOpen => door && door.IsPassable;
        readonly Color navy = new Color(.04f,.13f,.22f), teal = new Color(.04f,.58f,.59f), gold = new Color(1,.68f,.14f);
        readonly MissionSurfaces surfaces = new MissionSurfaces();
        readonly List<Action> ticks = new List<Action>();
        readonly List<Action> pending = new List<Action>();
        Transform area;
        Feixe feixe; SetaDoPulso seta;
        Transform descompressao;
        TextMesh status, mural;
        GameObject confirm, coffeeSpot, botaoDescompressao;
        PortaDeVidro portaDeVidro;
        TextMesh placaDaPorta;
        bool portaDosFundosAberta;
        // Organização: item que está na mão e itens ainda não entregues, por tipo (livro, cadeira, notebook).
        GameObject itemNaMao;
        readonly List<GameObject>[] itens={new List<GameObject>(),new List<GameObject>(),new List<GameObject>()};
        public bool PortaDosFundosAberta => portaDosFundosAberta;
        XRGrabInteractable coffee;
        bool coffeeCollected;
        readonly Dictionary<GameObject,GameObject> assigned=new Dictionary<GameObject,GameObject>();
        public GameObject Destination(GameObject item) => assigned.TryGetValue(item,out var spot) && spot.activeInHierarchy ? spot : null;
        public bool Reading => confirm && confirm.activeSelf;
        public void ConfirmReading(){if(Reading)UseStation(confirm);}
        public void ReturnToStart(){Move(PlantaDaTrilha.Nascimento);}
        int placed;
        const int ItemCount = 6;
        // Etapa atual, objetivo e avisos. Aparece igual na placa de status (Quest) e no HUD do PC.
        MissionManager etapas;
        public MissionManager Etapas => etapas;
        public void Initialize(ManagerMission manager)
        {
            mission = manager;
            if (!TryGetComponent(out etapas)) etapas = gameObject.AddComponent<MissionManager>();
            area = new GameObject("Missão Estágio • percurso").transform;
            Sign("PRIMEIRO DIA\nProcure o gestor do espaço",new Vector3(3.9f,1.9f,7.4f),.067f,270f); // lida por quem nasce a leste, olhando para o gerente (−X)
            Music();
            // A trilha acontece dentro do prédio: as marcas azuis e a placa "SUA SALA" da cena saem (os objetos ficam, vazios).
            foreach(Transform mark in mission.routeMarkers.transform) Destroy(mark.gameObject);
            foreach(Transform mark in mission.roomMarker.transform) Destroy(mark.gameObject);
            MontarSala2();
            manager.onCompleted.AddListener(()=>etapas.Concluir(EtapaExpediente.Sala));
            gameObject.AddComponent<LabPlaza>().Build();
            MontarMural();
            MontarCafe();
            MontarOrganizacao();
            MontarFundos();
            BuildLounge();
            feixe=Feixe.Criar(area,mission.playerHead);
            // No PC, o DesktopPlayer já trocou o playerRoot e desligou o rig VR; o controle é procurado na cena toda, preferindo o ativo.
            Transform esquerdo=null;
            foreach(var t in FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(t.name=="Left Controller" && (!esquerdo || t.gameObject.activeInHierarchy)) esquerdo=t;
            if(esquerdo) seta=SetaDoPulso.Criar(esquerdo,mission.playerHead,()=>feixe.transform.position);
            ticks.Add(()=>feixe.Mover(GuiaDaTrilha.Destino(Estado())));
        }
        /// <summary>Sala 2 na Sala A do prédio: porta com maçaneta no vão do corredor e o conteúdo do escritório antigo.</summary>
        void MontarSala2()
        {
            var sala=new GameObject("Sala 2 • porta").transform;
            sala.SetParent(area,false);
            sala.SetPositionAndRotation(PlantaDaTrilha.Dobradica,Quaternion.Euler(0f,PlantaDaTrilha.GuinadaDaPorta,0f));
            hinge=new GameObject("Dobradiça da sala").transform;
            hinge.SetParent(sala,false);
            door=hinge.gameObject.AddComponent<HandDoor>();
            float largura=PlantaDaTrilha.LarguraDaPorta;
            Shape("Porta • sala do estagiário",new Vector3(largura/2f,1.04f,0),new Vector3(largura,2.08f,.07f),teal,parent:hinge);
            var knob=Shape("Maçaneta",new Vector3(largura-.13f,1,.12f),new Vector3(.22f,.09f,.17f),gold,parent:hinge);
            var insideHandle=Shape("Maçaneta interna",new Vector3(0,0,-1.42f),Vector3.one,gold,parent:knob.transform);
            var grip=knob.AddComponent<XRSimpleInteractable>();
            door.Bind(grip);
            desktopActions[knob]=ToggleDoor;
            desktopActions[insideHandle]=ToggleDoor;
            // Área que conta como "entrou na sala": o interior da Sala A.
            mission.room.transform.position=PlantaDaTrilha.AreaDeConclusao;
            var zona=mission.room.GetComponent<BoxCollider>();
            zona.center=new Vector3(0f,PlantaDaTrilha.TamanhoDaAreaDeConclusao.y/2f,0f);
            zona.size=PlantaDaTrilha.TamanhoDaAreaDeConclusao;
            Sign("SALA 2",PlantaDaTrilha.PlacaSala2,.09f,PlantaDaTrilha.GuinadaDasPortasDoCorredor);
            Shape("Mesa do escritório",PlantaDaTrilha.MesaDoEscritorio,new Vector3(1.25f,.88f,.7f),gold);
            var chair=Shape("Cadeira do escritório",PlantaDaTrilha.CadeiraDoEscritorio,new Vector3(.48f,.5f,.48f),teal);
            Shape("Encosto",new Vector3(0,.72f,.43f),new Vector3(1,.85f,.13f),navy,parent:chair.transform);
            Shape("Quadro Redes",PlantaDaTrilha.QuadroDoEscritorio,new Vector3(.04f,.8f,.95f),navy);
            var prateleira=PlantaDaTrilha.PrateleiraDoEscritorio;
            foreach(float dx in new[]{-.6f,.6f})
                Shape("Prancha de carvalho",prateleira+new Vector3(dx,.61f,0),new Vector3(.07f,1.22f,.44f),gold);
            foreach(float altura in new[]{.70f,1.16f})
            {
                Shape("Prancha de carvalho",prateleira+new Vector3(0,altura,0),new Vector3(1.18f,.05f,.42f),gold);
                for(int i=0;i<6;i++)
                {
                    int id=(altura<1?24:30)+i;
                    var volume=Shape("Volume "+id,prateleira+new Vector3(-.42f+i*.078f,altura+.16f,0),new Vector3(.26f,.062f,.21f),gold);
                    volume.transform.rotation=Quaternion.Euler(0,0,90);
                    Grab(volume).GetComponent<Rigidbody>().mass=.31f;
                }
            }
        }

        bool Near(Vector3 target)
        {
            var delta=mission.playerHead.position-target;
            return Mathf.Abs(delta.y)<1.6f && new Vector2(delta.x,delta.z).sqrMagnitude<2.25f;
        }
        Vector3 Macaneta => hinge.TransformPoint(new Vector3(PlantaDaTrilha.LarguraDaPorta-.13f,1,0));
        public string RoomHint()
        {
            if (Near(Macaneta)) return door.IsOpen?"[F] Fechar a porta":"[F] Abrir a porta";
            if (mission.State==ManagerMissionState.FindRoom)
                return DoorOpen?"Atravesse a porta e entre na sala.":"Siga a luz dourada até a porta da Sala 2.";
            return null;
        }
        public void UseRoom()
        {
            if(Near(Macaneta)) ToggleDoor();
        }
        void ToggleDoor() => door.Toggle();
        void Update()
        {
            if(mission.playerRoot.position.y < -2f) Move(PlantaDaTrilha.Nascimento+Vector3.up*.05f);
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
        // Placa parada, em caixa, legível dos dois lados e escondida por paredes (ver Placa).
        TextMesh Sign(string value,Vector3 pos,float size,float? guinada=null) => Placa.Criar(value,pos,size,area,guinada);
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
        GameObject Station(string caption,Vector3 p,Action action,EtapaExpediente? etapa=null,float? guinada=null)
        {
            // A ação entra na fila e roda no Update: evita mexer na cena dentro do evento de UI.
            var button=MissionButton.Criar(caption,area,p+Vector3.up*.25f,new Vector2(1.1f,.3f),()=>pending.Add(action),etapas,etapa);
            var root=button.Peca; // corpo e as duas faces: SetActive(false) esconde tudo
            // Parado: a direção é a do trecho, definida uma vez, sem girar para o jogador.
            root.transform.rotation=Quaternion.Euler(0f,guinada??Placa.GuinadaPadrao(root.transform.position),0f);
            stations[root]=button;
            return root;
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
        /// <summary>Mural na parede leste do corredor, placa do expediente e mesa de inovação sobre a Mesa Grande original.</summary>
        void MontarMural()
        {
            var quadro=GameObject.Find(PlantaDaTrilha.QuadroQueSai);
            if(quadro) quadro.SetActive(false);
            mural=Sign("MURAL DO CONHECIMENTO",PlantaDaTrilha.Mural,.085f,PlantaDaTrilha.GuinadaDoMural);
            // Liberado a partir do Mural: pode ser relido depois, como antes.
            var muralButton=Station("LER O MURAL",PlantaDaTrilha.BotaoLerMural,()=>{if(!etapas.Alcancou(EtapaExpediente.Mural))return;mural.text="SEBRAE LAB\nUm espaço para experimentar ideias,\ntrocar experiências e aprender em grupo.";confirm.SetActive(true);Cursor.lockState=CursorLockMode.None;},EtapaExpediente.Mural,PlantaDaTrilha.GuinadaDoMural);
            mural.gameObject.AddComponent<BoxCollider>();
            mural.GetComponent<Placa>().colisorMinimo=new Vector3(2.1f,.8f,.1f); // área de clique do PC: nunca menor que a de antes
            stations[mural.gameObject]=stations[muralButton];
            confirm=Station("ENTENDIDO",PlantaDaTrilha.BotaoEntendido,()=>{confirm.SetActive(false);Cursor.lockState=CursorLockMode.Locked;if(!etapas.Em(EtapaExpediente.Mural))return;etapas.Concluir(EtapaExpediente.Mural);coffee.enabled=true;coffeeSpot.SetActive(true);mural.text="MURAL DO CONHECIMENTO";},guinada:PlantaDaTrilha.GuinadaDoMural);
            confirm.SetActive(false);
            status=Sign(etapas.Texto,PlantaDaTrilha.PlacaDoExpediente,.105f,PlantaDaTrilha.GuinadaDaPlacaDoExpediente);
            etapas.TextoMudou+=texto=>status.text=texto;
            Sign("MESA DE INOVAÇÃO",PlantaDaTrilha.PlacaDaMesaDeInovacao,.07f,PlantaDaTrilha.GuinadaDaMesaDeInovacao);
            Grab(Shape("Cartilha de inovação",PlantaDaTrilha.Cartilha,new Vector3(.25f,.04f,.32f),gold));
            Grab(Shape("Protótipo",PlantaDaTrilha.Prototipo,Vector3.one*.25f,teal,PrimitiveType.Sphere));
        }

        /// <summary>Copo na bancada real da copa; entrega no pires da mesa da Sala 2.</summary>
        void MontarCafe()
        {
            coffee=Grab(Shape("Copo de café",PlantaDaTrilha.CopoDeCafe,new Vector3(.13f,.1f,.13f),gold,PrimitiveType.Cylinder));
            coffee.enabled=false;
            var saucer=PlantaDaTrilha.Pires;
            coffeeSpot=Shape("Pires • café",saucer,new Vector3(.32f,.012f,.32f),gold,PrimitiveType.Cylinder);
            coffeeSpot.GetComponent<Collider>().enabled=false;
            assigned[coffee.gameObject]=coffeeSpot;
            var coffeeLabel=Sign("CAFÉ   •   ENTREGUE NO PIRES",saucer+Vector3.up*.27f,.065f,180f); // lida por quem está diante da mesa, olhando para o sul
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
            Sign("COPA   •   RETIRE O CAFÉ",PlantaDaTrilha.PlacaDaCopa,.09f,PlantaDaTrilha.GuinadaDaPlacaDaCopa);
        }

        /// <summary>Sala de aula na Sala B: estante e duas mesas; os 6 itens começam dentro da própria sala.</summary>
        void MontarOrganizacao()
        {
            var estante=Shape("Prateleira",PlantaDaTrilha.Estante,PlantaDaTrilha.TamanhoDaEstante,teal);
            estante.transform.rotation=Quaternion.Euler(0f,90f,0f); // comprimento ao longo da parede oeste
            Sign("ESTANTE",PlantaDaTrilha.Estante+new Vector3(.4f,1.2f,0f),.075f,270f);
            foreach(var mesa in PlantaDaTrilha.Mesas)
            {
                Shape("Mesa da sala de aula",mesa,PlantaDaTrilha.TamanhoDaMesa,teal);
                Shape("Tela",new Vector3(mesa.x,1.31f,mesa.z-.27f),new Vector3(.53f,.32f,.035f),navy);
                Shape("Pé de aço",new Vector3(mesa.x,1.04f,mesa.z-.27f),new Vector3(.07f,.24f,.09f),navy);
            }
            Sign("SALA DE AULA",PlantaDaTrilha.PlacaSalaDeAula,.075f,PlantaDaTrilha.GuinadaDasPortasDoCorredor);
            for(int i=0;i<2;i++) Delivery("Livro",PlantaDaTrilha.InicioDosLivros[i],PlantaDaTrilha.DestinoDosLivros[i],new Vector3(.25f,.08f,.32f),gold,0);
            for(int i=0;i<2;i++) Delivery("Cadeira",PlantaDaTrilha.InicioDasCadeiras[i],PlantaDaTrilha.DestinoDasCadeiras[i],new Vector3(.46f,.5f,.45f),teal,1);
            for(int i=0;i<2;i++) Delivery("Notebook",PlantaDaTrilha.InicioDosNotebooks[i],PlantaDaTrilha.DestinoDosNotebooks[i],new Vector3(.4f,.025f,.28f),Color.gray,2);
        }

        /// <summary>Porta de vidro dos fundos trancada até o fim, frestas fechadas e botão da descompressão escondido.</summary>
        void MontarFundos()
        {
            var vidro=GameObject.Find(PlantaDaTrilha.NomeDaPortaDeVidro);
            if(!vidro) throw new InvalidOperationException("Porta de vidro dos fundos ausente: "+PlantaDaTrilha.NomeDaPortaDeVidro);
            portaDeVidro=vidro.AddComponent<PortaDeVidro>();
            placaDaPorta=Sign("DESCOMPRESSÃO   •   ABRE AO CONCLUIR O EXPEDIENTE",PlantaDaTrilha.PlacaDaPortaDosFundos,.067f,PlantaDaTrilha.GuinadaDaPortaDosFundos);
            var frestas=new GameObject("Frestas • porta dos fundos").transform;
            frestas.SetParent(area,false);
            for(int i=0;i<PlantaDaTrilha.Frestas.Length;i++)
            {
                var fresta=new GameObject("Fresta "+(i+1)).AddComponent<BoxCollider>();
                fresta.transform.SetParent(frestas,false);
                fresta.transform.position=PlantaDaTrilha.Frestas[i].center;
                fresta.size=PlantaDaTrilha.Frestas[i].size;
            }
            botaoDescompressao=Station("SALA DE DESCOMPRESSÃO",PlantaDaTrilha.BotaoDaDescompressao,()=>Move(PlantaDaTrilha.EntradaDaDescompressao),guinada:180f);
            botaoDescompressao.SetActive(false);
        }

        /// <summary>Fim da trilha: abre a porta de vidro dos fundos e mostra o botão da descompressão.</summary>
        public void LiberarDescompressao()
        {
            if(portaDosFundosAberta)return;
            portaDosFundosAberta=true;
            portaDeVidro.Abrir();
            placaDaPorta.text="DESCOMPRESSÃO   •   ENTRADA LIBERADA";
            botaoDescompressao.SetActive(true);
            descompressao.gameObject.SetActive(true); Physics.SyncTransforms();
        }

        /// <summary>O que a sinalização precisa saber agora (ver GuiaDaTrilha).</summary>
        public EstadoDaTrilha Estado()
        {
            var e=new EstadoDaTrilha{Gerente=mission.State,Etapa=etapas.Atual,CafeNaMao=coffeeCollected,PortaDosFundosAberta=portaDosFundosAberta};
            if(etapas.Em(EtapaExpediente.Organizacao) && placed<ItemCount)
            {
                var pendentes=itens[placed/2];
                if(itemNaMao && pendentes.Contains(itemNaMao) && assigned.TryGetValue(itemNaMao,out var destino)) e.DestinoDoItemNaMao=destino.transform.position;
                else if(pendentes.Count>0) e.ProximoItem=pendentes[0].transform.position;
            }
            return e;
        }

        /// <summary>Seta de texto do HUD do PC, vazia quando o feixe está na vista.</summary>
        public string SetaDoHud()=>feixe && mission.playerHead ? GuiaDaTrilha.SetaDoHud(mission.playerHead.position,mission.playerHead.forward,feixe.transform.position) : "";
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
            itens[kind].Add(g);
            grab.selectEntered.AddListener(e=>itemNaMao=g);
            grab.selectExited.AddListener(e=>{if(itemNaMao==g)itemNaMao=null;});
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
                delivered=true;itens[kind].Remove(g);tag.gameObject.SetActive(false);
                deliveryBody.isKinematic=true;
                grab.enabled=false;
                g.transform.SetPositionAndRotation(slot.transform.position+Vector3.up*(size.y/2),Quaternion.identity);
                destinations[kind].Remove(slot);slot.SetActive(false);
                placed++;
                if(placed==ItemCount){etapas.Concluir(EtapaExpediente.Organizacao);LiberarDescompressao();}
                else etapas.Progresso("Organização • "+placed+" de "+ItemCount+" itens");
                ShowDeliveryTargets();
            });});
        }
        void BuildLounge()
        {
            // Montada com as coordenadas de antes (x de 41 a 55), dentro de uma raiz deslocada para trás da parede sul.
            // Shape usa posição local da área; Sign, Station, luminárias e luzes recebem posição no mundo, por L().
            descompressao=new GameObject("Descompressão (provisória)").transform;
            descompressao.position=PlantaDaTrilha.DeslocamentoDaDescompressao;
            var trilha=area; area=descompressao;
            Vector3 L(Vector3 p)=>descompressao.TransformPoint(p);
            const float guinada=PlantaDaTrilha.GuinadaDaDescompressao;
            Room("Sala de descompressão",48,14,16);
            Sign("SALA DE DESCOMPRESSÃO",L(new Vector3(48,2.6f,0)),.13f,guinada);
            var lane=Shape("Pista de boliche",new Vector3(45,.04f,1),new Vector3(2,.08f,9),gold);
            var gutterFloors=new List<Collider>();var gutterLips=new List<Collider>();
            foreach(float side in new[]{-1f,1f})
            {
                float center=45+side*1.24f;
                gutterFloors.Add(Shape("Fundo da calha",new Vector3(center,-.19f,1),new Vector3(.49f,.08f,9),navy).GetComponent<Collider>());
                foreach(float edge in new[]{-1f,1f}){
                    var lip=Shape("Lateral da calha",new Vector3(center+edge*.19f,-.075f,1),new Vector3(.09f,.28f,9),navy);
                    lip.transform.rotation=Quaternion.Euler(0,0,-edge*25);
                    gutterLips.Add(lip.GetComponent<Collider>());
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
                lamp.transform.position+=L(new Vector3(x,3.16f,z))-new Vector3(bounds.center.x,bounds.max.y,bounds.center.z);
                var light=new GameObject("Luz quente").AddComponent<Light>();light.transform.SetParent(lamp.transform,true);
                light.transform.position=L(new Vector3(x,2.9f,z));light.color=new Color(1,.89f,.73f);light.range=5;light.intensity=1.3f;light.renderMode=LightRenderMode.ForceVertex;
            }
            // Placar, rodadas e contagem ficam no BowlingLane.
            var board=Sign("BOLICHE",L(new Vector3(45,2.2f,5.4f)),.12f,guinada);
            var pins=new List<Rigidbody>();
            for(int r=0;r<4;r++)for(int c=0;c<=r;c++){
                var p=new Vector3(45+(c-r*.5f)*.27f,.32f,3+r*.27f);var pin=Shape("Pino",p,new Vector3(.16f,.24f,.16f),Color.white,PrimitiveType.Capsule);
                // A flat sole supports the upright pin between throws.
                pin.GetComponent<CapsuleCollider>().enabled=false;
                var foot=pin.AddComponent<BoxCollider>();
                foot.center=new Vector3(0,-.75f,0);foot.size=new Vector3(.69f,.5f,.69f);
                var belly=pin.AddComponent<SphereCollider>();belly.center=new Vector3(0,-.27f,0);belly.radius=.47f;
                var neck=pin.AddComponent<BoxCollider>();neck.center=new Vector3(0,.48f,0);neck.size=new Vector3(.40f,.98f,.40f);
                pins.Add(pin.AddComponent<Rigidbody>());}
            Sign("BOLICHE • lance a bola pela pista",L(new Vector3(45,1.9f,-3.5f)),.085f,guinada);
            var ballPos=new Vector3(45,.85f,-3.8f);
            Shape("Suporte da bola",ballPos-Vector3.up*.48f,new Vector3(.65f,.68f,.65f),navy);
            foreach(float side in new[]{-1f,1f})
                Shape("Trilho do berço",ballPos+new Vector3(side*.104f,-.123f,0),new Vector3(.053f,.055f,.49f),gold);
            var ball=Grab(Shape("Bola de boliche",ballPos,Vector3.one*.31f,teal,PrimitiveType.Sphere));
            var bowling=gameObject.AddComponent<BowlingLane>();
            bowling.Configurar(lane.GetComponent<Collider>(),gutterFloors,gutterLips,pins,ball,board);
            Station("REARMAR BOLICHE",L(new Vector3(43,1,-3)),bowling.Rearmar,guinada:guinada);
            var counter=Sign("ARCO ARCADE • 0 pontos",L(new Vector3(51,2.3f,5)),.09f,guinada);
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
            Sign("ARCO • segure e puxe a corda\ncom a outra mão\nPC: botão direito dispara",L(new Vector3(51,2,-2)),.073f,guinada);
            // Corda, disparo, física da flecha, anéis e placar ficam no ArcheryRange.
            gameObject.AddComponent<ArcheryRange>().Configurar(hand,visual,()=>ArcadeArrow(visual),target,counter,area);
            Station("VOLTAR AO PRÉDIO",L(new Vector3(48,1,-6)),()=>Move(PlantaDaTrilha.ChegadaDosFundos),guinada:guinada);
            Station("RECOMEÇAR O DIA",L(new Vector3(50,1,-6)),()=>UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.name),guinada:guinada);
            area=trilha;
            descompressao.gameObject.SetActive(false); // só aparece quando a porta dos fundos abre
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
}
