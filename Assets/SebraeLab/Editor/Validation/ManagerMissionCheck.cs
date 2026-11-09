using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using SebraeLab.Missions;

namespace SebraeLab.Editor
{
    [InitializeOnLoad]
    public static class ManagerMissionCheck
    {
        const string Pending = "SebraeLab.ManagerMissionCheck.Pending";
        static double entered;
        [Serializable] class Result { public bool passed; public string[] checks; public string error; }
        static ManagerMissionCheck()
        {
            EditorApplication.update += WaitRequest;
            EditorApplication.playModeStateChanged += OnPlayState;
        }
        static void WaitRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists("Logs/run-manager-check.request") || !File.Exists("Logs/manager-mission-route.txt")) return;
            File.Delete("Logs/run-manager-check.request"); Run();
        }
        [MenuItem("Sebrae Lab/Missao 01/4 - Verificar fluxo da missao")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Pare o Play antes da verificação.");
            EditorSceneManager.OpenScene(ManagerMissionSetup.MissionScene);
            SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
        }
        static void OnPlayState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= CheckWhenReady;
                SessionState.SetBool(Pending, false);
            }
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
            {
                entered = EditorApplication.timeSinceStartup;
                EditorApplication.update += CheckWhenReady;
            }
        }
        static void CheckWhenReady()
        {
            if (EditorApplication.timeSinceStartup - entered < 2) return;
            EditorApplication.update -= CheckWhenReady;
            var checks = new List<string>(); var mission = UnityEngine.Object.FindFirstObjectByType<ManagerMission>();
            var fakeHead = new GameObject("Verification head"); Transform original = mission ? mission.playerHead : null;
            var result = new Result();
            try
            {
                Action<bool, string> require = (ok, message) => { if (!ok) throw new Exception(message); checks.Add(message); };
                require(mission != null, "Mission component loaded");
                mission.playerHead = fakeHead.transform;
                fakeHead.transform.position = mission.manager.position + Vector3.right * 8 + Vector3.up * 1.65f;
                mission.RestartMission();
                require(!mission.TryBeginConversation() && mission.State == ManagerMissionState.FindManager, "Cannot start conversation at a distance");
                fakeHead.transform.position = mission.room.transform.position + Vector3.up * 1.65f;
                mission.EvaluatePlayer();
                require(mission.State == ManagerMissionState.FindManager && mission.CompletionCount == 0, "Room entry before accepting does not complete mission");
                fakeHead.transform.position = mission.manager.position + Vector3.right * 1.2f + Vector3.up * 1.65f;
                Physics.SyncTransforms();
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = mission.manager.position + new Vector3(.6f, 1.4f, 0); wall.transform.localScale = new Vector3(.1f, 2.8f, 2);
                Physics.SyncTransforms(); require(!mission.TryBeginConversation(), "Cannot talk through a wall");
                UnityEngine.Object.DestroyImmediate(wall); Physics.SyncTransforms();
                mission.conversationButton.onClick.Invoke();
                require(mission.State == ManagerMissionState.Dialogue && mission.DialogueIndex == 0, "Conversation button starts dialogue");
                fakeHead.transform.position += Vector3.right * 8;
                require(!mission.TryAdvanceDialogue() && mission.DialogueIndex == 0, "Leaving manager blocks dialogue advancement");
                fakeHead.transform.position = mission.manager.position + Vector3.right * 1.2f + Vector3.up * 1.65f;
                for (int i = 0; i < mission.dialogue.Length; i++) mission.conversationButton.onClick.Invoke();
                require(mission.State == ManagerMissionState.FindRoom && mission.routeMarkers.activeSelf && mission.roomMarker.activeSelf, "Accepting dialogue enables room objective and route");
                require(!mission.TryBeginConversation(), "Conversation cannot reset an accepted mission");
                mission.EvaluatePlayer(); require(mission.State == ManagerMissionState.FindRoom, "Mission remains active while player is outside room");
                fakeHead.transform.position = mission.room.transform.position + Vector3.up * 1.65f;
                mission.EvaluatePlayer(); mission.EvaluatePlayer();
                require(mission.State == ManagerMissionState.Completed && mission.CompletionCount == 1 && mission.completionPanel.activeSelf && !mission.routeMarkers.activeSelf, "Entering room completes exactly once and shows success");
                mission.completionPanel.GetComponentInChildren<Button>(true).onClick.Invoke();
                require(mission.State == ManagerMissionState.FindManager && mission.CompletionCount == 0 && !mission.routeMarkers.activeSelf && !mission.completionPanel.activeSelf, "Restart button resets progress and markers");
                result.passed = true;
            }
            catch (Exception e) { result.error = e.ToString(); Debug.LogError("SEBRAE_MANAGER_CHECK_FAILED " + e.Message); }
            finally
            {
                if (mission) { mission.playerHead = original; mission.RestartMission(); }
                UnityEngine.Object.DestroyImmediate(fakeHead);
                result.checks = checks.ToArray(); Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/manager-mission-check.json", JsonUtility.ToJson(result, true));
                if (result.passed) Debug.Log("SEBRAE_MANAGER_CHECK_OK");
                SessionState.SetBool(Pending, false); EditorApplication.isPlaying = false;
            }
        }
    }
}
