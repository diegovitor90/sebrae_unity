using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace SebraeLab.Missions
{
    /// <summary>
    /// Botão de Canvas em World Space para VR. Responde ao raio dos controles com o GATILHO
    /// (ação UI Press do XR UI Input Module) e dá retorno de hover, clique e bloqueio com cor,
    /// escala, vibração e som.
    /// </summary>
    /// <remarks>
    /// Vai no mesmo objeto do componente Button. Pode ser montado à mão pelo Inspector ou criado
    /// por código com <see cref="Criar"/>.
    /// </remarks>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public class MissionButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Partes do botão")]
        [Tooltip("Imagem de fundo que muda de cor. Vazio: usa o Target Graphic do Button.")]
        public Graphic fundo;
        [Tooltip("Texto do botão. Vazio: procura um Text nos filhos.")]
        public Text rotulo;

        [Header("Cores")]
        public Color corNormal = new Color(.04f, .13f, .22f);
        public Color corHover = new Color(.04f, .58f, .59f);
        public Color corPressionado = new Color(1f, .68f, .14f);
        public Color corBloqueado = new Color(.30f, .33f, .36f);
        public Color textoNormal = Color.white;
        public Color textoPressionado = new Color(.04f, .13f, .22f);

        [Header("Retorno")]
        [Tooltip("Quanto o botão cresce quando o raio passa por cima.")]
        [Range(1f, 1.2f)] public float escalaHover = 1.06f;
        [Tooltip("Força da vibração ao apontar. Zero desliga.")]
        [Range(0f, 1f)] public float vibracaoHover = .15f;
        [Tooltip("Força da vibração ao apertar o gatilho. Zero desliga.")]
        [Range(0f, 1f)] public float vibracaoClique = .5f;
        public bool som = true;

        [Header("Etapa (opcional)")]
        [Tooltip("Marque para o botão só funcionar a partir da etapa escolhida. Antes dela ele fica cinza e, se apertado, mostra o motivo.")]
        public bool exigirEtapa;
        public EtapaExpediente etapa;
        [Tooltip("Vazio: procura o MissionManager da cena.")]
        public MissionManager missoes;

        [Header("Ação")]
        [Tooltip("O que acontece ao apertar. Use este evento, e não o On Click do Button, para o bloqueio por etapa valer.")]
        public UnityEvent aoPressionar = new UnityEvent();

        Button botao;
        Vector3 escalaBase;
        bool hover, segurando, ultimoInteractable = true;
        float destaqueAte;
        HapticImpulsePlayer vibrador;
        static AudioClip somClique, somNegado;

        /// <summary>Distância entre cada face de Canvas e a face do corpo, em metros.</summary>
        const float DistanciaDaFace = .002f;

        /// <summary>Peça inteira do botão (corpo, moldura e as duas faces). Nula em botões montados à mão.</summary>
        public GameObject Peca { get; private set; }

        /// <summary>Cinza: o Button está desativado ou a etapa exigida ainda não foi alcançada.</summary>
        public bool Bloqueado => !botao.interactable || (exigirEtapa && missoes && !missoes.Alcancou(etapa));

        void Awake()
        {
            botao = GetComponent<Button>();
            botao.transition = Selectable.Transition.None;
            botao.navigation = new Navigation { mode = Navigation.Mode.None };
            botao.onClick.AddListener(Pressionar);
            if (!fundo) fundo = botao.targetGraphic;
            if (!rotulo) rotulo = GetComponentInChildren<Text>(true);
            escalaBase = transform.localScale;
            ultimoInteractable = botao.interactable;
        }

        void OnEnable()
        {
            if (exigirEtapa && !missoes) missoes = FindFirstObjectByType<MissionManager>();
            if (missoes) missoes.EtapaMudou += AoMudarEtapa;
            hover = segurando = false;
            destaqueAte = 0;
            transform.localScale = escalaBase;
            Pintar();
        }

        void OnDisable()
        {
            if (missoes) missoes.EtapaMudou -= AoMudarEtapa;
        }

        void OnDestroy()
        {
            if (botao) botao.onClick.RemoveListener(Pressionar);
        }

        void AoMudarEtapa(EtapaExpediente _) => Pintar();

        void Update()
        {
            // O ManagerMission liga e desliga o Button pela distância até o gestor, sem avisar.
            if (botao.interactable != ultimoInteractable)
            {
                ultimoInteractable = botao.interactable;
                Pintar();
            }
            if (destaqueAte > 0 && Time.unscaledTime >= destaqueAte)
            {
                destaqueAte = 0;
                Pintar();
            }
            var alvo = hover && !Bloqueado ? escalaBase * escalaHover : escalaBase;
            if (transform.localScale != alvo)
                transform.localScale = Vector3.Lerp(transform.localScale, alvo, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
        }

        /// <summary>
        /// Aperta o botão. Chamado pelo clique do raio, pelo modo teclado e mouse do editor e pelos
        /// validadores. Bloqueado: mostra o motivo e não executa a ação.
        /// </summary>
        public void Pressionar()
        {
            if (Bloqueado)
            {
                if (exigirEtapa && missoes && !missoes.Alcancou(etapa)) missoes.AvisarBloqueio(etapa);
                Tocar(somNegado ? somNegado : somNegado = Tom(220f, .14f), .6f);
                return;
            }
            destaqueAte = Time.unscaledTime + .15f;
            Pintar();
            Tocar(somClique ? somClique : somClique = Tom(1300f, .05f), .5f);
            aoPressionar.Invoke();
        }

        public void OnPointerEnter(PointerEventData dados)
        {
            hover = true;
            var outro = Vibrador(dados);
            if (outro) vibrador = outro;
            if (!Bloqueado) Vibrar(vibracaoHover, .03f);
            Pintar();
        }

        public void OnPointerExit(PointerEventData dados)
        {
            hover = segurando = false;
            Pintar();
        }

        public void OnPointerDown(PointerEventData dados)
        {
            segurando = true;
            var outro = Vibrador(dados);
            if (outro) vibrador = outro;
            if (Bloqueado) Vibrar(vibracaoClique * .6f, .15f);
            else Vibrar(vibracaoClique, .06f);
            Pintar();
        }

        public void OnPointerUp(PointerEventData dados)
        {
            segurando = false;
            Pintar();
        }

        void Pintar()
        {
            if (!botao) return;
            bool bloqueado = Bloqueado;
            bool pressionado = !bloqueado && (segurando || destaqueAte > 0);
            if (fundo)
                fundo.color = bloqueado ? corBloqueado : pressionado ? corPressionado : hover ? corHover : corNormal;
            if (rotulo)
            {
                var cor = pressionado ? textoPressionado : textoNormal;
                if (bloqueado) cor.a *= .55f;
                rotulo.color = cor;
            }
        }

        /// <summary>
        /// Controle que disparou o evento. No rig do XRI o vibrador fica no objeto do controle,
        /// acima do interactor do raio. Com mouse não há controle e não vibra.
        /// </summary>
        static HapticImpulsePlayer Vibrador(PointerEventData dados)
        {
            if (dados is TrackedDeviceEventData xr && xr.interactor is Component quem)
                return quem.GetComponentInParent<HapticImpulsePlayer>();
            return null;
        }

        void Vibrar(float forca, float duracao)
        {
            if (vibrador && forca > 0f) vibrador.SendHapticImpulse(forca, duracao);
        }

        void Tocar(AudioClip clip, float volume)
        {
            if (som && clip) AudioSource.PlayClipAtPoint(clip, transform.position, volume);
        }

        static AudioClip Tom(float hz, float duracao)
        {
            const int taxa = 22050;
            var amostras = new float[Mathf.CeilToInt(taxa * duracao)];
            for (int i = 0; i < amostras.Length; i++)
            {
                float t = (float)i / taxa;
                amostras[i] = .5f * Mathf.Sin(2f * Mathf.PI * hz * t) * Mathf.Exp(-t / duracao * 4f);
            }
            var clip = AudioClip.Create("Clique " + hz, amostras.Length, 1, taxa, false);
            clip.SetData(amostras, 0);
            return clip;
        }

        /// <summary>
        /// Monta por código um botão parado e de dois lados: um corpo opaco no meio, que esconde a face do outro lado,
        /// e uma face de Canvas World Space com Tracked Device Graphic Raycaster em cada lado. As duas faces chamam a
        /// mesma ação. Devolve o MissionButton da frente; a peça inteira fica em <see cref="Peca"/>.
        /// </summary>
        /// <param name="centro">Centro do botão no mundo.</param>
        /// <param name="tamanho">Largura e altura em metros.</param>
        public static MissionButton Criar(string legenda, Transform pai, Vector3 centro, Vector2 tamanho,
            UnityAction acao, MissionManager missoes = null, EtapaExpediente? etapa = null)
        {
            var peca = new GameObject(legenda);
            peca.SetActive(false); // só acorda os componentes depois de tudo configurado
            peca.transform.SetParent(pai, false);
            peca.transform.position = centro;

            // Colisor da mira do modo PC, dentro do corpo: o raio do XR acerta a face, 2 mm à frente, antes dele.
            var colisor = peca.AddComponent<BoxCollider>();
            colisor.size = new Vector3(tamanho.x, tamanho.y, Placa.Espessura);

            var moldura = Placa.Lamina(peca.transform, "Moldura", "PlacaMoldura");
            moldura.localScale = new Vector3(tamanho.x + 2f * Placa.Borda, tamanho.y + 2f * Placa.Borda, Placa.Espessura * .8f);
            var corpo = Placa.Lamina(peca.transform, "Corpo", "PlacaCorpo");
            corpo.localScale = new Vector3(tamanho.x, tamanho.y, Placa.Espessura);

            var frente = Face(peca.transform, "Frente", -1f, legenda, tamanho, acao, missoes, etapa);
            var verso = Face(peca.transform, "Verso", 1f, legenda, tamanho, acao, missoes, etapa);
            frente.Peca = peca;
            verso.Peca = peca;
            peca.SetActive(true);
            return frente;
        }

        /// <summary>Uma face do botão: Canvas com fundo, borda, texto e este componente.</summary>
        /// <param name="lado">-1 para a frente (lida por quem olha no sentido +Z da peça) e +1 para o verso.</param>
        static MissionButton Face(Transform peca, string nome, float lado, string legenda, Vector2 tamanho,
            UnityAction acao, MissionManager missoes, EtapaExpediente? etapa)
        {
            const float escala = .001f; // 1 unidade do Canvas = 1 mm
            var area = tamanho / escala;

            var raiz = new GameObject(nome, typeof(RectTransform));
            raiz.transform.SetParent(peca, false);
            raiz.transform.localPosition = new Vector3(0f, 0f, lado * (Placa.Espessura / 2f + DistanciaDaFace));
            raiz.transform.localRotation = lado < 0f ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
            raiz.transform.localScale = Vector3.one * escala;
            ((RectTransform)raiz.transform).sizeDelta = area;

            var canvas = raiz.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            // A face de costas para o raio não responde: de qualquer lado, só a face voltada para quem aponta.
            raiz.AddComponent<TrackedDeviceGraphicRaycaster>().ignoreReversedGraphics = true;

            var corpo = new GameObject("Botão", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Button));
            corpo.transform.SetParent(raiz.transform, false);
            Esticar((RectTransform)corpo.transform, 0f);
            var borda = corpo.GetComponent<Outline>();
            borda.effectColor = new Color(1f, .68f, .14f);
            borda.effectDistance = new Vector2(6f, -6f);
            corpo.GetComponent<Button>().targetGraphic = corpo.GetComponent<Image>();

            var texto = new GameObject("Rótulo", typeof(RectTransform), typeof(Text));
            texto.transform.SetParent(corpo.transform, false);
            Esticar((RectTransform)texto.transform, 24f);
            var t = texto.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = legenda;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.resizeTextForBestFit = true; // legenda longa encolhe em vez de sair do botão
            t.resizeTextMinSize = 36;
            t.resizeTextMaxSize = 84;      // ~8 cm de altura de linha: legível a 3 m no Quest 2
            t.raycastTarget = false;

            var botao = corpo.AddComponent<MissionButton>();
            botao.missoes = missoes;
            botao.exigirEtapa = etapa.HasValue;
            if (etapa.HasValue) botao.etapa = etapa.Value;
            if (acao != null) botao.aoPressionar.AddListener(acao);
            return botao;
        }

        static void Esticar(RectTransform rt, float margem)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margem, margem);
            rt.offsetMax = new Vector2(-margem, -margem);
        }
    }
}
