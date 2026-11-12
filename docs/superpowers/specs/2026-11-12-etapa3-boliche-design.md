# Etapa 3: boliche com física, rodadas e placar

Data: 2026-11-12. Prazo: entrar no APK da apresentação da P2 (14/11).
Base: branch `feature/etapa3-boliche`, criada sobre a Etapa 2 (`feature/etapa2-navegacao-iluminacao`, PR #6).

## Contexto

O boliche fica na sala de descompressão e é montado por código em `InternshipDay.BuildLounge` (commit 57c97fd do Richardson). Medidas atuais, que não mudam:

| Peça | Posição e tamanho |
|---|---|
| Pista | centro (45; 0,04; 1), tamanho (2; 0,08; 9): x 44 a 46, z -3,5 a 5,5, superfície em y 0,08 |
| Calhas | centros em x 45 ± 1,24, de z -3,5 a 5,5, fundo em y -0,15 |
| Pinos | 10 cápsulas de 0,16 × 0,48 m em `(45 + (c - r·0,5)·0,27; 0,32; 3 + r·0,27)`, r de 0 a 3, c de 0 a r |
| Bola | esfera de 0,31 m de diâmetro no suporte em (45; 0,85; -3,8) |
| Piso de lançamento | x 43,52 a 46,48, z -8 a -3,5 |
| Fundo atrás dos pinos | piso em x 43,52 a 46,48, z 5,5 a 8, parede em z 8 |
| Placar | texto em (45; 2,2; 5,4) |
| Botão REARMAR BOLICHE | estação em (43; 1; -3) |

Hoje não há material físico em nenhum objeto, a bola tem a massa padrão (1 kg) e os pinos 0,22 kg. O placar conta a cada quadro os pinos inclinados mais de 35° e mostra "BOLICHE • x / 10". Não há rodadas, triggers nem devolução automática da bola. No modo PC, o `DesktopPlayer` coloca a bola na pista e a lança reta a 8 m/s. O `DesktopWalkCheck` confere o texto do placar.

## Decisões

| Tema | Decisão |
|---|---|
| Placar | Rodada de até 2 bolas, com STRIKE e SPARE, pontos da rodada e total da sessão. Sem a contagem oficial de 10 frames. |
| Arremesso no Quest | Ajuda leve: apoiar na pista, puxar a direção até 10°, velocidade entre 3 e 12 m/s, só ao soltar na área de lançamento. |
| Detecção | Triggers para os eventos da jogada; pino derrubado por inclinação acima de 35° ou deslocamento acima de 15 cm. |
| Código | Componente `BowlingLane`; o `InternshipDay` continua montando as peças e só entrega as referências. |

## Componentes

### `BowlingRound` (C# puro)

Regras da rodada, sem dependência de cena:

- `int Rodada`, `int Bola` (1 ou 2), `int PinosNestaBola`, `int PontosDaRodada`, `int Total`, `string Destaque` ("STRIKE", "SPARE", "CALHA" ou vazio), `bool RodadaEncerrada`.
- `void RegistrarBola(int pinos, bool calha)`: pinos é a quantidade derrubada nesta bola, de 0 a 10. Na bola 1, 10 pinos marcam STRIKE e encerram a rodada. Na bola 2, se a soma das duas bolas for 10, marca SPARE; a rodada encerra depois da bola 2. Calha com 0 pinos marca CALHA. Os pontos da rodada são a soma dos pinos derrubados, sem bônus. O total soma os pontos ao encerrar a rodada.
- `void ProximaRodada()`: avança para a rodada seguinte, bola 1.
- `void RecomecarRodada()`: volta à bola 1 da rodada atual, descarta os pinos da rodada e não mexe no total. Se a rodada já estava encerrada (os pontos dela já entraram no total), avança para a próxima, para não contar a mesma rodada duas vezes.

### `BowlingLane` (componente)

Recebe do `InternshipDay` a pista, as calhas, os pinos com as posições de origem, a bola com a posição do suporte e o placar. Faz o seguinte:

1. **Física.** Aplica os materiais de `Resources/Fisica/`: `Pista` na pista e nas calhas (atrito dinâmico e estático 0,05, combinação mínima, quique 0), `Bola` na bola (atrito 0,2, quique 0) e `Pino` nos colisores dos pinos (atrito 0,4, quique 0,3). Massa da bola 6 kg, massa dos pinos 1,5 kg, rotação máxima da bola 60 rad/s, interpolação ligada na bola.
2. **Triggers.** Cria, a partir das medidas da pista:
   - Área de lançamento: x 43,52 a 46,48, z -6 a -2 (piso de lançamento e o primeiro 1,5 m da pista), de y 0 a 2.
   - Calha esquerda e calha direita: sobre cada calha, de z -3,5 a 5,5, de y -0,2 a 0 (abaixo da superfície da pista, para a bola que rola rente à borda não contar como calha).
   - Fundo: x 43,52 a 46,48, z 5,2 a 8, de y -0,2 a 1 (a bola que corre na calha para no degrau em z 5,5 e precisa entrar nele).
3. **Ajuda no arremesso.** Ao `selectExited` da bola com um interactor de verdade (não nulo) e com a bola dentro da área de lançamento, no `FixedUpdate` seguinte:
   - se houver superfície a até 0,6 m abaixo da bola (raio para baixo, ignorando triggers e a própria bola), posiciona a bola apoiada nela (y = ponto atingido + raio) e zera a velocidade vertical; o piso de lançamento fica 8 cm abaixo da pista, por isso o apoio usa a superfície real embaixo da bola;
   - limita o ângulo da velocidade horizontal a 10° do eixo +z da pista;
   - limita a velocidade horizontal entre 3 e 12 m/s;
   - envia uma vibração curta ao controle que soltou (o `HapticImpulsePlayer` acima do interactor).
   Soltar fora da área de lançamento, ou pelo modo PC (que não passa interactor), não aciona a ajuda.
4. **Ciclo da jogada.**
   - Pronta: bola no suporte ou na mão.
   - Rolando: depois de solta, a bola passa de 0,5 m/s. Durante a jogada, o placar mostra ao vivo quantos pinos estão derrubados nesta bola.
   - Assentando: a bola entrou no trigger do fundo, ou ficou abaixo de 0,1 m/s por 1 s, ou passaram 8 s desde que foi solta.
   - Contagem: quando todos os pinos dormem (`Rigidbody.IsSleeping`) ou no máximo 3 s depois de assentar, conta como derrubado nesta bola o pino ativo que estava em pé no início da jogada e agora está inclinado mais de 35° ou a mais de 15 cm da posição de origem no plano horizontal. Calha vale se a bola tocou um trigger de calha nesta jogada.
   - Depois da contagem: `BowlingRound.RegistrarBola`, devolve a bola ao suporte parada e, se a rodada continuar, desativa os pinos derrubados (varredura). Se a rodada encerrar, 2 s depois os 10 pinos voltam em pé, ativos, e `ProximaRodada` é chamado.
   - Pinos derrubados fora de uma jogada não contam: na jogada seguinte eles já não estão em pé no início, e o REARMAR os levanta.
5. **Placar.** Duas linhas no texto existente:
   `BOLICHE • RODADA n • BOLA b` (ou o destaque STRIKE, SPARE ou CALHA por 2 s no lugar de "BOLICHE")
   `Nesta bola: x   Rodada: y   Total: z`
6. **Rearme manual.** `Rearmar()`: devolve a bola, levanta e ativa os 10 pinos, chama `RecomecarRodada` e cancela a jogada em andamento. O botão REARMAR BOLICHE chama `Rearmar()`.

### `InternshipDay`

Em `BuildLounge`, mantém a criação das peças e:
- remove a contagem por ângulo que roda a cada quadro e o texto antigo do placar;
- adiciona o `BowlingLane` e entrega as referências;
- liga o botão REARMAR BOLICHE a `Rearmar()`.

### `DesktopWalkCheck`

A conferência do texto antigo ("BOLICHE • n / 10") passa a ler o placar novo: o número de "Nesta bola" deve ser igual aos pinos derrubados que o teste conta.

## Verificação

- `BowlingRoundCheck` (EditMode, batchmode), escrito antes do código: strike, spare, rodada aberta, calha, bola 2 após strike não existe, rearme sem perder o total, total somando três rodadas.
- `BowlingCheck` (Play, editor com janela que fecha sozinho), com relatório em `Logs/bowling-check.txt`:
  - materiais e massas aplicados à pista, às calhas, à bola e aos pinos;
  - arremesso simulado por um `XRDirectInteractor` de teste soltando a bola 0,5 m acima do início da pista, com velocidade inclinada 25° e 2 m/s: depois da ajuda, a bola está apoiada na pista, com direção a até 10° do eixo e velocidade entre 3 e 12 m/s;
  - arremesso reto a 8 m/s pelo centro: ao menos 1 pino derrubado registrado, bola de volta ao suporte, rodada na bola 2 com os derrubados desativados (ou rodada seguinte se for strike);
  - arremesso direto na calha: destaque CALHA e 0 pinos;
  - REARMAR: 10 pinos em pé e ativos, bola 1, total igual ao de antes.
- Continuam passando: `DesktopWalkCheck`, `NavigationCheck` e `HandDoorCheck`. APK gerado ao final.

O editor não prova o gesto de arremessar com a mão no Quest. Isso entra no teste no headset.

## Fora do escopo

Contagem oficial de 10 frames com bônus, som, animação de máquina repondo pinos, mudança no tamanho da pista e mudança na ajuda do modo PC.

## Riscos

- `InternshipDay` continua sendo editado pelo Richardson. A mudança nele se limita a ligações em `BuildLounge`.
- Física de pinos depende da taxa de física: com o passo padrão (0,02 s), arremessos muito rápidos podem atravessar pinos. A colisão contínua da bola reduz isso, e o limite de 12 m/s também.
