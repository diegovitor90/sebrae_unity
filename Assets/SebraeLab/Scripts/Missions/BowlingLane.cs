using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SebraeLab.Missions
{
    /// <summary>
    /// Regras do boliche da sala de descompressão: materiais físicos, massas, triggers, ajuda leve no
    /// arremesso do Quest, ciclo da jogada, rodadas de duas bolas (<see cref="BowlingRound"/>), placar,
    /// varredura e rearme. O InternshipDay monta as peças e chama <see cref="Configurar"/>.
    /// </summary>
    public class BowlingLane : MonoBehaviour
    {
        public enum Fase { Pronta, Solta, Rolando, Assentando, AguardandoRearme }

        [Header("Ajuda no arremesso (Quest)")]
        public float anguloMaximo = 10f;
        public float velocidadeMinima = 3f;
        public float velocidadeMaxima = 12f;
        public float alturaMaximaDeApoio = .6f;

        [Header("Contagem")]
        public float inclinacaoDerrubado = 35f;
        public float deslocamentoDerrubado = .15f;

        [Header("Tempos (s)")]
        public float esperaVelocidade = 1.5f;
        public float tempoParada = 1f;
        public float tempoMaximoRolando = 8f;
        public float tempoMaximoAssentando = 3f;
        public float pausaRearme = 2f;
        public float duracaoDestaque = 2f;

        [Header("Massas (kg)")]
        public float massaBola = 6f;
        public float massaPino = 1.5f;

        public BowlingRound Partida { get; } = new BowlingRound();
        public Fase FaseAtual { get; private set; }
        public Collider Pista { get; private set; }
        public IReadOnlyList<Collider> Calhas => calhas;
        public IReadOnlyList<Rigidbody> Pinos => pinos;
        public XRGrabInteractable Bola { get; private set; }
        public Vector3 SuporteBola { get; private set; }
        public Collider AreaDeLancamento { get; private set; }
        public TextMesh Placar { get; private set; }
        public float RaioBola { get; private set; }

        readonly List<Collider> calhas = new List<Collider>();
        readonly List<Rigidbody> pinos = new List<Rigidbody>();
        readonly List<Vector3> origens = new List<Vector3>();
        bool[] emPeNoInicio = new bool[0];
        Rigidbody corpoBola;
        Collider colisorBola;
        bool ajudaPendente, tocouCalha, chegouAoFundo;
        float tempoFase, tempoLento, destaqueAte;

        public void Configurar(Collider pista, IEnumerable<Collider> fundosDasCalhas, IEnumerable<Collider> bordasDasCalhas,
            IEnumerable<Rigidbody> pinosDaPista, XRGrabInteractable bola, TextMesh placar)
        {
            // Colisores recém-montados ainda não têm bounds no mundo; os triggers abaixo dependem deles.
            Physics.SyncTransforms();
            Pista = pista;
            Placar = placar;
            Bola = bola;
            corpoBola = bola.GetComponent<Rigidbody>();
            colisorBola = bola.GetComponent<Collider>();
            SuporteBola = bola.transform.position;
            var esfera = colisorBola as SphereCollider;
            RaioBola = esfera ? esfera.radius * bola.transform.lossyScale.x : colisorBola.bounds.extents.y;
            foreach (var pino in pinosDaPista) { pinos.Add(pino); origens.Add(pino.position); }
            emPeNoInicio = new bool[pinos.Count];

            var materialPista = MaterialFisico("Pista", .05f, .05f, 0f, PhysicsMaterialCombine.Minimum);
            var materialBola = MaterialFisico("Bola", .2f, .2f, 0f, PhysicsMaterialCombine.Average);
            var materialPino = MaterialFisico("Pino", .4f, .4f, .3f, PhysicsMaterialCombine.Average);
            pista.sharedMaterial = materialPista;
            foreach (var c in fundosDasCalhas) { c.sharedMaterial = materialPista; calhas.Add(c); }
            foreach (var c in bordasDasCalhas) c.sharedMaterial = materialPista;
            colisorBola.sharedMaterial = materialBola;
            corpoBola.mass = massaBola;
            corpoBola.interpolation = RigidbodyInterpolation.Interpolate;
            corpoBola.maxAngularVelocity = 60f;
            foreach (var pino in pinos)
            {
                pino.mass = massaPino;
                foreach (var c in pino.GetComponents<Collider>()) c.sharedMaterial = materialPino;
            }

            // Triggers a partir da pista: lançamento de z min-2,5 a min+1,5; fundo de z max-0,3 a max+2,5.
            var b = pista.bounds;
            float largura = b.size.x + .96f;
            AreaDeLancamento = CriarTrigger("Área de lançamento", BowlingTrigger.Tipo.Lancamento,
                new Vector3(b.center.x, 1f, b.min.z - .5f), new Vector3(largura, 2f, 4f));
            foreach (var c in calhas)
            {
                var cb = c.bounds;
                CriarTrigger("Calha", BowlingTrigger.Tipo.Calha, new Vector3(cb.center.x, -.1f, cb.center.z), new Vector3(cb.size.x, .2f, cb.size.z));
            }
            CriarTrigger("Fundo da pista", BowlingTrigger.Tipo.Fundo,
                new Vector3(b.center.x, .4f, b.max.z + 1.1f), new Vector3(largura, 1.2f, 2.8f));

            bola.selectEntered.AddListener(AoPegar);
            bola.selectExited.AddListener(AoSoltar);
            AtualizarPlacar();
        }

        /// <summary>Recomeça a rodada atual: bola no suporte, 10 pinos em pé, bola 1, total mantido.</summary>
        public void Rearmar()
        {
            if (Bola.isSelected) return;
            ajudaPendente = false;
            DevolverBola();
            LevantarPinos();
            Partida.RecomecarRodada();
            FaseAtual = Fase.Pronta;
            destaqueAte = 0f;
            AtualizarPlacar();
        }

        /// <summary>Pino ativo inclinado além do limite ou deslocado da origem no plano horizontal.</summary>
        public bool Derrubado(int i)
        {
            var pino = pinos[i];
            if (!pino.gameObject.activeInHierarchy) return false;
            var deslocamento = pino.position - origens[i];
            deslocamento.y = 0f;
            return Vector3.Angle(pino.rotation * Vector3.up, Vector3.up) > inclinacaoDerrubado || deslocamento.magnitude > deslocamentoDerrubado;
        }

        /// <summary>Pinos que estavam em pé no início da jogada e agora estão derrubados.</summary>
        public int PinosDerrubadosNestaJogada
        {
            get
            {
                int n = 0;
                for (int i = 0; i < pinos.Count; i++) if (emPeNoInicio[i] && Derrubado(i)) n++;
                return n;
            }
        }

        internal void AoEntrarNoTrigger(BowlingTrigger.Tipo tipo, Collider outro)
        {
            if (outro.attachedRigidbody != corpoBola || (FaseAtual != Fase.Solta && FaseAtual != Fase.Rolando)) return;
            if (tipo == BowlingTrigger.Tipo.Calha) tocouCalha = true;
            else if (tipo == BowlingTrigger.Tipo.Fundo) chegouAoFundo = true;
        }

        void AoPegar(SelectEnterEventArgs args)
        {
            // Pegar a bola durante a pausa do rearme encerra a pausa; args pode ser nulo (modo PC).
            if (FaseAtual == Fase.AguardandoRearme) FecharRodada();
        }

        void FecharRodada()
        {
            LevantarPinos();
            Partida.ProximaRodada();
            FaseAtual = Fase.Pronta;
        }

        void AoSoltar(SelectExitEventArgs args)
        {
            // Passar a bola de uma mão para a outra não é soltar.
            if (Bola.isSelected) return;
            if (FaseAtual == Fase.AguardandoRearme) FecharRodada();
            // Sem interactor: modo PC ou validador. A ajuda é só para quem solta com o controle.
            var interactor = args != null ? args.interactorObject as Object : null;
            if (interactor && AreaDeLancamento.bounds.Contains(corpoBola.position))
            {
                ajudaPendente = true;
                var vibrador = args.interactorObject.transform.GetComponentInParent<HapticImpulsePlayer>();
                if (vibrador) vibrador.SendHapticImpulse(.4f, .08f);
            }
            if (FaseAtual == Fase.Pronta) { tocouCalha = false; chegouAoFundo = false; FaseAtual = Fase.Solta; tempoFase = 0f; }
        }

        void FixedUpdate()
        {
            if (!ajudaPendente) return;
            ajudaPendente = false;
            AplicarAjuda();
        }

        void AplicarAjuda()
        {
            if (Bola.isSelected || corpoBola.isKinematic) return;
            Physics.SyncTransforms(); // no Quest o XRI move o Transform e a pose do Rigidbody pode estar uma etapa atrás
            var velocidade = corpoBola.linearVelocity;
            var posicao = corpoBola.position;
            RaycastHit? apoio = null;
            foreach (var hit in Physics.RaycastAll(posicao, Vector3.down, RaioBola + alturaMaximaDeApoio, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider != colisorBola && (apoio == null || hit.distance < apoio.Value.distance)) apoio = hit;
            if (apoio.HasValue)
            {
                posicao.y = apoio.Value.point.y + RaioBola;
                velocidade.y = 0f;
            }
            // O piso de lançamento fica 8 cm abaixo da pista: a bola atrás do início da pista sobe para ela, sem bater no degrau.
            var pb = Pista.bounds;
            if (posicao.z < pb.min.z && Mathf.Abs(posicao.x - pb.center.x) < pb.extents.x - RaioBola)
            {
                posicao.y = pb.max.y + RaioBola;
                posicao.z = pb.min.z + RaioBola;
                velocidade.y = 0f;
            }
            corpoBola.transform.position = posicao;
            corpoBola.position = posicao;
            var eixo = Vector3.ProjectOnPlane(Pista.transform.forward, Vector3.up).normalized;
            var horizontal = new Vector3(velocidade.x, 0f, velocidade.z);
            float angulo = horizontal.sqrMagnitude > 1e-4f
                ? Mathf.Clamp(Vector3.SignedAngle(eixo, horizontal, Vector3.up), -anguloMaximo, anguloMaximo)
                : 0f;
            float rapidez = Mathf.Clamp(horizontal.magnitude, velocidadeMinima, velocidadeMaxima);
            var direcao = Quaternion.AngleAxis(angulo, Vector3.up) * eixo;
            corpoBola.linearVelocity = direcao * rapidez + Vector3.up * velocidade.y;
            corpoBola.angularVelocity = Vector3.Cross(Vector3.up, direcao * rapidez) / RaioBola; // já sai rolando
        }

        void Update()
        {
            float dt = Time.deltaTime;
            switch (FaseAtual)
            {
                case Fase.Solta:
                    tempoFase += dt;
                    if (Bola.isSelected) FaseAtual = Fase.Pronta;
                    else if (corpoBola.linearVelocity.magnitude > .5f) ComecarJogada();
                    else if (tempoFase > esperaVelocidade) FaseAtual = Fase.Pronta;
                    break;
                case Fase.Rolando:
                    tempoFase += dt;
                    tempoLento = corpoBola.linearVelocity.magnitude < .1f ? tempoLento + dt : 0f;
                    if (chegouAoFundo || tempoLento > tempoParada || tempoFase > tempoMaximoRolando)
                    {
                        FaseAtual = Fase.Assentando;
                        tempoFase = 0f;
                    }
                    break;
                case Fase.Assentando:
                    tempoFase += dt;
                    if (tempoFase > tempoMaximoAssentando || pinos.All(p => !p.gameObject.activeInHierarchy || p.IsSleeping())) Contar();
                    break;
                case Fase.AguardandoRearme:
                    tempoFase += dt;
                    if (tempoFase > pausaRearme)
                    {
                        FecharRodada();
                    }
                    break;
            }
            AtualizarPlacar();
        }

        void ComecarJogada()
        {
            for (int i = 0; i < pinos.Count; i++) emPeNoInicio[i] = pinos[i].gameObject.activeInHierarchy && !Derrubado(i);
            tempoFase = 0f;
            tempoLento = 0f;
            FaseAtual = Fase.Rolando;
        }

        void Contar()
        {
            Partida.RegistrarBola(PinosDerrubadosNestaJogada, tocouCalha);
            if (!string.IsNullOrEmpty(Partida.Destaque)) destaqueAte = Time.time + duracaoDestaque;
            if (!Bola.isSelected) DevolverBola();
            if (Partida.RodadaEncerrada)
            {
                FaseAtual = Fase.AguardandoRearme;
                tempoFase = 0f;
                return;
            }
            for (int i = 0; i < pinos.Count; i++) if (Derrubado(i)) pinos[i].gameObject.SetActive(false); // varredura
            FaseAtual = Fase.Pronta;
        }

        void AtualizarPlacar()
        {
            if (!Placar) return;
            bool emJogada = FaseAtual == Fase.Rolando || FaseAtual == Fase.Assentando;
            int nestaBola = emJogada ? PinosDerrubadosNestaJogada : Partida.PinosNestaBola;
            string titulo = Time.time < destaqueAte && !string.IsNullOrEmpty(Partida.Destaque) ? Partida.Destaque : "BOLICHE";
            string texto = $"{titulo}  •  RODADA {Partida.Rodada}  •  BOLA {Partida.Bola}\n" +
                           $"Nesta bola: {nestaBola}   Rodada: {Partida.PontosDaRodada}   Total: {Partida.Total}";
            if (Placar.text != texto) Placar.text = texto;
        }

        void DevolverBola() => Repor(corpoBola, SuporteBola, Quaternion.identity);

        void LevantarPinos()
        {
            for (int i = 0; i < pinos.Count; i++)
            {
                pinos[i].gameObject.SetActive(true);
                Repor(pinos[i], origens[i], Quaternion.identity);
            }
            Physics.SyncTransforms();
        }

        static void Repor(Rigidbody corpo, Vector3 posicao, Quaternion rotacao)
        {
            if (!corpo.isKinematic)
            {
                corpo.linearVelocity = Vector3.zero;
                corpo.angularVelocity = Vector3.zero;
            }
            corpo.transform.SetPositionAndRotation(posicao, rotacao);
            corpo.position = posicao;
            corpo.rotation = rotacao;
            corpo.Sleep();
        }

        Collider CriarTrigger(string nome, BowlingTrigger.Tipo tipo, Vector3 centro, Vector3 tamanho)
        {
            var objeto = new GameObject("Trigger • " + nome) { layer = 2 }; // Ignore Raycast: raios do XR e do PC não param nele
            objeto.transform.SetParent(transform, true);
            objeto.transform.position = centro;
            var caixa = objeto.AddComponent<BoxCollider>();
            caixa.isTrigger = true;
            caixa.size = tamanho;
            objeto.AddComponent<BowlingTrigger>().Configurar(this, tipo);
            return caixa;
        }

        static PhysicsMaterial MaterialFisico(string nome, float dinamico, float estatico, float quique, PhysicsMaterialCombine atrito)
        {
            var material = Resources.Load<PhysicsMaterial>("Fisica/" + nome);
            if (material) return material;
            Debug.LogWarning($"BowlingLane: Resources/Fisica/{nome} não encontrado; usando os valores padrão do spec.");
            return new PhysicsMaterial(nome) { dynamicFriction = dinamico, staticFriction = estatico, bounciness = quique, frictionCombine = atrito };
        }
    }

    /// <summary>Repassa ao BowlingLane a entrada da bola num trigger da pista.</summary>
    public class BowlingTrigger : MonoBehaviour
    {
        public enum Tipo { Lancamento, Calha, Fundo }

        BowlingLane dono;
        Tipo tipo;

        public void Configurar(BowlingLane lane, Tipo t)
        {
            dono = lane;
            tipo = t;
        }

        void OnTriggerEnter(Collider outro)
        {
            if (dono) dono.AoEntrarNoTrigger(tipo, outro);
        }
    }
}
