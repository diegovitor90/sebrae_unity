# Abrir, testar e executar

## Requisitos

- Git, Unity Hub e **Unity 6000.0.83f1**, conforme [ProjectVersion.txt](../ProjectSettings/ProjectVersion.txt).
- Para gerar APK: Android Build Support, SDK/NDK e OpenJDK compatíveis com essa instalação do Unity.
- Para teste real: Quest 3/3S, controles e cabo USB com transferência de dados.

As ferramentas Android não acompanham o repositório. O teste pelo simulador pode ser feito antes da instalação no headset.

## Primeira abertura

~~~bash
git clone https://github.com/diegovitor90/sebrae_unity.git
cd sebrae_unity
~~~

No Unity Hub, adicione a raiz do clone: a pasta com Assets, Packages e ProjectSettings. Não abra somente o pacote .unitypackage.

Espere a importação e a compilação. Se o Input System solicitar ativação e reinicialização, preserve seu trabalho e siga a orientação do editor. Confira a Console se houver erro.

Abra **Sebrae Lab → Missao 01 → 2 - Abrir missao do gerente**, ou dê dois cliques em Assets/SebraeLab/Scenes/SebraeLab_MissaoGerente.unity.

## Testar no computador

1. Pare o Play, se o botão central estiver azul e mostrar um quadrado.
2. Ative **Sebrae Lab → 4 - Alternar simulador PC**, caso o simulador esteja desativado.
3. Aperte **▶ Play** e clique dentro da aba Game.
4. Siga as instruções do painel XR Device Simulator para selecionar e movimentar cabeça e controles.
5. Clique no botão azul para parar. O atalho é **Ctrl + P**.

“Alternar” liga ou desliga: não acione novamente se o simulador já estiver ativo. O objeto **Simulador PC - excluido da build** aparece apagado na Hierarchy quando está inativo.

### Não encontrei o menu

Sebrae Lab fica na barra superior, junto de File, Edit, Assets e Window. Não é a pasta Assets.

Alternativa: pare o Play, selecione o objeto do simulador na Hierarchy e marque a caixa de ativação ao lado do nome no Inspector. Para exibir o painel, abra **Window → General → Inspector**.

Se o menu não aparecer, aguarde a compilação e verifique os erros da Console.

### A imagem ficou ampliada ou apontando para o chão

- Reduza **Scale** na aba Game para aproximadamente **1x**. Isso altera o zoom da janela, não o tamanho do modelo.
- Pare e reinicie o Play para restaurar o teste.
- Abra a cena, em vez do modo de edição do prefab SebraeLab_Ambiente.
- O asterisco no nome da cena indica alteração não salva; ativar um objeto também pode causá-lo. Revise antes de salvar.

Alterações durante o Play normalmente são descartadas ao parar. Edite e salve as entregas fora do Play.

## Gerar o APK correto

Prepare Android Build Support e dependências pelo Unity Hub. Confira os caminhos em **Edit → Preferences → External Tools**.

O helper VRProjectSetup.ConfigureToolchain procura **../../work/android-toolchain** quando essa pasta existe. Ela foi usada na máquina original de desenvolvimento; não é um requisito para quem clonou o projeto. Configure suas próprias ferramentas.

| Entrega | Menu | Arquivo em Builds/Quest/ |
| --- | --- | --- |
| Sala de testes | Sebrae Lab → 3 - Gerar APK Quest | SebraeLabVR.apk |
| Ambiente | Sebrae Lab → Ambiente → 5 - Gerar APK do ambiente | SebraeLab-Ambiente.apk |
| Missão do gerente | Sebrae Lab → Missao 01 → 3 - Gerar APK da missao | SebraeLab-MissaoGerente.apk |

Os comandos de montar/preparar e de build podem abrir cenas e salvar configurações. Preserve seu trabalho antes de executá-los.

Confira a Console e a validação OpenXR na aba Android. Para a missão, execute também a [verificação de fluxo](TESTES.md#verificação-do-fluxo-da-missão). O APK da missão ainda precisa ser gerado.

Configuração preparada: com.sebraelab.missaoestagio, ARM64, IL2CPP, Vulkan, Android mínimo 32 e alvo 35.

## Instalar no Quest

1. Ligue o headset e termine a configuração inicial.
2. Habilite o modo desenvolvedor pelo fluxo da Meta, se necessário.
3. Conecte um cabo USB de dados e autorize a depuração no headset apenas para um computador reconhecido.
4. Consulte os dispositivos pelo ADB:

~~~bash
adb devices -l
~~~

5. Quando o dispositivo estiver autorizado e em estado **device**, instale o APK escolhido:

~~~bash
adb install -r "Builds/Quest/SebraeLab-Ambiente.apk"
~~~

Para a missão, troque o nome pelo APK correspondente depois de gerá-lo. Se adb não estiver no PATH, use o caminho completo até SDK/platform-tools/adb da sua instalação.

6. Abra o aplicativo pelo acesso a aplicativos de desenvolvimento/fontes desconhecidas disponível no headset e siga o [roteiro de testes](TESTES.md#teste-manual-no-quest).

Sem dispositivo na lista: confira cabo, conexão e modo desenvolvedor. Com **unauthorized**: confira a autorização no óculos.

Conectar o USB não instala o aplicativo automaticamente. A build standalone roda no próprio Quest. Apertar Play no Unity não a envia ao headset. PC VR via Quest Link é outro fluxo e não foi validado nesta entrega; desative o simulador ao usar um headset real no editor.

## Distribuição

APKs ficam fora do Git. Publique builds aprovadas em GitHub Releases com cena, commit de origem, testes e limitações. Ainda não há Release publicada.
