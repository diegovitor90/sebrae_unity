# Sebrae Lab VR

Projeto Unity 6000.0.83f1 para Meta Quest 3 / 3S.

## Abrir o ambiente
Clone o repositorio e adicione a pasta que contem Assets, Packages e ProjectSettings no Unity Hub. Depois usar:
**Sebrae Lab > Ambiente > 2 - Abrir ambiente**.

Cena principal: `Assets/SebraeLab/Scenes/SebraeLab_Exploracao.unity`.
Cena de testes anterior: `Assets/SebraeLab/Scenes/VR_Test.unity`.

## Primeira missao: buscar o gerente
Cena: `Assets/SebraeLab/Scenes/SebraeLab_MissaoGerente.unity`.
Abrir pelo menu **Sebrae Lab > Missao 01 > 2 - Abrir missao do gerente**.
Inclui NPC estilizado, conversa por texto, aceite, percurso sinalizado, chegada a sala, conclusao e reinicio.
As verificacoes de fluxo em Play Mode passaram; a compilacao do novo APK e o teste desta missao no Quest ainda estao pendentes.
Os APKs anteriores continuam sendo versoes da sala de testes e do ambiente sem esta missao.
Instrucoes completas em [docs/MISSAO_GERENTE.md](docs/MISSAO_GERENTE.md).

## O que foi montado
- 27 modelos FBX e 4 texturas originais importados com GUIDs preservados.
- Layout montado a partir de Sebrae Lab.fbx, com mobiliario em objetos separados.
- 24 elementos de exportacao/catalogo retirados da cena; arquivos originais preservados.
- Escala original mantida: aproximadamente 1 unidade Unity por metro. Geladeira de referencia: 1,72 m.
- Origem do ambiente centralizada em X. Jogador na recepcao, com rastreamento relativo ao piso.
- Hierarquia: Arquitetura, Mobiliario, Decoracao e luminarias, Colisoes e navegacao.
- Materiais de paredes, piso, madeira, metal, vidro e trim sheet; remapeados tambem nos modelos de origem.
- Texturas Android em ASTC 6x6, com mipmaps; atlas 2048 e demais texturas ate 1024.
- Iluminacao ambiente simples e luz direcional, sem sombras em tempo real.
- Prefab reutilizavel: `Assets/SebraeLab/Environment/Prefabs/SebraeLab_Ambiente.prefab`.

## Colisoes e locomocao
A base foi separada em dois colliders: estrutura e piso navegavel.
O piso utiliza somente triangulos horizontais voltados para cima na altura zero (178 triangulos).
Paredes, portas e mobiliario possuem colisao de malha estatica, preservando aberturas da geometria.
O piso usa a interaction layer 31, Teleport, e o jogador utiliza teletransporte e giro de 45 graus.
Os moveis ainda sao estaticos; tornar uma cadeira ou porta interativa exige configurar a fisica e a interacao especificas daquela missao.

## Validacao
- Cena de exploracao abriu em Play Mode com duas maos simuladas, sem excecoes de execucao nessa verificacao inicial.
- APK do ambiente compilado com sucesso e assinatura verificada; 51.567.489 bytes, ARM64.
- Previa interna e planta renderizadas e inspecionadas em `Previews/`.
- Posicao inicial: sobre piso navegavel e sem obstaculos em uma capsula de raio 0,20 m.
- Zero scripts ausentes e zero referencias de material vazias na cena.
- Relatorio local: `Logs/environment-validation.json`.
- Conforto, desempenho, escala percebida e trajetos completos precisam ser validados no Quest real.

## Teste no computador
Usar **Sebrae Lab > 4 - Alternar simulador PC**, depois Play.
O simulador esta desativado por padrao e marcado EditorOnly, sendo excluido da build.
Os controles sao os do XR Device Simulator nos Samples do XR Interaction Toolkit.
Desativar o simulador antes de usar um headset conectado via PC.

## APKs
- Sala de testes anterior: `Builds/Quest/SebraeLabVR.apk`.
- Ambiente: `Builds/Quest/SebraeLab-Ambiente.apk`, gerado pelo menu **Sebrae Lab > Ambiente > 5 - Gerar APK do ambiente**.
- Ambos usam o identificador `com.sebraelab.missaoestagio`; instalar um substitui a versao do outro no headset.

## Ferramentas Android nesta maquina
Android Build Support instalado no Unity.
SDK (API 35 / build tools 36), NDK r27c e OpenJDK 17 extraidos em `../../work/android-toolchain`.
O comando Preparar base VR usa esses caminhos quando existem; em outro computador, instalar as ferramentas e configurar External Tools.
A pasta work e local e nao deve ser enviada ao repositorio.

## Controle de versao
Versionar Assets, Packages, ProjectSettings e arquivos .meta.
Library, Temp, Logs, UserSettings e Builds ficam fora do Git.
Repositorio compartilhado: https://github.com/diegovitor90/sebrae_unity

Guia para aprender a versionar: [docs/VERSIONAMENTO.md](docs/VERSIONAMENTO.md).
