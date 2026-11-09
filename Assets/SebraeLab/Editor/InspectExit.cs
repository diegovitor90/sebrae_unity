using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad] static class InspectExit {
 static InspectExit(){EditorApplication.update+=Inspect;}
 static void Inspect(){if(!Application.isPlaying || !File.Exists("Logs/exit.request"))return;File.Delete("Logs/exit.request");
 var lines=new System.Collections.Generic.List<string>();
 for(float z=6.5f;z<=12;z+=.25f){var p=new Vector3(5.7f,0,z);
 lines.Add(z+" "+string.Join(",",Physics.OverlapCapsule(p+Vector3.up*.36f,p+Vector3.up*1.48f,.23f,~0,QueryTriggerInteraction.Ignore).Select(c=>c.name+" ("+c.GetType().Name+")"))+" floor="+Physics.Raycast(p+Vector3.up*.18f,Vector3.down,.5f));}
 File.WriteAllLines("Logs/exit.txt",lines);}
}
