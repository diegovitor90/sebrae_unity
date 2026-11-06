using UnityEngine;
using UnityEngine.UI;
namespace SebraeLab {
 public class VRTestButton : MonoBehaviour {
  public Text status;
  public Renderer indicator;
  int count;
  public void Press() {
   count++;
   status.text = "Botao acionado: " + count;
   indicator.material.color = new Color(0.15f,0.85f,0.45f);
   Debug.Log("SEBRAE_VR_BUTTON_OK " + count);
  }
 }
}
