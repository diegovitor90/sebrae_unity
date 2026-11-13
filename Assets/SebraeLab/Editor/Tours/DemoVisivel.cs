using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using SebraeLab.Missions;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class DemoVisivel {
 static Camera view;
 static Text title;
 static Transform eyes;
 static string Log => Path.Combine(Application.dataPath,"../Logs/demo-visivel.txt");
 static DemoVisivel(){EditorApplication.update+=StartWhenReady;}
 static void StartWhenReady(){
  string flag=Log+".request";
  if(!EditorApplication.isPlaying || !File.Exists(flag))return;
  var m=Object.FindFirstObjectByType<ManagerMission>();if(!m || !m.day)return;
  File.Delete(flag);File.WriteAllText(Log,"Iniciando");m.StartCoroutine(Watch(Run(m)));
 }
 static IEnumerator Watch(IEnumerator routine){
  while(true){object wait;try{if(!routine.MoveNext())break;wait=routine.Current;}catch(Exception ex){File.AppendAllText(Log,"\nERRO "+ex);throw;}yield return wait;}
 }
  public static Vector3 DoorCamera(){
    var center=GameObject.Find("Dobradiça da sala").transform.TransformPoint(new Vector3(.56f,1.08f,0));
    var best=center+Vector3.forward;float bestScore=-1;
    foreach(float side in new[]{1f,-1f})foreach(float dx in new[]{0f,.4f,-.4f,.8f,-.8f}){
      var dir=new Vector3(side,.08f,dx).normalized;
      var start=center+dir*.2f;float depth=2.8f;
      foreach(var hit in Physics.RaycastAll(start,dir,2.8f,~0,QueryTriggerInteraction.Ignore))depth=Mathf.Min(depth,hit.distance-.18f);
      float score=depth-.22f*Mathf.Abs(dx)+(side>0?.08f:0);
      if(score>bestScore){bestScore=score;best=start+dir*Mathf.Max(.15f,depth);}
    }
    return best;
 }
 static float kindOffset(string name)=>name=="Livro"?2:-2;
 static void Show(string text, Vector3 target, Vector3 offset){
  view.fieldOfView=text.StartsWith("2/5")?82:58;view.transform.position=target+offset;view.transform.LookAt(target);eyes.position=view.transform.position;
  title.text=text;File.AppendAllText(Log,"\n"+text);
 }
 static IEnumerator Run(ManagerMission m){
  var camera=m.playerHead.GetComponent<Camera>();
  view=new GameObject("Câmera da demonstração").AddComponent<Camera>();view.CopyFrom(camera);view.stereoTargetEye=StereoTargetEyeMask.None;view.targetTexture=null;camera.enabled=false;
  var canvas=new GameObject("Legenda",typeof(Canvas)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;
  var panel=new GameObject("Faixa",typeof(Image)).GetComponent<Image>();panel.transform.SetParent(canvas.transform,false);panel.color=new Color(.02f,.08f,.13f,.95f);
  var rect=panel.rectTransform;rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.sizeDelta=new Vector2(0,84);
  title=new GameObject("Etapa",typeof(Text)).GetComponent<Text>();title.transform.SetParent(panel.transform,false);title.rectTransform.anchorMin=Vector2.zero;title.rectTransform.anchorMax=Vector2.one;title.rectTransform.offsetMin=new Vector2(12,4);title.rectTransform.offsetMax=new Vector2(-12,-4);title.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");title.fontSize=24;title.alignment=TextAnchor.MiddleCenter;title.color=Color.white;
  var pause=new WaitForSecondsRealtime(5);
    var xr=Object.FindFirstObjectByType<XRInteractionManager>();
    var controller=new GameObject("Test controller");
    controller.AddComponent<SphereCollider>().isTrigger=true;
    IXRSelectInteractor hand=controller.AddComponent<XRDirectInteractor>();
    var head=new GameObject("Test head");m.playerHead=head.transform;eyes=head.transform;head.transform.position=m.manager.position+new Vector3(1.2f,1.65f,0);Physics.SyncTransforms();
    Show("1/5   Conversa com o gerente", m.manager.position+Vector3.up*1.35f, Vector3.right*1.8f);yield return pause;
    if(!m.TryBeginConversation())throw new Exception("Conversa não iniciou");
    foreach(var line in m.dialogue){title.text="1/5   "+line;yield return new WaitForSecondsRealtime(3);m.TryAdvanceDialogue();}
    if(m.State!=ManagerMissionState.FindRoom)throw new Exception("Missão da sala não recebida");
    var knob=GameObject.Find("Maçaneta").GetComponent<XRSimpleInteractable>();
    var pivot=GameObject.Find("Dobradiça da sala").transform.position;
    var doorCenter=GameObject.Find("Dobradiça da sala").transform.TransformPoint(new Vector3(.56f,1.08f,0));Show("2/5   Abrir a porta da sala",doorCenter,DoorCamera()-doorCenter);yield return pause;
    controller.transform.position=knob.transform.position;
    xr.SelectEnter(hand,knob);
    controller.transform.RotateAround(pivot,Vector3.up,75);
    m.day.SendMessage("Update");
    if(!m.day.DoorOpen)throw new Exception("A maçaneta não abriu a porta.");
    xr.SelectExit(hand,knob);yield return pause;
    head.transform.position=m.room.transform.position+Vector3.up*1.65f;m.EvaluatePlayer();
    if(m.State!=ManagerMissionState.Completed)throw new Exception("Sala não liberada");
    Press("ENTRAR NA ALA DE ATIVIDADES"); Show("3/5   Mural do Conhecimento",new Vector3(22,1.5f,2),new Vector3(1,0,-3));Press("LER O MURAL");yield return pause;Press("ENTENDIDO");
    foreach(var item in new[]{"Copo de café"}){
     var grab=GameObject.Find(item).GetComponent<XRGrabInteractable>();
     Show("4/5   Pegar o café na copa",grab.transform.position,new Vector3(.4f,.35f,-1.2f));yield return pause;
     controller.transform.position=grab.transform.position;xr.SelectEnter(hand,grab);
     if(!grab.isSelected)throw new Exception("Falha ao pegar "+item);
     xr.SelectExit(hand,grab);
    }
    foreach(var name in new[]{"Livro","Cadeira","Notebook"}){
     var items=Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None).Where(g=>g.name==name).OrderBy(g=>g.transform.position.x).ToArray();
     var spots=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(g=>g.name=="Destino • "+name).OrderBy(g=>g.transform.position.x).ToArray();
     if(items.Length!=2 || spots.Length!=2)throw new Exception("Itens: "+name);
     for(int i=0;i<2;i++){Show("5/5   Organizar o coworking • "+name+" "+(i+1)+"/2",spots[i].transform.position,new Vector3(.6f,1.2f,kindOffset(name)));yield return new WaitForSecondsRealtime(2);items[i].transform.position=spots[i].transform.position+Vector3.up*items[i].transform.localScale.y/2;items[i].selectExited.Invoke(null);m.day.SendMessage("Update");yield return new WaitForSecondsRealtime(1);}
    }
    Press("SALA DE DESCOMPRESSÃO"); Show("5/5   Expediente concluído • sala de descompressão liberada",new Vector3(45,.5f,2),new Vector3(2,2,-5));
 File.AppendAllText(Log,"\nPASS");
 title.text="Tour concluído • em instantes você poderá andar com WASD e olhar com o mouse";
 yield return new WaitForSecondsRealtime(4);
 m.playerHead=camera.transform;
 var root=m.playerRoot;
 root.position+=new Vector3(46-camera.transform.position.x,-root.position.y,-2-camera.transform.position.z);
 camera.enabled=true;
 view.enabled=false;
 Object.Destroy(view.gameObject);
 Object.Destroy(canvas.gameObject);
 Object.Destroy(head);
 Object.Destroy(controller);
 }
 static void Press(string name){
  var station=GameObject.Find(name);if(!station)throw new Exception("Botão não encontrado ou inativo: "+name);
  var day=Object.FindFirstObjectByType<InternshipDay>();
  if(!day.UseStation(station))throw new Exception("Não é um botão de missão: "+name);
  day.SendMessage("Update");}
}