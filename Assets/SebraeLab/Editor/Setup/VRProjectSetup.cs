using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit.UI;
namespace SebraeLab.Editor {
 [InitializeOnLoad]
 public static class VRProjectSetup {
  const string Root="Assets/SebraeLab";
  const string ScenePath=Root+"/Scenes/VR_Test.unity";
  const string Samples="Assets/Samples/XR Interaction Toolkit/3.0.11/";
  static VRProjectSetup(){ EditorApplication.delayCall += AutoSetup; }
  static void AutoSetup(){
   if(EditorApplication.isCompiling || EditorApplication.isUpdating){EditorApplication.delayCall+=AutoSetup;return;}
   if(!File.Exists(ScenePath) || !File.Exists("vr-scene-v2.txt")) Setup();
  }
  [MenuItem("Sebrae Lab/1 - Preparar base VR")]
  public static void Setup(){
   Directory.CreateDirectory(Root+"/Scenes"); Directory.CreateDirectory(Root+"/Materials"); Directory.CreateDirectory(Root+"/XR"); AssetDatabase.Refresh();
   PlayerSettings.runInBackground=true;
   PlayerSettings.companyName="SebraeLab"; PlayerSettings.productName="Missao Estagio VR";
   PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"com.sebraelab.missaoestagio");
   PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
   PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
   PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel32;
   PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevel35;
   PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.Vulkan});
   PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
   var ps=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
   var input=ps.FindProperty("activeInputHandler"); if(input!=null){input.intValue=1;ps.ApplyModifiedPropertiesWithoutUndo();}
   ConfigureToolchain();
   ConfigureXR();
   if(!File.Exists(ScenePath)) CreateScene();
   RepairScene();
   AssetDatabase.SaveAssets();
   File.WriteAllText("vr-setup-result.txt","VR setup completed: "+DateTime.Now+"\nScene: "+ScenePath+"\nAndroid module available: "+UnityEditor.BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android));
   Debug.Log("SEBRAE_VR_SETUP_OK");
  }
  static void ConfigureToolchain(){
   var tools=Path.GetFullPath("../../work/android-toolchain");
   var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.Android.AndroidExternalToolsSettings")).FirstOrDefault(t=>t!=null);
   if(type==null || !File.Exists(Path.Combine(tools,"SDK/platform-tools/adb.exe")))return;
   foreach(var entry in new[]{new[]{"sdkRootPath","SDK"},new[]{"ndkRootPath","NDK"},new[]{"jdkRootPath","OpenJDK"}})
    type.GetProperty(entry[0])?.SetValue(null,Path.Combine(tools,entry[1]));
  }
  static void ConfigureXR(){
   var settings=AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(Root+"/XR/XRGeneralSettings.asset");
   if(settings==null){settings=ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();AssetDatabase.CreateAsset(settings,Root+"/XR/XRGeneralSettings.asset");}
   EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey,settings,true);
   foreach(var group in new[]{BuildTargetGroup.Android,BuildTargetGroup.Standalone}){
    if(!settings.HasSettingsForBuildTarget(group))settings.CreateDefaultSettingsForBuildTarget(group);
    if(!settings.HasManagerSettingsForBuildTarget(group))settings.CreateDefaultManagerSettingsForBuildTarget(group);
    var general=settings.SettingsForBuildTarget(group); general.InitManagerOnStart=true;
    if(!XRPackageMetadataStore.AssignLoader(general.Manager,"UnityEngine.XR.OpenXR.OpenXRLoader",group))throw new Exception("OpenXR loader assignment failed: "+group);
    FeatureHelpers.RefreshFeatures(group);
    var xr=OpenXRSettings.GetSettingsForBuildTargetGroup(group);
    if(xr==null){Debug.LogWarning("SEBRAE_VR: OpenXR Android pendente da instalacao do modulo Android. Execute Preparar base VR apos instalar.");continue;}
    foreach(var feature in xr.GetFeatures()){
     var name=feature.GetType().Name;
     if(name=="OculusTouchControllerProfile" || name=="MetaQuestTouchPlusControllerProfile" || (group==BuildTargetGroup.Android && name=="MetaQuestFeature"))feature.enabled=true;
    }
    EditorUtility.SetDirty(xr); EditorUtility.SetDirty(general); EditorUtility.SetDirty(general.Manager);
   }
   EditorUtility.SetDirty(settings);
  }
  static Material Mat(string name,Color color){
   var path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(!m){m=new Material(Shader.Find("Standard"));m.color=color;AssetDatabase.CreateAsset(m,path);} return m;
  }
  static GameObject Cube(string name,Vector3 pos,Vector3 scale,Material mat){
   var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.position=pos;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=mat;return g;
  }
  static Text Label(Transform parent,string name,string text,Vector2 position,Vector2 size,int fontSize){
   var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
   var t=go.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=text;t.fontSize=fontSize;t.alignment=TextAnchor.MiddleCenter;t.color=Color.white;t.raycastTarget=false;
   var rt=go.GetComponent<RectTransform>();rt.anchoredPosition=position;rt.sizeDelta=size;return t;
  }
  static void CreateScene(){
   var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   RenderSettings.ambientLight=new Color(.55f,.6f,.7f);
   var light=new GameObject("Luz principal",typeof(Light));light.GetComponent<Light>().type=LightType.Directional;light.transform.rotation=Quaternion.Euler(45,-30,0);
   var blue=Mat("Azul",new Color(.05f,.32f,.65f));var dark=Mat("Piso",new Color(.16f,.2f,.27f));var orange=Mat("Objeto",new Color(1,.5f,.12f));
   var floor=Cube("Piso - area de teletransporte",new Vector3(0,-.1f,0),new Vector3(10,.2f,10),dark);
   floor.AddComponent<TeleportationArea>().interactionLayers = (InteractionLayerMask)(1 << 31);
   Cube("Parede fundo",new Vector3(0,1.5f,5),new Vector3(10,3,.15f),blue);
   Cube("Parede esquerda",new Vector3(-5,1.5f,0),new Vector3(.15f,3,10),blue);
   Cube("Parede direita",new Vector3(5,1.5f,0),new Vector3(.15f,3,10),blue);
   new GameObject("XR Interaction Manager",typeof(XRInteractionManager));
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Samples+"Starter Assets/Prefabs/XR Origin (XR Rig).prefab");
   if(!prefab)throw new Exception("XR Origin prefab not found");
   var rig=(GameObject)PrefabUtility.InstantiatePrefab(prefab);rig.name="Jogador VR - XR Origin";rig.transform.position=new Vector3(0,0,-2);
   foreach(var control in rig.GetComponentsInChildren<ControllerInputActionManager>(true)){control.smoothMotionEnabled=false;control.smoothTurnEnabled=false;}
   Cube("Mesa",new Vector3(0,.75f,.5f),new Vector3(1.8f,.12f,.8f),blue);
   var obj=Cube("Cubo - pegar e soltar",new Vector3(0,1.05f,.5f),Vector3.one*.2f,orange);obj.AddComponent<Rigidbody>();obj.AddComponent<XRGrabInteractable>();
   var indicator=Cube("Indicador do botao",new Vector3(1.2f,1.2f,1),Vector3.one*.2f,orange);
   var panel=new GameObject("Painel de teste",typeof(RectTransform),typeof(Canvas),typeof(TrackedDeviceGraphicRaycaster));
   panel.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;panel.transform.position=new Vector3(0,1.8f,2);panel.transform.localScale=Vector3.one*.002f;panel.GetComponent<RectTransform>().sizeDelta=new Vector2(1000,600);
   Label(panel.transform,"Titulo","SEBRAE LAB | TESTE VR",new Vector2(0,200),new Vector2(950,80),46);
   Label(panel.transform,"Instrucoes","Teletransporte pelo piso e gire em passos.\nUse o grip para pegar o cubo.\nAponte para o botao e aperte o gatilho.",new Vector2(0,50),new Vector2(950,180),32);
   var status=Label(panel.transform,"Status","Aguardando clique",new Vector2(0,-200),new Vector2(950,60),32);
   var b=new GameObject("Botao de teste",typeof(RectTransform),typeof(Image),typeof(Button),typeof(VRTestButton));b.transform.SetParent(panel.transform,false);
   var br=b.GetComponent<RectTransform>();br.anchoredPosition=new Vector2(0,-100);br.sizeDelta=new Vector2(420,80);b.GetComponent<Image>().color=new Color(.05f,.45f,.8f);
   Label(b.transform,"Texto","TESTAR INTERACAO",Vector2.zero,new Vector2(410,70),30);
   var test=b.GetComponent<VRTestButton>();test.status=status;test.indicator=indicator.GetComponent<Renderer>();UnityEditor.Events.UnityEventTools.AddPersistentListener(b.GetComponent<Button>().onClick,test.Press);
   new GameObject("EventSystem XR",typeof(EventSystem),typeof(XRUIInputModule));
   var simulator=AssetDatabase.LoadAssetAtPath<GameObject>(Samples+"XR Device Simulator/XR Device Simulator.prefab");
   if(simulator){var sim=(GameObject)PrefabUtility.InstantiatePrefab(simulator);sim.tag="EditorOnly";sim.name="Simulador PC - excluido da build";sim.SetActive(false);}
   EditorSceneManager.SaveScene(scene,ScenePath);EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
   if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(new Vector3(0,1,0),Quaternion.Euler(25,0,0),6);
  }

  static void RepairScene(){
   if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=ScenePath) EditorSceneManager.OpenScene(ScenePath);
   foreach(var area in UnityEngine.Object.FindObjectsByType<TeleportationArea>(FindObjectsSortMode.None)) area.interactionLayers=(InteractionLayerMask)(1<<31);
   var layers=AssetDatabase.LoadMainAssetAtPath("Assets/XRI/Settings/Resources/InteractionLayerSettings.asset");
   if(layers){var so=new SerializedObject(layers);var names=so.FindProperty("m_LayerNames");names.arraySize=32;names.GetArrayElementAtIndex(31).stringValue="Teleport";so.ApplyModifiedPropertiesWithoutUndo();}
   EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
   File.WriteAllText("vr-scene-v2.txt","Teleport layer configured");
  }
  [MenuItem("Sebrae Lab/4 - Alternar simulador PC")]
  public static void ToggleSimulator(){
   var sim=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(t=>t.name=="Simulador PC - excluido da build");
   if(sim){sim.gameObject.SetActive(!sim.gameObject.activeSelf);EditorSceneManager.MarkSceneDirty(sim.gameObject.scene);}
  }
  [MenuItem("Sebrae Lab/2 - Abrir cena de teste")]
  public static void OpenScene(){EditorSceneManager.OpenScene(ScenePath);}
  [MenuItem("Sebrae Lab/3 - Gerar APK Quest")]
  public static void BuildQuest(){
   Setup();
   if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android))throw new Exception("Instale Android Build Support, SDK/NDK e OpenJDK pelo Unity Hub para Unity 6000.0.83f1.");
   Directory.CreateDirectory("Builds/Quest");
   var report=BuildPipeline.BuildPlayer(new[]{ScenePath},"Builds/Quest/SebraeLabVR.apk",BuildTarget.Android,BuildOptions.Development);
   if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Quest build failed: "+report.summary.result);
  }
 }
}
