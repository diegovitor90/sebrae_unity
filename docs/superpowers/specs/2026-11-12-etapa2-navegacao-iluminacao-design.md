# Etapa 2: locomoção livre, tetos, iluminação e porta

Data: 2026-11-12. Prazo: entrar no APK da apresentação da P2 (14/11).
Base: branch `feature/etapa2-navegacao-iluminacao`, criada sobre a Etapa 1 (`feature/etapa1-botoes-missoes`, PR #5).

## Contexto

O prédio original (prefab `SebraeLab_Ambiente`, 64 objetos estáticos vindos do FBX) existe na cena. A Sala 2, as galerias, a ala de atividades, a sala de descompressão e a praça são criadas por código no Play (`InternshipDay` e `LabPlaza`), com primitivas. Por isso essas áreas não recebem bake.

Pipeline: Built-in. Perfil de qualidade do Android: Medium (1 luz por pixel, MSAA desligado). A cena tem 1 luz direcional sem sombra e nenhum lightmap. O código cria 12 luzes pontuais em tempo real.

O rig do jogador (XR Origin do XRI 3.0.11) já traz movimento contínuo no controle esquerdo. A cena da missão desliga isso com um override (`m_SmoothMotionEnabled: 0` no `ControllerInputActionManager` do Left Controller).

## Decisões

| Tema | Decisão |
|---|---|
| Locomoção | Analógico esquerdo anda (contínuo, 2,5 m/s, com gravidade). Analógico direito gira 45° e teleporta. |
| Iluminação | Tempo real ajustado, sem bake, sem light probes. |
| Tetos | Todo ambiente coberto tem teto, inclusive a Galeria da praça. Praça e gramado ficam abertos. |
| Porta da Sala 2 | No Quest, segue a mão enquanto o grip está apertado. No PC, clique ou F mantém a animação. |

## Componentes

### Locomoção e pisos

- Reverter o override de `m_SmoothMotionEnabled` do Left Controller na cena `SebraeLab_MissaoGerente`. O Right Controller continua com teleporte e giro em passos.
- Pisos: todos já têm collider sólido (mesh do FBX ou `BoxCollider` das primitivas) e todas as camadas de física colidem entre si. Nada muda sem medição.
- Degrau do `CharacterController`: hoje 0,5 m, o que permite subir em bancos e sofás. O `NavigationCheck` mede o maior degrau real da rota. Se esse degrau for de até 0,25 m, o degrau do rig cai para 0,3 m por override na cena, sem editar o prefab do sample. Se for maior, fica em 0,5 m e o relatório indica onde está o degrau alto.

### Tetos

- `InternshipDay` chama `AddLitRoof` também para a Galeria da praça (piso em x 12 a 16, z -2 a 7), na altura das outras galerias.
- O `NavigationCheck` lista os trechos de piso coberto sem teto.

### Iluminação

1. Luz direcional: render mode Important, sem sombra.
2. Luzes pontuais criadas por `AddLitRoof` e as "Luz quente" da descompressão: `LightRenderMode.ForceVertex`.
3. Material `Resources/Acabamentos/LuminariaAcesa.mat` (Standard, emissão quente). `MissionSurfaces` aplica esse material às peças `Luminária`, no lugar de `Aco`.
4. `QualitySettings`, nível Medium: MSAA 4x.

### Porta (`HandDoor`)

Componente novo no objeto da dobradiça, com toda a lógica da porta que hoje fica no `InternshipDay`:

- `Angle` (0 a 95°), `IsOpen` (ângulo acima de 45°), `Toggle()`.
- `Bind(XRSimpleInteractable)`: ao `selectEntered` com um interactor de verdade, a porta passa a seguir o ângulo do transform do interactor em volta da dobradiça, a partir do ângulo em que estava. Ao `selectExited`, para onde estiver. Vibração curta ao pegar.
- `Toggle()` anima até o extremo oposto em 0,65 s, como a versão do Richardson. Partir de um ângulo intermediário não causa salto.
- `selectEntered` sem interactor (chamada do modo PC ou de validador) alterna a porta.

`InternshipDay` cria o `HandDoor`, liga a maçaneta, usa `IsOpen` no lugar de `DoorOpen` e repassa o F e o clique para `Toggle()`. A animação sai do `Update` do `InternshipDay`.

## Verificação

- Compilação na 6000.0.83f1 sem erros e sem avisos.
- `NavigationCheck` (novo, menu e arquivo de pedido em `Logs/`, grava `Logs/navigation-check.txt`):
  - em Play, cria um `CharacterController` com as medidas do rig (raio 0,1 m, altura 1,36 m, degrau e inclinação do rig) e anda por uma rota que passa por recepção, praça, Galeria da praça, Sala 2, ala de atividades, Galeria de descompressão e descompressão, e volta;
  - falha se o corpo travar ou cair abaixo de y = -0,5;
  - registra o maior degrau vencido;
  - sonda para cima a partir de pontos dos pisos cobertos e lista os que estão sem teto;
  - confere que o Left Controller está com movimento contínuo ligado;
  - anota draw calls e triângulos vistos de um ponto de cada ambiente (medida do editor, para comparação).
- `HandDoorCheck` (novo): um interactor simulado seleciona a maçaneta e gira 60° em volta da dobradiça. A porta deve acompanhar com tolerância de 5°, ficar parada ao soltar, e `Toggle()` deve continuar alternando.
- `DesktopWalkCheck` do Richardson continua passando do início ao fim.
- APK novo gerado ao final.

O editor não prova a sensação de andar com o analógico nem o FPS. No editor, o modo PC substitui o rig XR. Esses dois pontos entram no teste no Quest, com o OVR Metrics Tool para o FPS.

## Fora do escopo

Bake, light probes, reflection probes, porta com Hinge Joint, troca das primitivas pelos modelos `SM_*` e correção do `ExpedienteValidation`, que deixou de acompanhar o fluxo após o commit 57c97fd.

## Riscos

- A porta gira pelo transform. Se o jogador estiver no caminho, a porta atravessa o corpo dele em vez de empurrá-lo.
- `InternshipDay` continua sendo editado pelo Richardson. A mudança nele se limita a ligações; a lógica da porta sai para `HandDoor`.
- Luzes por vértice em primitivas grandes têm efeito sutil. A sensação de lugar iluminado vem das luminárias emissivas e da luz direcional.

## Ajustes após a revisão final

- Pegada de longe (raio) a mais de 0,45 m da maçaneta abre ou fecha a porta inteira em vez de seguir o controle.
- A missão da Sala 2 conclui com a porta aberta acima de 30° (`IsPassable`). `IsOpen` (45°) continua definindo a direção do Toggle.
- O ângulo da porta acumula quadro a quadro para não saltar ao contornar a dobradiça.
- O NavigationCheck usa altura de 1,75 m no corpo de teste e falha se o degrau do rig não for 0,3 m.
