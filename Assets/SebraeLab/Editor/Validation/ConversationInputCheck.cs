using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using SebraeLab.Missions;

[InitializeOnLoad]
internal static class ConversationInputCheck
{
    static readonly string Request=Path.Combine(Application.dataPath,"../Logs/keyboard-dialogue.request");
    static double started, next;
    static int phase;
    static ConversationInputCheck(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(!EditorApplication.isPlaying || Time.time<2 || EditorApplication.timeSinceStartup<next)return;
        if(phase==0 && !File.Exists(Request))return;
        var m=Object.FindFirstObjectByType<ManagerMission>();if(!m)return;
        next=EditorApplication.timeSinceStartup+.18;
        if(phase==0){
            File.Delete(Request);started=EditorApplication.timeSinceStartup;
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.W));phase=1;return;
        }
        if(m.State==ManagerMissionState.FindRoom || EditorApplication.timeSinceStartup-started>8){
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
            File.WriteAllText(Request+".result",m.State+" route="+m.routeMarkers.activeSelf+" distance="+Vector3.Distance(m.playerHead.position,m.manager.position));
            phase=0;return;
        }
        if(phase==1 && !m.CanConverse())return;
        InputSystem.QueueStateEvent(Keyboard.current,phase==2?new KeyboardState(Key.F):new KeyboardState());
        phase=phase==2?3:2;
    }
}
