using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SebraeLab.Missions
{
    /// <summary>
    /// Regras do arco da sala de descompressão: colisor do alvo, placar (<see cref="ArcheryScore"/>), disparo,
    /// disparo direto do modo PC e recarga. O InternshipDay monta as peças e chama <see cref="Configurar"/>.
    /// </summary>
    public class ArcheryRange : MonoBehaviour
    {
        [Header("Tempos (s)")]
        public float recarga = .35f;
        public float tempoCravada = 4f;
        public float tempoMaximoNoAr = 5f;

        [Header("Modo PC")]
        public float velocidadeDoPc = ArcheryDraw.VelocidadeMaxima;
        public float avancoDaFlechaNoPc = .38f;

        public ArcheryScore Placar { get; } = new ArcheryScore();
        public TextMesh Texto { get; private set; }
        public XRGrabInteractable Arco { get; private set; }
        public Transform Visual { get; private set; }
        public Transform Alvo { get; private set; }
        public Transform Area { get; private set; }
        public GameObject FlechaCarregada { get; private set; }
        public ArcheryArrow UltimaFlecha { get; private set; }
        public int Disparos { get; private set; }
        public ArcheryString Corda { get; private set; }
        /// <summary>Verdadeiro enquanto o arco está seguro, por um controle de VR ou pelo modo PC.</summary>
        public bool ArcoNaMao { get; private set; }
        /// <summary>Verdadeiro enquanto um controle de VR segura o arco. O modo PC pega o arco sem interactor.</summary>
        public bool ArcoComControle { get; private set; }

        /// <summary>Uma flecha nova foi montada sobre a corda.</summary>
        public event Action FlechaMontada;
        /// <summary>O arco deixou de ser segurado.</summary>
        public event Action ArcoSolto;

        Func<GameObject> montarFlecha;
        float recargaEm;

        public void Configurar(XRGrabInteractable arco, Transform visual, Func<GameObject> novaFlecha, GameObject alvo, TextMesh placar, Transform area)
        {
            Arco = arco;
            Visual = visual;
            Alvo = alvo.transform;
            Area = area;
            Texto = placar;
            montarFlecha = novaFlecha;

            // A esfera achatada vira uma bola de 0,7 m de raio com colisor de esfera; o alvo precisa de um disco fino.
            foreach (var colisor in alvo.GetComponents<Collider>()) colisor.enabled = false;
            alvo.AddComponent<BoxCollider>();
            Physics.SyncTransforms();

            FlechaCarregada = montarFlecha();
            arco.activated.AddListener(AoAtivar);
            arco.selectEntered.AddListener(AoPegarArco);
            arco.selectExited.AddListener(AoSoltarArco);
            AtualizarPlacar();

            // A corda precisa da flecha carregada, que já existe aqui.
            Corda = gameObject.AddComponent<ArcheryString>();
            Corda.Configurar(this);
        }

        /// <summary>Solta a flecha carregada na direção +Z do arco, na pose em que ela está.</summary>
        public void Disparar(float velocidade)
        {
            if (!FlechaCarregada) return;
            var flecha = FlechaCarregada;
            FlechaCarregada = null;
            recargaEm = Time.time + recarga;
            Disparos++;
            flecha.transform.SetParent(Area, true);
            var voo = flecha.AddComponent<ArcheryArrow>();
            UltimaFlecha = voo;
            voo.Lancar(flecha.transform.forward * velocidade, Alvo, Arco.transform, Corda ? Corda.Pegador.transform : null,
                tempoMaximoNoAr, tempoCravada, AoAcertar, AoErrar);
        }

        void Update()
        {
            if (FlechaCarregada || Time.time < recargaEm) return;
            FlechaCarregada = montarFlecha();
            FlechaMontada?.Invoke();
        }

        void AoAtivar(ActivateEventArgs args)
        {
            // Sem argumentos: modo PC (o DesktopPlayer chama activated.Invoke(null)). O gatilho do Quest traz
            // argumentos e é ignorado, para a flecha só sair pela corda.
            if (args != null || !FlechaCarregada) return;
            var arco = Arco.transform;
            FlechaCarregada.transform.SetPositionAndRotation(arco.position + arco.forward * avancoDaFlechaNoPc, arco.rotation);
            Disparar(velocidadeDoPc);
        }

        void AoPegarArco(SelectEnterEventArgs args)
        {
            ArcoNaMao = true;
            ArcoComControle = args != null && args.interactorObject != null; // args é nulo no modo PC
        }

        void AoSoltarArco(SelectExitEventArgs args)
        {
            // Com o selectMode padrão (Single), o XRI tira o arco da primeira mão antes de entregá-lo à segunda:
            // numa troca de mão, ArcoSolto dispara e AoPegarArco volta a marcar o arco como seguro na mesma chamada.
            // O retorno abaixo só vale se o arco aceitar duas mãos (selectMode Multiple). args pode ser nulo (modo PC).
            if (Arco.isSelected) return;
            ArcoNaMao = false;
            ArcoComControle = false;
            ArcoSolto?.Invoke();
        }

        void AoAcertar(float distancia)
        {
            Placar.Registrar(distancia);
            AtualizarPlacar();
        }

        void AoErrar()
        {
            Placar.Errou();
            AtualizarPlacar();
        }

        void AtualizarPlacar()
        {
            if (Texto && Texto.text != Placar.Texto) Texto.text = Placar.Texto;
        }
    }
}
