using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using SebraeLab.Missions;
using Object = UnityEngine.Object;

namespace SebraeLab.Editor
{
    /// <summary>
    /// Teste em EditMode da porta da Sala 2. Monta uma dobradiça igual à do InternshipDay
    /// (sala girada -90°), simula um controle e confere seguir a mão, soltar, Toggle e limites.
    /// Resultado em Logs/hand-door-check.txt.
    /// </summary>
    public static class HandDoorCheck
    {
        const string Report = "Logs/hand-door-check.txt";

        [MenuItem("Sebrae Lab/Verificação/Porta da Sala 2 (mão)")]
        static void Menu() => Debug.Log("HandDoorCheck: " + (Run() == 0 ? "OK" : "FALHA") + ". Detalhes em " + Report);

        /// <summary>Para -executeMethod: fecha o Unity com 0 (ok) ou 1 (falha).</summary>
        public static void RunFromCommandLine() => EditorApplication.Exit(Run() == 0 ? 0 : 1);

        public static int Run()
        {
            var texto = new StringBuilder();
            int falhas = 0;
            void Check(bool ok, string mensagem)
            {
                if (!ok) falhas++;
                texto.AppendLine((ok ? "OK    " : "FALHA ") + mensagem);
            }

            var sala = new GameObject("Teste porta • sala");
            sala.transform.SetPositionAndRotation(new Vector3(16, 0, 0), Quaternion.Euler(0, -90, 0));
            var dobradica = new GameObject("Dobradiça de teste").transform;
            dobradica.SetParent(sala.transform, false);
            dobradica.localPosition = Vector3.left * .56f;
            var macaneta = GameObject.CreatePrimitive(PrimitiveType.Cube);
            macaneta.transform.SetParent(dobradica, false);
            macaneta.transform.localPosition = new Vector3(.99f, 1, .12f);
            var interativo = macaneta.AddComponent<XRSimpleInteractable>();
            var mao = new GameObject("Mão de teste").AddComponent<XRDirectInteractor>();

            void Pegar(IXRSelectInteractor quem) =>
                interativo.selectEntered.Invoke(new SelectEnterEventArgs { interactorObject = quem, interactableObject = interativo });
            void Soltar(IXRSelectInteractor quem) =>
                interativo.selectExited.Invoke(new SelectExitEventArgs { interactorObject = quem, interactableObject = interativo });
            void Girar(float graus) => mao.transform.RotateAround(dobradica.position, Vector3.up, graus);

            try
            {
                var porta = dobradica.gameObject.AddComponent<HandDoor>();
                porta.Bind(interativo);
                Check(porta.Angle == 0f && !porta.IsOpen, $"começa fechada ({porta.Angle:0.#}°)");

                mao.transform.position = macaneta.transform.position;
                Pegar(mao);
                Check(porta.Following, "pegar com controle passa a seguir a mão");
                Girar(60f); porta.Tick(.02f);
                Check(Mathf.Abs(porta.Angle - 60f) < 5f && porta.IsOpen, $"girou o controle 60°, porta em {porta.Angle:0.#}°");
                Check(Quaternion.Angle(dobradica.localRotation, Quaternion.Euler(0, porta.Angle, 0)) < .01f, "rotação local da dobradiça acompanha o ângulo");

                Soltar(mao);
                Girar(-60f); porta.Tick(.02f);
                Check(!porta.Following && Mathf.Abs(porta.Angle - 60f) < 5f, $"solta e fica onde parou ({porta.Angle:0.#}°)");

                porta.Toggle(); for (int i = 0; i < 60; i++) porta.Tick(.02f);
                Check(porta.Angle == 0f && Quaternion.Angle(dobradica.localRotation, Quaternion.identity) < .01f, $"Toggle a partir de 60° fecha ({porta.Angle:0.#}°)");

                porta.Toggle(); for (int i = 0; i < 60; i++) porta.Tick(.02f);
                Check(Mathf.Approximately(porta.Angle, HandDoor.MaxAngle), $"Toggle abre até {HandDoor.MaxAngle}° ({porta.Angle:0.#}°)");

                Pegar(null); for (int i = 0; i < 60; i++) porta.Tick(.02f);
                Check(porta.Angle == 0f && !porta.Following, $"seleção sem controle (modo PC) alterna e fecha ({porta.Angle:0.#}°)");

                mao.transform.position = macaneta.transform.position;
                Pegar(mao);
                Girar(150f); porta.Tick(.02f);
                Check(porta.Angle <= HandDoor.MaxAngle + .01f && porta.Angle >= HandDoor.MaxAngle - 1f, $"não passa de {HandDoor.MaxAngle}° ({porta.Angle:0.#}°)");
                Girar(-200f); porta.Tick(.02f);
                Check(porta.Angle >= 0f && porta.Angle < 1f, $"não passa de 0° ({porta.Angle:0.#}°)");
                Soltar(mao);
            }
            catch (Exception e)
            {
                falhas++;
                texto.AppendLine("FALHA exceção: " + e);
            }
            finally
            {
                Object.DestroyImmediate(sala);
                Object.DestroyImmediate(mao.gameObject);
            }

            texto.Insert(0, "Porta da Sala 2 (HandDoor) • " + DateTime.Now + "\n");
            texto.AppendLine(falhas == 0 ? "RESULTADO: OK" : $"RESULTADO: FALHA ({falhas})");
            Directory.CreateDirectory("Logs");
            File.WriteAllText(Report, texto.ToString());
            Debug.Log(texto.ToString());
            return falhas;
        }
    }
}
