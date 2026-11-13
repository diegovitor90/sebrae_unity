# Etapa 4: arco e flecha com corda, física de voo e alvo por anéis

Data: 2026-11-12. Prazo: entrar no APK da apresentação da P2 (14/11).
Base: branch `feature/etapa4-arco`, criada sobre a Etapa 3 (`feature/etapa3-boliche`, PR #7).

## Contexto

O arco fica na sala de descompressão e é montado por código em `InternshipDay.BuildLounge`. Medidas atuais, que não mudam:

| Peça | Posição e tamanho |
|---|---|
| Suporte do arco | centro (51; 0,45; -2), tamanho (1; 0,9; 1) |
| Arco arcade | caixa de (0,08; 0,7; 0,09) em (51; 1,33; -2), com `Rigidbody` e `XRGrabInteractable`; o eixo longo (Y) é o das lâminas e a flecha sai em +Z |
| Arco recurvo | filho do arco, com escala que anula a do pai (1 m por unidade), 10 lâminas laminadas e a corda (`Corda do arco`, cubo fino em z -0,12, de y -0,77 a 0,77) |
| Flecha de luz | filha do arco recurvo, em (0,032; 0,035; -0,13), montada por `ArcadeArrow`; a ponta fica em z 0,53 local |
| Alvo luminoso | esfera achatada em (51; 1,5; 5), escala (1,4; 1,4; 0,15), com a face (`Face do alvo`, cilindro com a textura `Alvo.png`) montada por `MissionSurfaces` |
| Placar | texto em (51; 2,3; 5), "ARCO ARCADE • n pontos" |

Hoje o gatilho (`activated`) cria uma flecha que anda reta a 9 m/s, sem gravidade, e `ArcadeStar` soma 1 ponto quando ela encosta no alvo. O modo PC (`DesktopPlayer`) segura o arco pelo nome e chama `held.activated.Invoke(null)` com o botão direito. O `DesktopWalkCheck` confere o texto do placar. O commit 57c97fd do Richardson (12/11) já está refletido nesse código, e nenhuma mudança dele ficou pendente para o arco.

A textura `Alvo.png` já tem 5 anéis (centro dourado, vermelho, creme, azul e creme), mas com larguras diferentes (9, 12, 12, 14 e 13,5 cm). A face mede 1,372 m de diâmetro, e o colisor atual é uma esfera, que numa escala achatada vira uma bola de 0,7 m de raio.

## Decisões

| Tema | Decisão |
|---|---|
| Puxar a corda | Segundo `XRGrabInteractable` (o pegador da corda), agarrado pela mão livre. Soltar o pegador dispara. |
| Anéis | 5 anéis em faixas de 14 cm: 10, 8, 6, 4 e 2 pontos. Fora do último anel, erro. |
| Detecção do anel | Distância do ponto de impacto ao centro, projetada no plano do alvo. Os anéis são a textura do alvo, regenerada a partir da mesma tabela de raios, e não têm colisor. |
| Física da flecha | Gravidade real, velocidade de 15 a 40 m/s proporcional à puxada, ponta acompanhando a trajetória. |
| Modo PC | Clique único com força total (40 m/s), como hoje. |
| Placar | Total acumulado e último tiro. Sem rodadas. |
| Código | Regras em classes C# puras, componentes `ArcheryRange` e `ArcheryString` recebendo as peças do `InternshipDay`. |

## Componentes

### `ArcheryScore` (C# puro)

Regras dos anéis e do placar, sem dependência de cena:

- Raios dos anéis em metros: 0,14 (10 pontos), 0,28 (8), 0,42 (6), 0,56 (4) e 0,70 (2), expostos em `Raios` para a textura do alvo usar a mesma tabela. A borda pertence ao anel de dentro (distância igual a 0,14 vale 10).
- `static int PontosDoAnel(float distancia)`: devolve 10, 8, 6, 4 ou 2, ou 0 acima de 0,70.
- `void Registrar(float distancia)`: soma os pontos ao `Total` e atualiza `UltimoPontos` e `UltimoRotulo` ("centro" para 10, "anel 2" a "anel 5" para 8 a 2). Distância acima de 0,70 equivale a `Errou()`. Distância negativa é inválida.
- `void Errou()`: `UltimoPontos` fica 0, `UltimoRotulo` fica "Errou" e o total não muda.
- `string Texto`: duas linhas. A primeira é `ARCO ARCADE • n pontos`. A segunda é `Último tiro: 10 (centro)`, `Último tiro: 6 (anel 3)` ou `Último tiro: Errou`. Antes do primeiro tiro, `Último tiro: nenhum`. Não há forma singular, porque o menor placar acima de zero é 2.

### `ArcheryDraw` (C# puro)

Conversão da puxada em velocidade:

- Puxada máxima 0,50 m, mínima de disparo 0,10 m.
- `float Limitar(float puxada)`: restringe a puxada a 0 a 0,50 m.
- `bool Dispara(float puxada)`: verdadeiro a partir de 0,10 m.
- `float Velocidade(float puxada)`: 0 abaixo de 0,10 m; de 0,10 a 0,50 m, interpolação linear de 15 m/s a 40 m/s; acima de 0,50 vale como 0,50.

### `ArcheryRange` (componente)

Recebe do `InternshipDay` o arco (`XRGrabInteractable`), o visual do arco recurvo, uma função que monta a flecha de luz, o alvo, o texto do placar e a área. Faz o seguinte:

1. **Alvo.** Desliga o colisor de esfera do alvo e põe uma caixa de 1,4 × 1,4 × 0,15 m (colisor de caixa na escala do objeto).
2. **Placar.** Mantém um `ArcheryScore` e escreve `Texto` no `TextMesh`.
3. **Disparo.** `Disparar(float velocidade)` solta a flecha carregada na direção +Z do arco, com `ArcheryArrow`, e conta o tiro. O resultado do voo atualiza o placar.
4. **Disparo do PC.** Escuta `activated` do arco. Com argumento nulo (o caso do `DesktopPlayer`), coloca a flecha 0,38 m à frente do arco e dispara a 40 m/s. Com argumento de verdade (gatilho do Quest), ignora, para a flecha só sair pela corda.
5. **Recarga.** 0,35 s depois de cada disparo, uma flecha nova é montada (mantém o `ArcadeArrow` do `InternshipDay`).
6. **Estado do arco.** Expõe se o arco está seguro (`ArcoNaMao`, pelos eventos `selectEntered` e `selectExited`) e se quem o segura é um controle de VR (`ArcoComControle`; o modo PC pega o arco sem interactor), e cria o `ArcheryString`. Na troca de mão o XRI solta o arco da primeira mão antes de entregá-lo à segunda, então `ArcoSolto` dispara e o arco volta a contar como seguro na mesma chamada.

### `ArcheryString` (componente)

Cuida da corda e do pegador, criado pelo `ArcheryRange`:

1. **Pegador.** `GameObject` pequeno com `Rigidbody` cinemático, colisor esférico sólido (0,07 m) e `XRGrabInteractable`, no ponto de repouso da corda (z -0,12 do visual do arco). O colisor é sólido porque os casters do `NearFarInteractor` do rig (Starter Assets) ignoram triggers. Para não interferir na física, o pegador não tem contato com o arco nem com o `CharacterController` do jogador (`Physics.IgnoreCollision`), e a flecha em voo passa por ele. O colisor só fica ligado enquanto um controle de VR segura o arco; no modo PC fica desligado. Se o arco for solto durante a puxada, a seleção do pegador é cancelada e nenhum tiro sai.
2. **Puxada.** A cada quadro (e antes de renderizar, porque o XRI move o pegador na própria atualização), com o pegador agarrado, projeta a posição dele no eixo do arco (para trás), limita a 0,50 m com `ArcheryDraw.Limitar` e descarta o desvio lateral. A flecha carregada e a corda visual acompanham o pegador. A corda visual passa a ser dois segmentos, das pontas das lâminas (y ±0,77) até o pegador, e no repouso coincide com a corda reta de hoje.
3. **Disparo.** Ao soltar o pegador com o arco na mão, se `ArcheryDraw.Dispara` for verdadeiro, chama `ArcheryRange.Disparar` com `ArcheryDraw.Velocidade`, e as duas mãos recebem vibração curta (`HapticImpulsePlayer` acima de cada interactor). Abaixo de 0,10 m não há tiro e a flecha volta ao repouso.

### `ArcheryArrow` (componente na flecha em voo)

A gravidade e o movimento são calculados pelo componente, sem `Rigidbody` dinâmico. A 40 m/s a flecha anda 0,8 m por passo de física, e o alvo tem 15 cm de espessura, então a colisão por evento pode ser perdida. A cada quadro (`Update`, em passos de no máximo 1/60 s, para o voo ficar fluido no headset):

- soma `Physics.gravity * passo` à velocidade e avança a ponta;
- lança um raio da ponta anterior até a nova, ignorando triggers, o `CharacterController` do jogador, as peças do próprio arco e o pegador da corda;
- gira a flecha para a direção da velocidade.

Ao acertar o alvo, calcula a distância do ponto de impacto ao centro no plano do alvo, chama `ArcheryScore.Registrar` (pelo `ArcheryRange`) e crava a flecha (parada) por 4 s. Ao acertar qualquer outra coisa, ou depois de 5 s no ar, conta `Errou`. Um tiro registra uma única vez.

### Textura do alvo

Um script de editor (`AlvoTexture`) regenera `Alvo.png` com os 5 anéis nas mesmas cores de hoje e nos raios de `ArcheryScore.Raios`, e confere o resultado lendo o arquivo de volta. A borda da textura vale 0,70 m, por isso a face (`Face do alvo`, em `MissionSurfaces`) passa de 98% para 100% da escala do alvo (1,4 m de diâmetro). A pintura e a pontuação saem da mesma tabela.

### `InternshipDay`

Em `BuildLounge`, mantém a criação das peças e:
- remove o bloco do `activated` que dispara a flecha reta, o campo `score` e a classe `ArcadeStar`;
- adiciona o `ArcheryRange` e entrega as referências;
- atualiza o texto da placa do arco para explicar a corda.

As medidas e posições do arco, do suporte, do alvo e do placar continuam as mesmas. O `DesktopPlayer` não muda.

### `DesktopWalkCheck`

A conferência do texto antigo ("ARCO ARCADE • 1 ponto") passa a ler o placar novo: o texto deve começar com `ARCO ARCADE • ` e trazer 2 pontos ou mais, já que a queda de cerca de 11 a 15 cm leva um tiro mirado no centro ao anel de 10 ou de 8.

## Verificação

- `ArcheryScoreCheck` (EditMode, batchmode), escrito antes das classes: bordas dos anéis (0,139 dá 10, 0,14 dá 10, 0,141 dá 8, 0,70 dá 2, 0,71 dá erro), total acumulado, `Errou` sem mexer no total, texto do placar, distância negativa inválida, `ArcheryDraw` (0,09 sem tiro, 0,10 com tiro e 15 m/s, 0,30 com 27,5 m/s, 0,50 com 40 m/s, 0,80 limitado a 40 m/s).
- `AlvoTexture` (batchmode) lê o PNG gerado e confere a cor no meio de cada anel e no canto.
- `ArcheryCheck` (Play, editor com janela que fecha sozinho), com relatório em `Logs/archery-check.txt`, escrito antes dos componentes. Usa mãos de teste (`XRDirectInteractor` inativo, como no `BowlingCheck`) e dispara os eventos do XRI:
  - peças ligadas, colisor de caixa no alvo e face com 1,4 m;
  - disparo do PC (`activated.Invoke(null)`) mirado no centro: a flecha para na face do alvo a 40 m/s, pontua pelo menos 8 e o placar mostra o total;
  - gatilho com argumento de verdade: não dispara;
  - tiro no chão: conta `Errou` e não mexe no total;
  - a flecha recarrega depois de 0,35 s;
  - arco na mão, pegador agarrado e puxado além do limite: a puxada fica em 0,50 m, a flecha e a corda recuam, e soltar dispara a 40 m/s, pontua pelo menos 8 e devolve o pegador ao repouso;
  - puxada de 0,05 m: nenhum tiro sai e a flecha continua no repouso;
  - puxada mínima de 0,10 m (15 m/s): a flecha passa por baixo do alvo e o último tiro é `Errou`;
  - puxada de 0,30 m: velocidade de 27,5 m/s;
  - soltar o arco no meio da puxada: sem tiro, pegador e corda de volta ao repouso;
  - com um controle segurando o arco, a consulta do caster de perto do rig (esfera de 0,1 m, camada Default, ignorando triggers) acha o pegador; sem o arco na mão ou com o arco pego pelo modo PC, não acha; o pegador não tem contato físico com o arco nem com o jogador.
- Continuam passando: `DesktopWalkCheck` (com o placar novo), `BowlingCheck`, `BowlingRoundCheck`, `NavigationCheck` e `HandDoorCheck`. APK gerado ao final.

O editor não prova o gesto de puxar a corda com a mão no Quest. Isso entra no teste no headset.

## Fora do escopo

Som, retículo de mira, rodadas de flechas, animação do arco recurvo envergando, ajuda de mira, mudança de medidas ou posições no `InternshipDay`, mudança no `DesktopPlayer`.

## Riscos

- `InternshipDay` continua sendo editado pelo Richardson. A mudança nele se limita a ligações em `BuildLounge`, mais o texto da placa.
- `MissionSurfaces` muda uma escala (face do alvo de 0,98 para 1) e `Alvo.png` é regenerado. O Richardson precisa saber, porque o PNG é um binário.
- O pegador só ser agarrável com o arco na mão depende de eventos do XRI. O interactor de teste no XRI 3.0.11 é inativo e só serve de "quem soltou", como no `BowlingCheck` da Etapa 3. O comportamento real do XRI ao mover o pegador (e o jitter entre a atualização do XRI e a do `ArcheryString`) só aparece no Quest.
- Com o pegador sólido, o raio de longe do `NearFarInteractor` também pode agarrá-lo à distância. Esse efeito, a troca de mão do arco e a mão livre pegar o arco em vez da corda ficam para o teste no Quest.
- O raio por passo cobre o atravessamento, mas flechas muito rápidas encostando na borda do alvo podem dar um ponto de impacto na quina e cair no anel de fora. A caixa de 15 cm de espessura reduz esse efeito.
