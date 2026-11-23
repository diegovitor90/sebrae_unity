using UnityEngine;

namespace SebraeLab.Missions
{
    /// <summary>
    /// Onde fica cada peça da trilha dentro do prédio do Sebrae Lab. Norte = +Z, leste = +X, piso em y = 0.
    /// Medidas tiradas da cena em 16/11/2026, por colisores numa grade de 0,25 m. As guinadas seguem a convenção das
    /// placas: a frente (+Z local) aponta para longe de quem lê.
    /// </summary>
    public static class PlantaDaTrilha
    {
        // ---------------------------------------------------------------- Abertura
        public static readonly Vector3 Nascimento = new Vector3(5.7f, 0f, 7.1f);
        public static readonly Vector3 Gerente = new Vector3(1.85f, 0f, 7.9f);
        public static readonly Vector3 IdentificacaoDoGerente = new Vector3(1.85f, 2.12f, 7.9f);
        public static readonly Vector3 CaixaDeConversa = new Vector3(2.7f, 1.48f, 6.25f);
        /// <summary>A mesa da recepção passa a ficar entre quem nasce e o gerente, girada 90°; o gerente fica atrás dela, a oeste.</summary>
        public const string NomeDaMesaDoGerente = "Mesa Recepção";
        public static readonly Vector3 MesaDoGerente = new Vector3(2.95f, .37f, 7.9f);
        public const float GiroDaMesaDoGerente = 90f;
        /// <summary>Prédio com a calçada, para a verificação de vestígios.</summary>
        public static readonly Bounds Predio = new Bounds(new Vector3(.15f, 1.5f, 0f), new Vector3(16.4f, 4f, 34.4f));
        public const string NomeDaFachada = "Parede transparente • fachada";
        /// <summary>Nome do vidro da recepção no FBX, com o espaço do fim.</summary>
        public const string NomeDaEntrada = "Entrada ";

        // ---------------------------------------------------------------- Sala 2 (Sala A do prédio)
        public static readonly Bounds Sala2 = new Bounds(new Vector3(-2.625f, 1.4f, -4.625f), new Vector3(5.75f, 2.8f, 2.35f));
        /// <summary>Dobradiça: ponta norte do vão de cerca de 0,8 m na parede do corredor.</summary>
        public static readonly Vector3 Dobradica = new Vector3(.62f, 0f, -3.6f);
        /// <summary>Com 90°, a porta fechada vai da dobradiça para o sul; abrindo, gira para dentro da Sala A.</summary>
        public const float GuinadaDaPorta = 90f;
        public const float LarguraDaPorta = .8f;
        public static readonly Vector3 AreaDeConclusao = new Vector3(-2.25f, 0f, -4.75f);
        public static readonly Vector3 TamanhoDaAreaDeConclusao = new Vector3(4.5f, 2.5f, 1.8f);
        public static readonly Vector3 MesaDoEscritorio = new Vector3(-2.2f, .44f, -5.55f);
        public static readonly Vector3 Pires = new Vector3(-2.2f, .9f, -5.55f);
        public static readonly Vector3 CadeiraDoEscritorio = new Vector3(-3.1f, .25f, -5.15f);
        public static readonly Vector3 QuadroDoEscritorio = new Vector3(-5.48f, 1.65f, -4.75f);
        /// <summary>Centro, no piso, da prateleira de livros decorativos, na parede sul, a oeste da mesa.</summary>
        public static readonly Vector3 PrateleiraDoEscritorio = new Vector3(-4.3f, 0f, -5.53f);
        public static readonly Vector3 PlacaSala2 = new Vector3(.9f, 2.45f, -4.0f);
        /// <summary>Placas sobre as portas das salas, lidas por quem está no corredor olhando para oeste.</summary>
        public const float GuinadaDasPortasDoCorredor = 270f;

        // ---------------------------------------------------------------- Corredor sul e mural
        public static readonly Bounds Corredor = new Bounds(new Vector3(2.375f, 1.4f, -6.1f), new Vector3(2.75f, 2.8f, 5.5f));
        public static readonly Vector3 Mural = new Vector3(3.78f, 1.85f, -2.03f);
        public const float GuinadaDoMural = 90f;
        /// <summary>Quadro decorativo original que sai para dar lugar ao mural.</summary>
        public const string QuadroQueSai = "DecoraçãoParede";
        public static readonly Vector3 BotaoLerMural = new Vector3(3.72f, .9f, -2.03f);
        public static readonly Vector3 BotaoEntendido = new Vector3(3.72f, .9f, -3.1f);
        public static readonly Vector3 PlacaDoExpediente = new Vector3(2.4f, 2.3f, -3.1f);
        public const float GuinadaDaPlacaDoExpediente = 180f;

        // ---------------------------------------------------------------- Copa
        public static readonly Bounds Copa = new Bounds(new Vector3(-2.75f, 1.4f, -1f), new Vector3(5.5f, 2.8f, 4f));
        public static readonly Vector3 CopoDeCafe = new Vector3(-2.6f, 1.15f, -.95f);
        public static readonly Vector3 PlacaDaCopa = new Vector3(-2.96f, 1.8f, -1f);
        public const float GuinadaDaPlacaDaCopa = 0f;

        // ---------------------------------------------------------------- Mesa de inovação (sobre a Mesa Grande original)
        public static readonly Vector3 Cartilha = new Vector3(3.25f, .57f, .16f);
        public static readonly Vector3 Prototipo = new Vector3(3.4f, .68f, -.58f);
        public static readonly Vector3 PlacaDaMesaDeInovacao = new Vector3(3.36f, 1.3f, -.21f);
        public const float GuinadaDaMesaDeInovacao = 90f;

        // ---------------------------------------------------------------- Sala de aula (Sala B do prédio)
        public static readonly Bounds SalaDeAula = new Bounds(new Vector3(-2.625f, 1.4f, -7.5f), new Vector3(5.75f, 2.8f, 2.1f));
        public static readonly Vector3 Estante = new Vector3(-5.15f, .55f, -7.45f);
        /// <summary>Tamanho antes de girar; a estante é girada 90° e o comprimento fica ao longo da parede oeste.</summary>
        public static readonly Vector3 TamanhoDaEstante = new Vector3(1.45f, 1.1f, .65f);
        public static readonly Vector3[] Mesas = { new Vector3(-3.7f, .45f, -8.15f), new Vector3(-2.05f, .45f, -8.15f) };
        public static readonly Vector3 TamanhoDaMesa = new Vector3(1.45f, .9f, .7f);
        public static readonly Vector3 PlacaSalaDeAula = new Vector3(.9f, 2.45f, -8.1f);
        public static readonly Vector3[] InicioDosLivros = { new Vector3(-3.7f, .94f, -8.15f), new Vector3(-2.05f, .94f, -8.15f) };
        public static readonly Vector3[] DestinoDosLivros = { new Vector3(-5.15f, 1.14f, -7.1f), new Vector3(-5.15f, 1.14f, -7.8f) };
        /// <summary>Logo depois da porta, encostadas na divisória: sobra cerca de 1 m entre elas e a parede do corredor.</summary>
        public static readonly Vector3[] InicioDasCadeiras = { new Vector3(-.7f, .26f, -7.6f), new Vector3(-.7f, .26f, -8.2f) };
        public static readonly Vector3[] DestinoDasCadeiras = { new Vector3(-3.7f, .26f, -7.45f), new Vector3(-2.05f, .26f, -7.45f) };
        /// <summary>Na prateleira do meio da estante.</summary>
        public static readonly Vector3[] InicioDosNotebooks = { new Vector3(-5.15f, .61f, -7.1f), new Vector3(-5.15f, .61f, -7.8f) };
        public static readonly Vector3[] DestinoDosNotebooks = { new Vector3(-3.7f, .92f, -8.15f), new Vector3(-2.05f, .92f, -8.15f) };

        // ---------------------------------------------------------------- Fundos
        public const string NomeDaPortaDeVidro = "Backroom GlassDoor";
        public const float CursoDaPortaDeVidro = 2.4f;
        public const float TempoDaPortaDeVidro = 1f;
        public static readonly Vector3 PlacaDaPortaDosFundos = new Vector3(1.79f, 2.45f, -8.8f);
        public const float GuinadaDaPortaDosFundos = 180f;
        /// <summary>Vãos sem colisão ao lado da porta de vidro, por onde se entraria nos fundos antes da hora.</summary>
        public static readonly Bounds[] Frestas =
        {
            new Bounds(new Vector3(3.35f, 1.4f, -9f), new Vector3(.8f, 2.8f, .3f)),
            new Bounds(new Vector3(.2f, 1.4f, -8.95f), new Vector3(.9f, 2.8f, .3f)),
        };
        public static readonly Bounds Fundos = new Bounds(new Vector3(-.875f, 1.4f, -11.85f), new Vector3(9.25f, 2.8f, 5.5f));
        /// <summary>Onde a porta do anexo devolve a pessoa: 0,85 m para dentro dos fundos, diante da porta.</summary>
        public static readonly Vector3 ChegadaDosFundos = new Vector3(2.75f, 0f, -12.5f);
        public const float GuinadaDeChegadaDosFundos = 270f; // olhando para oeste, para a porta de vidro

        // ---------------------------------------------------------------- Anexo da descompressão (Etapa 3)
        public const string NomeDoAnexo = "Anexo de descompressão";
        /// <summary>
        /// A sala antiga (x de 41 a 55, z de −8 a 8) vai para o leste dos fundos, sem giro: o boliche e o arco atiram
        /// para +Z, e quem joga fica na metade sul, perto da porta.
        /// </summary>
        public static readonly Vector3 DeslocamentoDaDescompressao = new Vector3(-36.65f, 0f, -7.5f);
        /// <summary>Converte uma posição da sala antiga para o anexo.</summary>
        public static Vector3 NaDescompressao(Vector3 posicaoAntiga) => posicaoAntiga + DeslocamentoDaDescompressao;
        /// <summary>
        /// Sala do anexo no mundo, entre os eixos das paredes: x de 4,35 a 18,35, z de −15,5 a 0,5. As paredes têm 14 cm, e
        /// as faces internas ficam em x 4,42 e 18,28, z −15,43 e 0,43.
        /// </summary>
        public static readonly Bounds Anexo = new Bounds(new Vector3(11.35f, 1.6f, -7.5f), new Vector3(14f, 3.2f, 16f));
        /// <summary>Face externa da parede leste do prédio, nos fundos. O anexo começa depois dela.</summary>
        public const float FaceLesteDoPredio = 4.25f;
        /// <summary>Onde começa o vidro da recepção (Entrada), ao norte do anexo.</summary>
        public const float InicioDoVidroDaRecepcao = 1.35f;
        /// <summary>Sem giro, as placas da sala continuam com a mesma guinada.</summary>
        public const float GuinadaDaDescompressao = 90f;
        /// <summary>Porta na face interna da parede leste dos fundos; a folha fica virada para dentro dos fundos.</summary>
        public static readonly Vector3 PortaDosFundosParaOAnexo = new Vector3(3.75f, 0f, -12.5f);
        public const float GuinadaDaPortaDosFundosParaOAnexo = 270f;
        /// <summary>Porta na face interna da parede oeste do anexo (x local 41,07), frente a frente com a dos fundos.</summary>
        public static readonly Vector3 PortaDoAnexoParaOsFundos = NaDescompressao(new Vector3(41.07f, 0f, -5f));
        public const float GuinadaDaPortaDoAnexoParaOsFundos = 90f;
        /// <summary>1 m para dentro do anexo; em z local −5, longe do botão REARMAR BOLICHE (z local −3).</summary>
        public static readonly Vector3 ChegadaNoAnexo = NaDescompressao(new Vector3(42.1f, 0f, -5f));
        public const float GuinadaDeChegadaNoAnexo = 90f; // olhando para leste, para a pista
        public static readonly Vector3 PlacaDaPortaDoAnexo = new Vector3(3.7f, 2.45f, -12.5f);
        public const float GuinadaDaPlacaDaPortaDoAnexo = 90f; // lida por quem olha para leste
        public static readonly Vector3 PlacaDeVoltaNoAnexo = NaDescompressao(new Vector3(41.12f, 2.45f, -5f));
        public const float GuinadaDaPlacaDeVolta = 270f; // lida por quem olha para oeste

        // ---------------------------------------------------------------- Feixe (no chão, onde a pessoa para)
        public static readonly Vector3 FeixeDoGerente = new Vector3(3.95f, 0f, 7.9f); // a 2,1 m do gerente: dentro dos 2,2 m de conversationDistance, diante da mesa
        public static readonly Vector3 FeixeDaPortaDaSala2 = new Vector3(1.4f, 0f, -4f);
        public static readonly Vector3 FeixeDoMural = new Vector3(2.6f, 0f, -2.03f);
        public static readonly Vector3 FeixeDaBancada = new Vector3(-2.96f, 0f, -1.9f);
        public static readonly Vector3 FeixeDoPires = new Vector3(-2.2f, 0f, -4.75f);
        public static readonly Vector3 FeixeDaPortaDosFundos = new Vector3(1.79f, 0f, -8.3f);
        public static readonly Vector3 FeixeDaPortaDoAnexo = new Vector3(2.9f, 0f, -12.5f);

        /// <summary>
        /// Onde a pessoa fica para pegar ou entregar algo em <paramref name="ponto"/>. Na sala de aula, é o corredor
        /// interno (z −6,85) na altura do ponto, sem encostar na parede oeste; fora dela, é o chão sob o ponto.
        /// </summary>
        public static Vector3 ChaoDiante(Vector3 ponto) =>
            SalaDeAula.Contains(new Vector3(ponto.x, 1.4f, ponto.z))
                ? new Vector3(Mathf.Max(ponto.x, -4.5f), 0f, -6.85f)
                : new Vector3(ponto.x, 0f, ponto.z);

        // ---------------------------------------------------------------- Caminhada (grade de 20 cm, quatro direções)
        public const float LimiteDoTrecho = 18f;
        public const float LimiteDaCaminhada = 110f;
    }
}
