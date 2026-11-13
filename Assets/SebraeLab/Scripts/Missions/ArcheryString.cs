using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace SebraeLab.Missions
{
    /// <summary>
    /// Corda do arco. Um pegador (XRGrabInteractable pequeno e cinemático) fica no ponto de repouso da corda e só
    /// pode ser agarrado com o arco na mão. A mão livre puxa o pegador para trás, a flecha e a corda visual
    /// acompanham, e soltar dispara se a puxada passar do mínimo (<see cref="ArcheryDraw"/>).
    /// </summary>
    public class ArcheryString : MonoBehaviour
    {
        [Header("Geometria (espaço local do Arco recurvo)")]
        public Vector3 repouso = new Vector3(0f, 0f, -.12f);
        public float alturaDasPontas = .77f;
        public float espessuraDaCorda = .005f;
        public float raioDoPegador = .07f;

        [Header("Vibração")]
        public float forcaDaVibracao = .5f;
        public float duracaoDaVibracao = .1f;

        public XRGrabInteractable Pegador { get; private set; }
        public Transform SegmentoSuperior { get; private set; }
        public Transform SegmentoInferior { get; private set; }
        public float Puxada { get; private set; }
        public bool PegadorAgarrado { get; private set; }
        /// <summary>Ponto de repouso da corda no mundo.</summary>
        public Vector3 PosicaoDeRepouso => visual.TransformPoint(repouso);

        ArcheryRange alcance;
        Transform visual;
        Rigidbody corpo;
        Collider colisor;
        Vector3 flechaEmRepouso;

        public void Configurar(ArcheryRange dono)
        {
            alcance = dono;
            visual = dono.Visual;
            flechaEmRepouso = dono.FlechaCarregada.transform.localPosition;

            // A corda existente vira o segmento de cima; uma cópia dela, o de baixo.
            SegmentoSuperior = visual.Find("Corda do arco");
            SegmentoInferior = Instantiate(SegmentoSuperior.gameObject, visual).transform;
            SegmentoInferior.name = SegmentoSuperior.name;

            var objeto = new GameObject("Pegador da corda");
            objeto.transform.SetParent(dono.Area, false);
            corpo = objeto.AddComponent<Rigidbody>();
            corpo.isKinematic = true;
            corpo.useGravity = false;
            var esfera = objeto.AddComponent<SphereCollider>();
            esfera.radius = raioDoPegador;
            esfera.isTrigger = true; // a flecha varre só colisores sólidos
            esfera.enabled = false;  // ligado só enquanto o arco está na mão (ver Update)
            colisor = esfera;
            Pegador = objeto.AddComponent<XRGrabInteractable>();
            Pegador.movementType = XRBaseInteractable.MovementType.Instantaneous;
            Pegador.trackRotation = false;
            Pegador.throwOnDetach = false;
            Pegador.selectEntered.AddListener(AoAgarrar);
            Pegador.selectExited.AddListener(AoSoltar);
            dono.ArcoSolto += AoArcoSolto;
            Atualizar();
        }

        // O XRI move o pegador na própria atualização (e de novo antes de renderizar); a restrição roda depois dele.
        void OnEnable() => Application.onBeforeRender += Atualizar;
        void OnDisable() => Application.onBeforeRender -= Atualizar;
        void LateUpdate() => Atualizar();

        void Update()
        {
            if (colisor) colisor.enabled = alcance.ArcoNaMao;
        }

        float Medir() => ArcheryDraw.Limitar(Vector3.Dot(Pegador.transform.position - PosicaoDeRepouso, -visual.forward));

        void Atualizar()
        {
            if (!Pegador || !visual) return;
            Puxada = PegadorAgarrado ? Medir() : 0f;
            var ponto = PosicaoDeRepouso - visual.forward * Puxada;
            Pegador.transform.SetPositionAndRotation(ponto, visual.rotation); // descarta o desvio lateral
            corpo.position = ponto;
            var noque = repouso + Vector3.back * Puxada;
            Segmento(SegmentoSuperior, new Vector3(0f, alturaDasPontas, repouso.z), noque);
            Segmento(SegmentoInferior, new Vector3(0f, -alturaDasPontas, repouso.z), noque);
            var flecha = alcance.FlechaCarregada;
            if (flecha) flecha.transform.localPosition = flechaEmRepouso + Vector3.back * Puxada;
        }

        void Segmento(Transform segmento, Vector3 de, Vector3 ate)
        {
            var direcao = ate - de;
            segmento.localPosition = (de + ate) / 2f;
            segmento.localRotation = Quaternion.FromToRotation(Vector3.up, direcao);
            segmento.localScale = new Vector3(espessuraDaCorda, direcao.magnitude, espessuraDaCorda);
        }

        void AoAgarrar(SelectEnterEventArgs args)
        {
            if (alcance.ArcoNaMao) PegadorAgarrado = true;
        }

        void AoSoltar(SelectExitEventArgs args)
        {
            if (!PegadorAgarrado) return; // cancelado porque o arco foi solto: nenhum tiro
            Atualizar(); // fixa a puxada e a pose da flecha no instante da soltura
            float puxada = Puxada;
            PegadorAgarrado = false;
            if (alcance.ArcoNaMao && alcance.FlechaCarregada && ArcheryDraw.Dispara(puxada))
            {
                Vibrar(args != null ? args.interactorObject : null);
                Vibrar(alcance.Arco.firstInteractorSelecting);
                alcance.Disparar(ArcheryDraw.Velocidade(puxada));
            }
            Atualizar(); // devolve o pegador e a corda ao repouso
        }

        void AoArcoSolto()
        {
            if (!PegadorAgarrado) return;
            PegadorAgarrado = false;
            if (Pegador.isSelected && Pegador.interactionManager != null)
                Pegador.interactionManager.CancelInteractableSelection((IXRSelectInteractable)Pegador);
            Atualizar();
        }

        void Vibrar(IXRInteractor interactor)
        {
            if (interactor == null) return;
            var vibrador = interactor.transform.GetComponentInParent<HapticImpulsePlayer>();
            if (vibrador) vibrador.SendHapticImpulse(forcaDaVibracao, duracaoDaVibracao);
        }
    }
}
