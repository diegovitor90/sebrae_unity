using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using SebraeLab.Missions;

[InitializeOnLoad]
internal static class DesktopWalkCheck
{
    const string request="Logs/desktop-walk.request", log="Logs/desktop-walk.txt";
    static ManagerMission m;
    static DesktopPlayer pc;
    static CharacterController body;
    static DesktopWalkCheck(){EditorApplication.update+=Poll;}
    static void Poll(){
        if(SessionState.GetBool("WalkCheck.Restart",false)){
            var fresh=UnityEngine.Object.FindFirstObjectByType<ManagerMission>();
            if(fresh && fresh.day && fresh.State==ManagerMissionState.FindManager){
                Note("Recomeçar o dia: recepção e objetivo inicial");SessionState.SetBool("WalkCheck.Restart",false);
            }
        }
        if(!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
        if(!EditorApplication.isPlaying){if(!EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.EnterPlaymode();return;}
        m=UnityEngine.Object.FindFirstObjectByType<ManagerMission>();
        if(!m || !m.day || !m.TryGetComponent(out pc))return;
        File.Delete(request);File.WriteAllText(log,"Início "+DateTime.Now);
        body=m.playerRoot.GetComponent<CharacterController>();m.StartCoroutine(Run());
    }
    static void Note(string message){File.AppendAllText(log,"\n"+message);}
    static IEnumerator Run(){
        var stack=new Stack<IEnumerator>();stack.Push(Route());
        while(stack.Count>0){
            var step=stack.Peek();
            try{if(!step.MoveNext()){stack.Pop();continue;}}
            catch(Exception ex){Note("FALHA "+ex.Message);yield break;}
            if(step.Current is IEnumerator nested)stack.Push(nested);else yield return step.Current;
        }
    }
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static IEnumerator Walk(Vector3 target,float reach,GameObject aim=null,bool clearAim=false){
        Physics.SyncTransforms();
        var origin=body.transform.position;origin.y=0;
        const float grid=.24f;
        var queue=new Queue<Vector2Int>();var prev=new Dictionary<Vector2Int,Vector2Int>();
        var seen=new HashSet<Vector2Int>();var start=Vector2Int.zero;queue.Enqueue(start);seen.Add(start);
        Vector3 World(Vector2Int c)=>origin+new Vector3(c.x*grid,0,c.y*grid);
        Vector2Int end=start;bool found=false;
        while(queue.Count>0){
            var cell=queue.Dequeue();var p=World(cell);
            var eye=p+Vector3.up*1.62f;var delta=target-eye;
            bool visible=!clearAim || !Physics.Raycast(eye,delta,out var surface,delta.magnitude,~0,QueryTriggerInteraction.Ignore) || Vector3.Distance(surface.point,target)<.12f;
            if(visible && delta.magnitude<=reach && (!aim || (Physics.Raycast(eye,delta,out var h,reach,~0,QueryTriggerInteraction.Ignore) && (h.transform==aim.transform || h.transform.IsChildOf(aim.transform))))){end=cell;found=true;break;}
            foreach(var dir in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down}){
                var next=cell+dir;if(!seen.Add(next)||next.sqrMagnitude>60000)continue;var v=World(next);
                if(!Physics.Raycast(v+Vector3.up*.18f,Vector3.down,.5f,~0,QueryTriggerInteraction.Ignore))continue;
                if(Physics.OverlapCapsule(v+Vector3.up*.36f,v+Vector3.up*1.48f,.23f,~0,QueryTriggerInteraction.Ignore).Any(c=>c!=body && !c.transform.IsChildOf(body.transform)))continue;
                prev[next]=cell;queue.Enqueue(next);
            }
        }
        Require(found,"Sem caminho até "+target+" de "+origin+" "+(aim?aim.name:""));
        var path=new Stack<Vector2Int>();for(var c=end;c!=start;c=prev[c])path.Push(c);
        while(path.Count>0){
            var toward=World(path.Pop());
            for(int i=0;i<8;i++){
                var move=toward-body.transform.position;move.y=0;if(move.magnitude<.045f)break;
                body.transform.rotation=Quaternion.LookRotation(move);body.Move(Vector3.ClampMagnitude(move,.08f)+Vector3.down*.015f);
                pc.UsePointer(false,false);yield return null;
                if(i==7)Require((toward-body.transform.position).sqrMagnitude<.08f,"Colisão durante caminhada em "+toward);
            }
        }
        m.playerHead.LookAt(target);
    }
    static IEnumerator Click(string name){
        var g=GameObject.Find(name);Require(g,"Ausente "+name);
        yield return Walk(g.transform.position,2.3f,g);
        pc.UsePointer(true,false);yield return null;yield return null;
    }
    static IEnumerator Route(){
        yield return Walk(m.manager.position+Vector3.up*1.35f,1.7f);
        for(int i=0;i<=m.dialogue.Length;i++)m.Interact();
        Require(m.State==ManagerMissionState.FindRoom,"Diálogo");Note("1 OK gerente");
        var knob=GameObject.Find("Maçaneta");
        yield return Walk(knob.transform.position,1.22f,knob);
        m.Interact();Require(m.day.DoorOpen,"Porta fechada");
        yield return Walk(m.room.transform.position+Vector3.up*1.62f,.28f);
        m.EvaluatePlayer();Require(m.State==ManagerMissionState.Completed,"Sala");Note("2 OK sala");
        yield return Walk(new Vector3(22,1.62f,0),.3f);Note("Escritório até sala de aula a pé");
        yield return Click("LER O MURAL");yield return Click("ENTENDIDO");
        Require(!GameObject.Find("ENTENDIDO"),"Mural");Note("3 OK mural");
        yield return Click("Copo de café");pc.UsePointer(true,false);yield return null;
        Require(GameObject.FindObjectsByType<TextMesh>(FindObjectsSortMode.None).Any(t=>t.text.Contains("guarde os livros")),"Café");Note("4 OK café");
        foreach(var name in new[]{"Livro","Cadeira","Notebook"}){
            var items=GameObject.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Where(b=>b.name==name).OrderBy(b=>b.position.x).ToArray();
            var spots=GameObject.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.name=="Destino • "+name).OrderBy(r=>r.transform.position.x).ToArray();
            for(int i=0;i<items.Length;i++){
                var item=items[i];yield return Walk(item.position,2.2f,item.gameObject);pc.UsePointer(true,false);
                Require(!item.detectCollisions,"Não pegou "+name);
                var dest=spots[i].transform.position+Vector3.up*(item.transform.localScale.y*.5f+.03f);
                yield return Walk(dest,1.55f,null,true);pc.UsePointer(true,false);yield return null;yield return null;
                Require(!spots[i].gameObject.activeSelf,"Entrega "+name+" em "+item.position+" alvo "+dest);Note("Entrega "+name);
            }
        }
        yield return Walk(new Vector3(46,1.62f,0),.35f);Require(body.transform.position.x>40,"Sala final");Note("5 OK expediente completo");
        yield return Click("Bola de boliche");
        var ball=GameObject.Find("Bola de boliche").GetComponent<Rigidbody>();
        Require(!ball.detectCollisions,"Bola não foi pega");
        yield return Walk(new Vector3(45,1.7f,-2.8f),.26f);
        m.playerHead.LookAt(new Vector3(45,.26f,3.4f));
        pc.UsePointer(false,false);
        yield return new WaitForFixedUpdate();
        pc.UsePointer(false,true);
        Note("Lançamento "+ball.position+" velocidade "+ball.linearVelocity);
        yield return new WaitForSeconds(3);
        var pins=UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None)
            .Where(b=>b.name=="Pino").Count(b=>Vector3.Angle(b.transform.up,Vector3.up)>35);
        Require(pins>0,"Bola não derrubou pinos, posição "+ball.position);
        Note("Boliche: "+pins+" pinos derrubados");
        yield return Click("Arco arcade");
        var target=GameObject.Find("Alvo luminoso").transform;
        m.playerHead.LookAt(target.position);
        pc.UsePointer(false,false);
        yield return new WaitForFixedUpdate();
        pc.UsePointer(false,true);
        yield return new WaitForSeconds(2);
        Require(UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None).Any(t=>t.text.StartsWith("ARCO ARCADE • 1 ponto")),"Alvo não pontuou");
        Note("Arco: acerto confirmado, 1 ponto");
        yield return Click("REARMAR BOLICHE");
        Require(Vector3.Distance(ball.position,new Vector3(45,.85f,-3.8f))<.18f,"Rearme da bola");
        Require(UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Where(b=>b.name=="Pino").All(b=>Vector3.Angle(b.rotation*Vector3.up,Vector3.up)<10),"Rearme dos pinos");
        ball.position=new Vector3(46.24f,.44f,1);ball.linearVelocity=Vector3.zero;ball.WakeUp();
        yield return new WaitForSeconds(.8f);
        Require(ball.position.y<.18f && ball.position.y>-.16f,"Calha "+ball.position);Note("Calha e botão de rearme OK");
        yield return Click("REARMAR BOLICHE");
        yield return Click("VOLTAR À ALA");Require(body.transform.position.x<30,"Voltar à ala");
        yield return Click("VOLTAR À RECEPÇÃO");Require(body.transform.position.x<10,"Voltar à recepção");
        yield return Walk(new Vector3(22,1.62f,0),.3f);Note("Escritório até sala de aula a pé");
        yield return Click("SALA DE DESCOMPRESSÃO");Note("Retornos à ala e à recepção OK");
        SessionState.SetBool("WalkCheck.Restart",true);
        yield return Click("RECOMEÇAR O DIA");
    }
}

