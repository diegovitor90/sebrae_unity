# Etapa 4: arco e flecha com corda, física de voo e alvo por anéis

Data: 2026-11-12. Prazo: entrar no APK da apresentação da P2 (14/11).
Base: branch `feature/etapa4-arco`, criada sobre a Etapa 3 (`feature/etapa3-boliche`, PR #7).

## Contexto

O arco fica na sala de descompressão e é montado por código em `InternshipDay.BuildLounge`. Medidas atuais, que não mudam:

| Peça | Posição e tamanho |
|---|---|
| Suporte do arco | centro (51; 0,45; -2), tamanho (1; 0,9; 1) |
| Arco arcade | caixa de (0,08; 0,7; 0,09) em (51; 1,33; -2), com `Rigidbody` e `XRGrabInteractable`; o eixo longo (Y) é o das lâminas e a flecha sai em +Z |
| Arco recurvo | filho do arco, com 10 lâminas laminadas e a corda (`Corda do arco`, cubo fino em z -0,12) |
| Flecha de luz | filha do arco recurvo, em (0,032; 0,035; -0,13), montada por `ArcadeArrow` |
| Alvo luminoso | esfera achatada em (51; 1,5; 5), escala (1,4; 1,4; 0,15) |
| Placar | texto em (51; 2,3; 5), "ARCO ARCADE • n pontos" |

Hoje o gatilho (`activated`) cria uma flecha que anda reta a 9 m/s, sem gravidade, e `ArcadeStar` soma 1 ponto quando ela encosta no alvo. O modo PC (`DesktopPlayer`) segura o arco pelo nome e chama `held.activated.Invoke(null)` com o botão direito. O `DesktopWalkCheck` confere o texto do placar. O commit 57c97fd do Richardson (12/11) já está refletido nesse código, e nenhuma mudança dele ficou pendente para o arco.

## Decisões

| Tema | Decisão |
|---|---|
| Puxar a corda | Segundo `XRGrabInteractable` (o pegador da corda), agarrado pela mão livre. Soltar o pegador dispara. |
| Anéis | 5 anéis em faixas de 14 cm: 10, 8, 6, 4 e 2 pontos. Fora do último anel, erro. |
| Detecção do anel | Distância do ponto de impacto ao centro, projetada no plano do alvo. Os anéis são pintura visível, sem colisor. |
| Física da flecha | Gravidade real, velocidade de 15 a 40 m/s proporcional à puxada, ponta acompanhando a trajetória. |
| Modo PC | Clique único com força total (40 m/s), como hoje. |
| Placar | Total acumulado e último tiro. Sem rodadas. |
| Código | Regras em classes C# puras, componente `ArcheryRange` recebendo as peças do `InternshipDay`. |

## Componentes

### `ArcheryScore` (C# puro)

Regras dos anéis e do placar, sem dependência de cena:

- Raios dos anéis em metros: 0,14 (10 pontos), 0,28 (8), 0,42 (6), 0,56 (4) e 0,70 (2). A borda pertence ao anel de dentro (distância igual a 0,14 vale 10).
- `int PontosDoAnel(float distancia)`: devolve 10, 8, 6, 4 ou 2, ou 0 acima de 0,70.
- `void Registrar(float distancia)`: soma os pontos ao `Total` e atualiza `UltimoPontos` e `UltimoRotulo` ("centro" para 10, "anel 2" a "anel 5" para 8 a 2). Distância acima de 0,70 equivale a `Errou()`.
- `void Errou()`: `UltimoPontos` fica 0, `UltimoRotulo` fica "Errou" e o total não muda.
- `string Texto`: duas linhas. A primeira é `ARCO ARCADE • n pontos` (`1 ponto` no singular). A segunda é `Último tiro: 10 (centro)`, `Último tiro: 6 (anel 3)` ou `Último tiro: Errou`. Antes do primeiro tiro, `Último tiro: nenhum`.

### `ArcheryDraw` (C# puro)

Conversão da puxada em velocidade:

- Puxada máxima 0,50 m, mínima de disparo 0,10 m.
- `float Limitar(float puxada)`: restringe a puxada a 0 a 0,50 m.
- `bool Dispara(float puxada)`: verdadeiro a partir de 0,10 m.
- `float Velocidade(float puxada)`: interpolação linear de 15 m/s (puxada 0,10) a 40 m/s (puxada 0,50). Valores acima de 0,50 valem como 0,50.

### `ArcheryRange` (componente)

Recebe do `InternshipDay` o arco (objeto e `XRGrabInteractable`), o visual do arco recurvo, a flecha carregada, o alvo e o texto do placar. Faz o seguinte:

1. **Pegador da corda.** Cria um `GameObject` pequeno com `Rigidbody` cinemático e `XRGrabInteractable`, no ponto de repouso da corda (o mesmo da flecha carregada). O pegador só pode ser agarrado enquanto o arco estiver selecionado por uma mão. Se o arco for solto durante a puxada, o pegador é solto sem tiro.
2. **Puxada.** A cada quadro com o pegador agarrado, projeta a posição dele no eixo do arco (para trás), limita a 0,50 m com `ArcheryDraw.Limitar` e descarta o desvio lateral. A flecha carregada e a corda visual acompanham o pegador. A corda visual passa a ser dois segmentos, das pontas das lâminas até o pegador, e no repouso coincide com a corda reta de hoje.
3. **Disparo.** Ao `selectExited` do pegador com interactor de verdade (não nulo), se `ArcheryDraw.Dispara` for verdadeiro, a flecha carregada é solta com a velocidade de `ArcheryDraw.Velocidade`, na direção +Z do arco, e as duas mãos recebem vibração curta (`HapticImpulsePlayer` acima de cada interactor). Abaixo de 0,10 m não há tiro e a flecha volta ao repouso.
4. **Disparo do PC.** Escuta `activated` do arco. Com argumento nulo (o caso do `DesktopPlayer`), dispara a 40 m/s a partir do arco. Com argumento de verdade (gatilho do Quest), ignora, para a flecha só sair pela corda.
5. **Recarga.** 0,35 s depois de cada disparo, uma flecha nova é montada sobre a corda (mantém o `ArcadeArrow` do `InternshipDay`).
6. **Placar.** Mantém um `ArcheryScore` e escreve `Texto` no `TextMesh`.

### `ArcheryArrow` (componente na flecha em voo)

A gravidade e o movimento são calculados pelo componente, sem `Rigidbody` dinâmico. A 40 m/s a flecha anda 0,8 m por passo de física, e o alvo tem 15 cm de espessura, então a colisão por evento pode ser perdida. A cada `FixedUpdate`:

- soma `Physics.gravity * Time.fixedDeltaTime` à velocidade e avança a posição;
- lança um raio do ponto anterior ao novo, ignorando triggers e as peças do próprio arco;
- gira a ponta para a direção da velocidade.

Ao acertar o alvo, calcula a distância do ponto de impacto ao centro no plano do alvo, chama `ArcheryScore.Registrar` e crava a flecha (parada, filha do alvo) por 4 s. Ao acertar qualquer outra coisa, ou depois de 5 s no ar, chama `Errou()`. Um tiro registra uma única vez.

### `InternshipDay`

Em `BuildLounge`, mantém a criação das peças e:
- remove o bloco do `activated` que dispara a flecha reta e a classe `ArcadeStar`;
- troca o colisor do alvo (esfera, que numa escala achatada vira uma bola de 0,7 m de raio) por uma caixa fina de 1,4 × 1,4 × 0,15 m;
- desenha sobre o alvo 5 discos concêntricos, sem colisor, com os raios dos anéis;
- adiciona o `ArcheryRange` e entrega as referências.

As medidas e posições do arco, do suporte, do alvo e do placar continuam as mesmas. O `DesktopPlayer` não muda.

### `DesktopWalkCheck`

A conferência do texto antigo ("ARCO ARCADE • 1 ponto") passa a ler o placar novo: o texto deve começar com `ARCO ARCADE • ` e trazer 2 pontos ou mais, já que a queda de 15 cm leva um tiro mirado no centro ao anel de 8.

## Verificação

- `ArcheryScoreCheck` (EditMode, batchmode), escrito antes das classes: bordas dos anéis (0,139 dá 10, 0,14 dá 10, 0,141 dá 8, 0,70 dá 2, 0,71 dá erro), total acumulado, `Errou` sem mexer no total, texto no singular e no plural, `ArcheryDraw` (0,09 sem tiro, 0,10 com tiro e 15 m/s, 0,30 com 27,5 m/s, 0,50 com 40 m/s, 0,80 limitado a 40 m/s).
- `ArcheryCheck` (Play, editor com janela que fecha sozinho), com relatório em `Logs/archery-check.txt`, escrito antes do componente. Usa um `XRDirectInteractor` de teste:
  - pega o arco, agarra o pegador, puxa 0,50 m e solta mirando o centro do alvo: a flecha pontua ao menos 8 e o placar mostra o total;
  - puxada de 0,05 m: nenhum tiro sai e a flecha continua no repouso;
  - puxada de 0,15 m: a flecha cai antes do alvo e o último tiro é `Errou`;
  - disparo do PC (`activated.Invoke(null)`) pontua, e `activated` com argumento de verdade não dispara;
  - soltar o arco no meio da puxada: sem tiro, pegador e corda de volta ao repouso;
  - a flecha a 40 m/s mirada no alvo não o atravessa: o impacto é registrado e o placar muda;
  - a flecha recarrega depois de 0,35 s.
- Continuam passando: `DesktopWalkCheck` (com o placar novo), `BowlingCheck`, `BowlingRoundCheck`, `NavigationCheck` e `HandDoorCheck`. APK gerado ao final.

O editor não prova o gesto de puxar a corda com a mão no Quest. Isso entra no teste no headset.

## Fora do escopo

Som, retículo de mira, rodadas de flechas, animação do arco recurvo envergando, ajuda de mira, mudança de medidas ou posições no `InternshipDay`, mudança no `DesktopPlayer`.

## Riscos

- `InternshipDay` continua sendo editado pelo Richardson. A mudança nele se limita a ligações em `BuildLounge`, mais a troca do colisor do alvo e os discos pintados.
- O pegador só ser agarrável com o arco na mão depende do `XRGrabInteractable.isSelected` do arco. O interactor de teste no XRI 3.0.11 precisa de `interactionManager` explícito. O `BowlingCheck` da Etapa 3 já resolve isso e serve de modelo.
- O raio por passo cobre o atravessamento, mas flechas muito rápidas encostando na borda do alvo podem dar um ponto de impacto na quina e cair no anel de fora. A caixa de 15 cm de espessura reduz esse efeito.
