using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SebraeLab.Missions;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class ExpedienteValidation {
 const string Phase="Expediente.TestStage";
 static string Log(string name) => Path.Combine(Application.dataPath, "../Logs", name);
 static string Request => Log("expediente-run.request");
 static double next;
 static ExpedienteValidation(){EditorApplication.update+=Run; File.WriteAllText(Log("expediente-editor.txt"),"Ready "+DateTime.Now);}
 [MenuItem("Sebrae Lab/Entrega/Testar percurso e gerar APK")]
 static void StartCheck(){File.WriteAllText(Request,"");Run();}
 static void Run(){
  if(EditorApplication.isUpdating || EditorApplication.isCompiling || EditorApplication.timeSinceStartup<next)return;
  next=EditorApplication.timeSinceStartup+1;
  try{
   var stage=SessionState.GetInt(Phase,0);
   if(File.Exists(Request)){File.WriteAllText(Log("expediente-editor.txt"),"Stage="+stage+" playing="+EditorApplication.isPlayingOrWillChangePlaymode);if(!EditorApplication.isPlayingOrWillChangePlaymode)stage=0;}
   if(stage==0 && File.Exists(Request) && !EditorApplication.isPlayingOrWillChangePlaymode){
    if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty){File.Delete(Request);File.WriteAllText(Log("expediente-result.txt"),"Cena aberta tem alterações não salvas.");return;}
    File.Delete(Request);SebraeLab.Editor.ManagerMissionSetup.Open();
    var sim=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(t=>t.name=="Simulador PC - excluido da build");
    sim.gameObject.SetActive(true); EditorSceneManager.SaveScene(sim.gameObject.scene);
    SessionState.SetInt(Phase,1);EditorApplication.EnterPlaymode();
   }else if(stage==1 && EditorApplication.isPlaying){
    var m=Object.FindFirstObjectByType<ManagerMission>(); if(!m || !m.day)return;
    CheckReception(m);
    foreach(var r in GameObject.Find("Missão Estágio • percurso").GetComponentsInChildren<MeshRenderer>(true)){
     if(r.enabled && !r.GetComponent<TextMesh>() && (!r.sharedMaterial || !r.sharedMaterial.mainTexture))throw new Exception("Sem textura: "+r.name);
    }
    var xr=Object.FindFirstObjectByType<XRInteractionManager>();
    var controller=new GameObject("Test controller");
    controller.AddComponent<SphereCollider>().isTrigger=true;
    IXRSelectInteractor hand=controller.AddComponent<XRDirectInteractor>();
    var volumes=Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None).Where(g=>g.name.StartsWith("Volume ")).ToArray();
    if(volumes.Length!=36)throw new Exception("Biblioteca incompleta");
    foreach(var book in volumes){ controller.transform.position=book.transform.position;xr.SelectEnter(hand,book);if(!book.isSelected)throw new Exception("Livro preso");xr.SelectExit(hand,book);if(!book.GetComponent<Rigidbody>().useGravity)throw new Exception("Sem gravidade");}
    var head=new GameObject("Test head");m.playerHead=head.transform;head.transform.position=m.manager.position+new Vector3(1.2f,1.65f,0);Physics.SyncTransforms();
    if(!m.TryBeginConversation())throw new Exception("Conversa não iniciou");
    foreach(var line in m.dialogue)m.TryAdvanceDialogue();
    var knob=GameObject.Find("Maçaneta").GetComponent<XRSimpleInteractable>();
    var pivot=GameObject.Find("Dobradiça da sala").transform.position;
    head.transform.position=knob.transform.position+Vector3.right*.8f;
    m.Interact();if(!m.day.DoorOpen)throw new Exception("Tecla de interação da porta não abriu.");
    knob.transform.parent.localRotation=Quaternion.identity;
    controller.transform.position=knob.transform.position;
    xr.SelectEnter(hand,knob);
    controller.transform.RotateAround(pivot,Vector3.up,75);
    m.day.SendMessage("Update");
    if(!m.day.DoorOpen)throw new Exception("A maçaneta não abriu a porta.");
    xr.SelectExit(hand,knob);
    head.transform.position=m.room.transform.position+Vector3.up*1.65f;m.EvaluatePlayer();
    if(m.State!=ManagerMissionState.Completed)throw new Exception("Sala não liberada");
    Press("ENTRAR NA ALA DE ATIVIDADES"); Press("LER O MURAL"); Press("ENTENDIDO");
    foreach(var item in new[]{"Cartilha de inovação","Protótipo","Copo de café"}){
     var grab=GameObject.Find(item).GetComponent<XRGrabInteractable>();
     controller.transform.position=grab.transform.position;xr.SelectEnter(hand,grab);
     if(!grab.isSelected)throw new Exception("Falha ao pegar "+item);
     xr.SelectExit(hand,grab);
    }
    foreach(var name in new[]{"Livro","Cadeira","Notebook"}){
     var items=Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None).Where(g=>g.name==name).OrderBy(g=>g.transform.position.x).ToArray();
     var spots=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(g=>g.name=="Destino • "+name).OrderBy(g=>g.transform.position.x).ToArray();
     if(items.Length!=2 || spots.Length!=2)throw new Exception("Itens: "+name);
     for(int i=0;i<2;i++){items[i].transform.position=spots[i].transform.position+Vector3.up*items[i].transform.localScale.y/2;items[i].selectExited.Invoke(null);m.day.SendMessage("Update");}
    }
    Press("SALA DE DESCOMPRESSÃO");Press("REARMAR BOLICHE");
    var pins=Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Where(b=>b.name=="Pino").ToArray();
    var ball=GameObject.Find("Bola de boliche").GetComponent<Rigidbody>();
    ball.position=new Vector3(45,.245f,-2);ball.linearVelocity=Vector3.forward*8;
    var previous=Physics.simulationMode;
    try{
     Physics.simulationMode=SimulationMode.Script; Physics.SyncTransforms();
     for(int i=0;i<150;i++)Physics.Simulate(.02f);
     int down=pins.Count(b=>Vector3.Angle(b.transform.up,Vector3.up)>40);
     if(down==0)throw new Exception("Nenhum pino caiu.");
     File.WriteAllText(Log("expediente-fisica.txt"),"Pinos derrubados: "+down);
     Press("REARMAR BOLICHE");
     var bow=GameObject.Find("Arco arcade").GetComponent<XRGrabInteractable>();
     bow.transform.SetPositionAndRotation(new Vector3(51,1.5f,-2),Quaternion.identity);
     bow.activated.Invoke(null);
     for(int i=0;i<58;i++)Physics.Simulate(.02f);
     if(!Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None).Any(t=>t.text=="ARCO ARCADE • 1 pontos"))throw new Exception("Alvo não pontuou.");
     File.AppendAllText(Log("expediente-fisica.txt"),"; alvo: 1 ponto.");
    }finally{Physics.simulationMode=previous;}
    
    File.WriteAllText(Log("expediente-result.txt"),"PLAY PASS: conversa, entrada, mural, café, seis entregas, descompressão, rearme do boliche e criação da flecha.");
    SessionState.SetInt(Phase,2);EditorApplication.ExitPlaymode();
   }else if(stage==2 && !EditorApplication.isPlayingOrWillChangePlaymode){
    SessionState.SetInt(Phase,0);SebraeLab.Editor.ManagerMissionSetup.Build();File.AppendAllText(Log("expediente-result.txt"),"\nAPK PASS");
   }
  }catch(Exception e){File.AppendAllText(Log("expediente-result.txt"),"\nERROR "+e);SessionState.SetInt(Phase,0);if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();}
 }
 static void CheckReception(ManagerMission m){
  var probe=new GameObject("Câmera de verificação").AddComponent<Camera>();
  probe.CopyFrom(m.playerHead.GetComponent<Camera>());probe.enabled=false;
  probe.stereoTargetEye=StereoTargetEyeMask.None;probe.transform.position=m.playerRoot.position+Vector3.up*1.55f;
  var rt=RenderTexture.GetTemporary(320,180,24);probe.targetTexture=rt;
  var previous=RenderTexture.active;var tex=new Texture2D(320,180,TextureFormat.RGB24,false);
  try{
   for(int yaw=0;yaw<360;yaw+=45){
    probe.transform.rotation=Quaternion.Euler(12,yaw,0);probe.Render();RenderTexture.active=rt;
    tex.ReadPixels(new Rect(0,0,320,180),0,0);tex.Apply();
    var pixels=tex.GetPixels32();int idx=Array.FindIndex(pixels,c=>c.r>235 && c.g<25 && c.b>235);
    int count=pixels.Count(c=>c.r>235 && c.g<25 && c.b>235);
    if(count>30){
     File.WriteAllBytes(Log("recepcao-rosa.png"),tex.EncodeToPNG());
     var renderers=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r.enabled).ToList();
     while(renderers.Count>1){
      int half=renderers.Count/2;var left=renderers.Take(half).ToArray();
      foreach(var renderer in left)renderer.enabled=false;
      probe.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,320,180),0,0);tex.Apply();
      var remaining=tex.GetPixels32().Count(c=>c.r>235 && c.g<25 && c.b>235);
      foreach(var renderer in left)renderer.enabled=true;
      renderers=remaining<count/2?left.ToList():renderers.Skip(half).ToList();
     }
     var culprit=renderers.First();
     File.WriteAllText(Log("material-rosa.txt"),culprit.name+" "+string.Join(", ",culprit.sharedMaterials.Select(mat=>mat?mat.name+"/"+mat.shader.name:"null")));

     var ray=probe.ViewportPointToRay(new Vector3((idx%320+.5f)/320,(idx/320+.5f)/180,0));
     string hits=string.Join(", ",Physics.RaycastAll(ray,100).OrderBy(h=>h.distance).Select(h=>h.collider.name));
     throw new Exception("Pixels magenta: "+count+" ângulo "+yaw+" objetos na direção: "+hits);
    }
   }
   var shots=new[]{
    ("porta",DemoVisivel.DoorCamera(),GameObject.Find("Dobradiça da sala").transform.TransformPoint(new Vector3(.56f,1.08f,0))),
    ("estante",new Vector3(27.45f,.95f,-2.6f),new Vector3(27,.58f,-4)),
    ("boliche",new Vector3(46.05f,1.15f,1.5f),new Vector3(45,.29f,3.2f)),
    ("cafe",new Vector3(26.20f,1.27f,2.62f),new Vector3(26,1.02f,3)),
    ("coworking",new Vector3(27.72f,1.64f,-.84f),new Vector3(27,.85f,-2)),
    ("pista",new Vector3(46.3f,2.05f,-5.5f),new Vector3(45,.3f,.8f))};
   foreach(var shot in shots){
    var frame=RenderTexture.GetTemporary(960,640,24);var output=new Texture2D(960,640,TextureFormat.RGB24,false);
    try{probe.targetTexture=frame;probe.transform.position=shot.Item2;probe.transform.LookAt(shot.Item3);probe.fieldOfView=shot.Item1=="porta"?82:46;probe.aspect=1.5f;probe.Render();RenderTexture.active=frame;
     output.ReadPixels(new Rect(0,0,960,640),0,0);output.Apply();File.WriteAllBytes(Log("acabamento-"+shot.Item1+".png"),output.EncodeToPNG());}
    finally{RenderTexture.active=previous;probe.targetTexture=rt;RenderTexture.ReleaseTemporary(frame);Object.DestroyImmediate(output);}
   }
   File.WriteAllText(Log("recepcao-visual.txt"),"Oito vistas da recepção renderizadas sem área magenta detectada.");
  }finally{RenderTexture.active=previous;probe.targetTexture=null;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);Object.DestroyImmediate(probe.gameObject);}
 }
 static void Press(string name){
  var station=GameObject.Find(name);if(!station)throw new Exception("Botão não encontrado ou inativo: "+name);
  var day=Object.FindFirstObjectByType<InternshipDay>();
  if(!day.UseStation(station))throw new Exception("Não é um botão de missão: "+name);
  day.SendMessage("Update");}
}
