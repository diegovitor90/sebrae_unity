# Como versionar este projeto no GitHub

[Documentação](README.md) · [Como contribuir](../CONTRIBUTING.md)

## 1. O que cada nome significa

| Termo | Significado neste projeto |
| --- | --- |
| Git | Programa que registra o historico no seu computador. |
| GitHub | Servico online onde a equipe compartilha esse historico. |
| Repositorio | Pasta do projeto e seu historico Git. A pasta oculta .git guarda o historico local. |
| Clone | Copia local de um repositorio, incluindo seu historico. |
| Working tree | Arquivos que voce esta editando no computador. |
| Staging / stage | Selecao das alteracoes que entrarao no proximo commit. |
| Commit | Registro local das alteracoes selecionadas, com mensagem, autor e identificador. |
| Branch | Linha de trabalho. A branch principal deste repositorio e main. |
| origin | Apelido local do repositorio remoto no GitHub. |
| Fetch | Atualiza a informacao local sobre o remoto, sem integrar mudancas nos seus arquivos. |
| Pull | Busca alteracoes remotas e as integra na branch local. |
| Push | Envia commits locais para o GitHub. |
| Pull request (PR) | Pedido para revisar e integrar uma branch a outra no GitHub. |
| Merge | Integra alteracoes entre branches. |

Salvar no Unity grava o arquivo. Fazer commit registra uma versao no Git local. Fazer push disponibiliza esses commits no GitHub. Sao etapas diferentes.

## 2. O que esta sendo versionado

Este repositorio guarda o projeto Unity na raiz:

- Assets/: cenas, modelos, materiais, scripts, prefabs e seus arquivos .meta.
- Packages/: manifesto e arquivo de versoes fixadas (packages-lock.json).
- ProjectSettings/: versao e configuracoes do projeto.
- Previews/: imagens para revisar o ambiente.
- docs/: instrucoes da equipe.
- README.md, .gitignore e .gitattributes.
- Archives/SebraeLabUnityAssets.unitypackage: pacote original preservado do historico anterior.

Library, Temp, Logs, UserSettings, Builds, SDK/NDK/JDK, APKs e chaves de assinatura nao entram no Git. O Unity recria seus caches. Builds podem ser distribuidas separadamente.

Um .meta identifica o asset e permite que Unity encontre suas referencias. Mova e renomeie assets pela janela Project do Unity e versiona sempre o asset junto com seu .meta. Esta configurado Visible Meta Files e serializacao Force Text.

O .gitignore ignora arquivos que ainda nao sao rastreados. Adicionar uma regra nao retira automaticamente um arquivo que ja entrou em um commit.

O .gitattributes define o tratamento de texto e de arquivos binarios; ele nao resolve sozinho conflitos de cenas Unity.

## 3. Primeira vez em outro computador

Instale Git e a versao do Unity indicada em ProjectSettings/ProjectVersion.txt (6000.0.83f1).
Em um terminal, na pasta onde deseja guardar seus projetos:

```powershell
git clone https://github.com/diegovitor90/sebrae_unity.git
cd sebrae_unity
git status
```

No Unity Hub, escolha adicionar projeto do disco e selecione sebrae_unity, a pasta que contem Assets, Packages e ProjectSettings. A importacao inicial pode demorar.

Configure sua identidade de autor uma vez, com seus dados:

```powershell
git config --global user.name "Seu nome"
git config --global user.email "seu-email-verificado-no-github"
```

Esses dados identificam seus commits; nao sao senha de acesso. Para privacidade, pode usar o endereco noreply mostrado nas configuracoes de email da sua conta GitHub.

Ao fazer push, o Git pode abrir o login do GitHub pelo gerenciador de credenciais. Nao coloque senhas ou tokens nos arquivos do projeto.

Se git nao for reconhecido no terminal, abra Git Bash ou reinicie o terminal depois de instalar o Git.

## 4. Rotina recomendada em equipe

Comece com os arquivos salvos e confira se ha alteracoes pendentes:

```powershell
git status
git switch main
git pull --ff-only
git switch -c feature/missao-gerente
```

O --ff-only atualiza apenas quando pode avancar sem criar um merge automaticamente. Se o comando recusar, examine o historico antes de continuar.

Trabalhe no Unity. Salve cenas e assets. Depois revise:

```powershell
git status
git diff
git diff --stat
```

Selecione os arquivos que pertencem a entrega. Exemplo para um arquivo de script existente e sua metadata:

```powershell
git add Assets/SebraeLab/Scripts/MeuScript.cs Assets/SebraeLab/Scripts/MeuScript.cs.meta
git diff --cached
git diff --cached --stat
git commit -m "feat: adicionar conversa inicial com o gerente"
git push -u origin feature/missao-gerente
```

Substitua os caminhos pelos arquivos que voce realmente alterou. O --cached mostra exatamente a selecao do proximo commit. O -u associa sua branch local a branch remota; nos proximos envios da mesma branch, basta git push.

Se voce revisou todas as alteracoes e quer incluir todas elas, git add -A e uma alternativa. Esse comando tambem seleciona exclusoes, portanto confira git status e git diff --cached antes do commit.

No GitHub, abra um pull request de feature/missao-gerente para main. Descreva o que mudou e como foi testado. Push nao e aprovacao e nao integra automaticamente a main. No PR, Approve registra uma revisao favoravel; Merge pull request integra as alteracoes. A obrigatoriedade de aprovacao depende das regras de protecao da branch configuradas no repositorio.

Depois da revisao e do merge:

```powershell
git switch main
git pull --ff-only
```

Para outra entrega, crie uma nova branch com outro nome.

## 5. Comandos para consultar o historico

```powershell
git status
git log --oneline -10
git show --stat HEAD
git branch -a
git remote -v
git diff
git diff --cached
```

- git diff: alteracoes ainda nao selecionadas.
- git diff --cached: alteracoes selecionadas para o proximo commit.
- git show --stat HEAD: resumo do commit mais recente.
- Working tree clean significa que os arquivos rastreados estao iguais ao ultimo commit. Para saber se os commits foram enviados, confira tambem a relacao com origin/main ou com a branch remota correspondente.

## 6. Como corrigir erros comuns

### Selecionei um arquivo errado, mas ainda nao fiz commit

```powershell
git restore --staged caminho/do/arquivo
```

Isso retira o arquivo do staging e preserva sua edicao no computador.

### Fiz commit, mas ainda nao enviei

Ainda esta apenas no computador. Confira git log --oneline -3 e git status. Se precisar mudar o conteudo ou a mensagem, revise primeiro se aquele commit foi compartilhado antes de reescrever historico.

### O push foi recusado porque outra pessoa enviou antes

Nao use push --force. Primeiro busque informacoes e veja as diferencas:

```powershell
git fetch origin
git status
git log --oneline --graph --decorate --all -15
```

A integracao depende de quais branches mudaram. Resolva o caso com os arquivos salvos e confira o resultado no Unity.

### Houve conflito em uma cena ou prefab

Nao aceite tudo de um lado sem verificar. Combine com o colega quem editara a cena, preserve as mudancas necessarias e teste a cena depois da resolucao.

Dividir objetos em prefabs e evitar editar a mesma cena ao mesmo tempo reduz conflitos. Modelos FBX, texturas e outros binarios nao sao combinados como texto; a equipe precisa escolher ou refazer a versao adequada.

### Quero desfazer uma versao que ja foi compartilhada

Normalmente use git revert HASH_DO_COMMIT, que cria um novo commit invertendo aquele conjunto de alteracoes, e revise o resultado antes do push. Nao use reset --hard ou push --force sem entender quais alteracoes e historico serao descartados.

## 7. Como escolher um bom commit

Um commit deve representar uma mudanca coerente, que possa ser explicada e testada. Exemplos:

- feat: montar ambiente do Sebrae Lab
- feat: configurar teletransporte no Quest
- fix: corrigir colisao na porta da recepcao
- docs: explicar como abrir e testar o projeto

O prefixo e uma convencao de organizacao; o mais importante e a mensagem descrever a entrega. Evite mensagens como "coisas", "final" ou "agora vai".

Antes de encerrar o dia: salve no Unity, revise os arquivos, faca commit do trabalho coerente e envie sua branch. Arquivos ainda nao commitados ou commits ainda nao enviados nao estao disponiveis para os colegas no GitHub.
