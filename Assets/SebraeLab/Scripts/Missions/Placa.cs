using UnityEngine;
using UnityEngine.Rendering;

namespace SebraeLab.Missions
{
    /// <summary>
    /// Placa de texto da missão: parada, dentro de uma caixa, legível dos dois lados e escondida por paredes.
    /// A raiz é o TextMesh da frente, então quem cria a placa continua escrevendo em .text, ligando e desligando
    /// e mudando o pai como antes. Este componente copia o texto para o verso e ajusta a caixa ao texto.
    /// </summary>
    /// <remarks>
    /// Filhos: Moldura (lâmina dourada), Corpo (lâmina azul, opaca, entre os dois textos) e Verso (TextMesh girado
    /// 180°). O corpo esconde o texto do outro lado porque o shader do texto testa profundidade.
    /// </remarks>
    [RequireComponent(typeof(TextMesh))]
    [DisallowMultipleComponent]
    public class Placa : MonoBehaviour
    {
        /// <summary>Espessura do corpo, em metros.</summary>
        public const float Espessura = .02f;
        /// <summary>Largura da moldura em volta do corpo, em metros.</summary>
        public const float Borda = .015f;
        /// <summary>Distância entre cada texto e a face do corpo, em metros.</summary>
        public const float Folga = .001f;
        /// <summary>Antes deste x a rota anda no eixo Z (praça e galeria); a partir dele, no eixo X.</summary>
        public const float LimiteDoTrecho = 15f;
        /// <summary>Intervalo, em segundos, entre as conferências do texto de cada placa.</summary>
        const float IntervaloDaConferencia = .2f;

        static Material materialDoTexto;
        static Font fonteDoMaterial;

        public TextMesh Frente { get; private set; }
        public TextMesh Verso { get; private set; }
        public Transform Corpo { get; private set; }
        public Transform Moldura { get; private set; }

        /// <summary>Tamanho mínimo do colisor de clique, se a placa tiver um BoxCollider (o mural do modo PC).</summary>
        public Vector3 colisorMinimo;

        MeshRenderer textoDaFrente;
        Bounds ultimaMedida;
        string ultimoTexto;
        BoxCollider colisor;
        float proximaConferencia;
        bool escalonada;

        /// <summary>Direção de uma placa ou botão parado: de frente para o sentido em que se anda no trecho.</summary>
        public static float GuinadaPadrao(Vector3 posicao) => posicao.x < LimiteDoTrecho ? 180f : 90f;

        /// <summary>
        /// Cria uma placa parada em <paramref name="posicao"/>, filha de <paramref name="pai"/>, e devolve o TextMesh da
        /// frente. Sem <paramref name="guinada"/>, usa <see cref="GuinadaPadrao"/>. Use guinadas em múltiplos de 90°.
        /// </summary>
        public static TextMesh Criar(string texto, Vector3 posicao, float tamanho, Transform pai, float? guinada = null)
        {
            var objeto = new GameObject("Placa");
            objeto.transform.SetParent(pai);
            objeto.transform.SetPositionAndRotation(posicao, Quaternion.Euler(0f, guinada ?? GuinadaPadrao(posicao), 0f));
            var frente = objeto.AddComponent<TextMesh>();
            frente.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            objeto.GetComponent<MeshRenderer>().sharedMaterial = MaterialDoTexto(frente.font);
            frente.fontSize = 72;
            frente.characterSize = tamanho * .1f;
            frente.anchor = TextAnchor.MiddleCenter;
            frente.alignment = TextAlignment.Center;
            frente.text = texto;
            objeto.AddComponent<Placa>(); // Awake monta corpo, moldura e verso a partir da frente já configurada
            return frente;
        }

        /// <summary>
        /// Material do texto, compartilhado por todas as placas: shader com teste de profundidade usando a textura da
        /// fonte. A fonte é dinâmica: o Unity pode recriar a textura ao incluir caracteres novos, e o material acompanha.
        /// </summary>
        public static Material MaterialDoTexto(Font fonte)
        {
            if (!materialDoTexto)
            {
                var modelo = Resources.Load<Material>("Acabamentos/PlacaTexto");
                if (!modelo) throw new System.InvalidOperationException("Material ausente: Acabamentos/PlacaTexto");
                materialDoTexto = new Material(modelo) { name = "PlacaTexto (instância)" };
                Font.textureRebuilt += AoRecriarTextura;
            }
            fonteDoMaterial = fonte;
            materialDoTexto.mainTexture = fonte.material.mainTexture;
            return materialDoTexto;
        }

        static void AoRecriarTextura(Font fonte)
        {
            if (materialDoTexto && fonte == fonteDoMaterial) materialDoTexto.mainTexture = fonte.material.mainTexture;
        }

        /// <summary>Lâmina sem colisor, sem luz e sem sombra: corpo ou moldura de placas e botões.</summary>
        public static Transform Lamina(Transform pai, string nome, string material)
        {
            var acabamento = Resources.Load<Material>("Acabamentos/" + material);
            if (!acabamento) throw new System.InvalidOperationException("Material ausente: Acabamentos/" + material);
            var lamina = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lamina.name = nome;
            var colisor = lamina.GetComponent<Collider>();
            colisor.enabled = false; // Destroy só acontece no fim do quadro
            Destroy(colisor);
            lamina.transform.SetParent(pai, false);
            var renderer = lamina.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = acabamento;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return lamina.transform;
        }

        void Awake()
        {
            Frente = GetComponent<TextMesh>();
            textoDaFrente = GetComponent<MeshRenderer>();
            // Numa cópia (Instantiate) as peças já existem; numa placa nova, são criadas aqui.
            Moldura = transform.Find("Moldura");
            if (!Moldura) Moldura = Lamina(transform, "Moldura", "PlacaMoldura");
            Corpo = transform.Find("Corpo");
            if (!Corpo) Corpo = Lamina(transform, "Corpo", "PlacaCorpo");
            var verso = transform.Find("Verso");
            Verso = verso ? verso.GetComponent<TextMesh>() : CriarVerso();
            Ajustar(MedirTexto(), Frente.text);
        }

        TextMesh CriarVerso()
        {
            var objeto = new GameObject("Verso");
            objeto.transform.SetParent(transform, false);
            objeto.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var texto = objeto.AddComponent<TextMesh>();
            texto.font = Frente.font;
            objeto.GetComponent<MeshRenderer>().sharedMaterial = textoDaFrente.sharedMaterial;
            texto.fontSize = Frente.fontSize;
            texto.characterSize = Frente.characterSize;
            texto.anchor = Frente.anchor;
            texto.alignment = Frente.alignment;
            texto.lineSpacing = Frente.lineSpacing;
            texto.color = Frente.color;
            texto.text = Frente.text;
            return texto;
        }

        void LateUpdate()
        {
            // A cada quadro só se mede o texto (sem lixo); ler TextMesh.text cria uma string nova a cada leitura.
            var medida = MedirTexto();
            if (medida != ultimaMedida) Sincronizar(medida, Frente.text); // o tamanho mudou: a caixa reage na hora
            else if (Time.time >= proximaConferencia)
            {
                // Conferência a cada 0,2 s por placa, com fase aleatória na segunda para as placas não conferirem juntas.
                proximaConferencia = Time.time + (escalonada ? IntervaloDaConferencia : Random.value * IntervaloDaConferencia);
                escalonada = true;
                var texto = Frente.text;
                if (texto != ultimoTexto) Sincronizar(medida, texto);
                if (Verso.color != Frente.color) Verso.color = Frente.color;
                TryGetComponent(out colisor); // o colisor pode ser acrescentado depois que a placa nasce
            }
            // Painel clicável no modo PC (como o do mural): o colisor acompanha a caixa, sem ficar menor que o mínimo.
            if (colisor)
            {
                var tamanho = Vector3.Max(Corpo.localScale, colisorMinimo);
                if (colisor.center != Corpo.localPosition || colisor.size != tamanho)
                {
                    colisor.center = Corpo.localPosition;
                    colisor.size = tamanho;
                }
            }
        }

        void OnEnable()
        {
            // Quem estava desligado pode ter perdido mudanças de texto: confere já no primeiro quadro.
            proximaConferencia = 0f;
            escalonada = false;
        }

        void Sincronizar(Bounds medida, string texto)
        {
            Verso.text = texto;
            Ajustar(medida, texto);
        }

        /// <summary>Caixa do texto da frente no espaço da raiz (exata para rotações em múltiplos de 90°).</summary>
        Bounds MedirTexto()
        {
            var mundo = textoDaFrente.bounds;
            var tamanho = transform.InverseTransformVector(mundo.size);
            return new Bounds(transform.InverseTransformPoint(mundo.center),
                new Vector3(Mathf.Abs(tamanho.x), Mathf.Abs(tamanho.y), Mathf.Abs(tamanho.z)));
        }

        void Ajustar(Bounds medida, string texto)
        {
            ultimaMedida = medida;
            ultimoTexto = texto;
            int linhas = Mathf.Max(1, texto.Split('\n').Length);
            float margem = .5f * medida.size.y / linhas;
            float largura = medida.size.x + 2f * margem;
            float altura = medida.size.y + 2f * margem;
            // A frente é lida por quem olha no sentido +Z: a caixa fica atrás dela, em +Z.
            var centro = new Vector3(medida.center.x, medida.center.y, Folga + Espessura / 2f);
            Corpo.localPosition = centro;
            Corpo.localScale = new Vector3(largura, altura, Espessura);
            Moldura.localPosition = centro;
            Moldura.localScale = new Vector3(largura + 2f * Borda, altura + 2f * Borda, Espessura * .8f);
            // O verso é girado 180°: o deslocamento horizontal do texto se inverte, por isso o dobro do centro.
            Verso.transform.localPosition = new Vector3(2f * medida.center.x, 0f, Espessura + 2f * Folga);
        }
    }
}
