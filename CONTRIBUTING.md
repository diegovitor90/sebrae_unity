# Como contribuir

Leia o [README](README.md), o [backlog](docs/ROADMAP.md) e o [guia de Git](docs/VERSIONAMENTO.md). Combine escopo e responsabilidade sobre cenas/prefabs. Use **Unity 6000.0.83f1**.

## Uma entrega por branch

Com seu trabalho local salvo e preservado:

~~~bash
git status
git switch main
git pull --ff-only
git switch -c feature/nome-da-entrega
~~~

Implemente, salve fora do Play e revise:

~~~bash
git status
git diff
~~~

Selecione os arquivos da entrega com git add, incluindo os .meta correspondentes. Depois:

~~~bash
git diff --cached
git diff --cached --check
git commit -m "feat: descrever a entrega"
git push -u origin feature/nome-da-entrega
~~~

Abra um PR para main e descreva comportamento e testes. Push envia a branch; merge integra à main. A exigência de aprovação depende das regras da branch.

## Cuidados com Unity

- Mova/renomeie assets pela janela Project e preserve .meta.
- Versione Assets, Packages e ProjectSettings.
- Não envie caches, APKs, ferramentas Android ou chaves.
- Alinhe mudanças de versão/pacotes com a equipe.
- Evite alterar exemplos oficiais sem necessidade.
- Divida interações em prefabs e combine quem edita cada cena.

## Pronto para revisão

- [ ] Critérios implementados ou limitações explicitadas.
- [ ] Sem referências ausentes na entrega.
- [ ] Console revisada e testes relevantes executados.
- [ ] Teste no editor diferenciado de teste no headset.
- [ ] Cena/commit da build anotados, quando aplicável.
- [ ] Documentação e status atualizados.
- [ ] Diff sem arquivos locais alheios à entrega.

Use o [roteiro de testes](docs/TESTES.md). Alterações somente de documentação não exigem recompilar o APK; confira os links, caminhos e comandos descritos.

Para bugs e novas tarefas, use os modelos de Issue e registre informações reproduzíveis.

Se o Unity alterar arquivos durante a importação, revise o diff antes de incluí-los. Não misture alterações automáticas ou locais sem relação com a entrega no mesmo commit.
