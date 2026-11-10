using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using SebraeLab.Missions;

[InitializeOnLoad]
internal static class MovementCheck
{
    static int phase;
    static float time;
    static Vector3 position;
    static Quaternion rotation;
    static MovementCheck(){EditorApplication.update += Check;}
    static void Check(){
        if(!EditorApplication.isPlaying || Time.time < 2 || Time.unscaledTime < time)return;
        string file=Path.Combine(Application.dataPath,"../Logs/movement.request");
        if(phase==0 && !File.Exists(file))return;
        var mission=Object.FindFirstObjectByType<ManagerMission>();
        if(!mission || !mission.playerHead.GetComponent<Camera>() || !mission.playerHead.GetComponent<Camera>().enabled)return;
        var camera=mission.playerHead;
        if(phase==0){
            File.Delete(file);position=camera.position;
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.W));
            phase=1;time=Time.unscaledTime+.7f;return;
        }
        if(phase==1){
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
            File.WriteAllText(file+".result","W: "+Vector3.Distance(camera.position,position)+" m");
            rotation=camera.rotation;
            InputSystem.QueueDeltaStateEvent(Mouse.current.delta,new Vector2(50,0));
            phase=2;time=Time.unscaledTime+.25f;return;
        }
        File.AppendAllText(file+".result","; mouse: "+Quaternion.Angle(rotation,camera.rotation)+" graus");
        phase=0;
    }
}
