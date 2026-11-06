# Roadmap e backlog

O quadro descreve entregas observáveis, sem porcentagens de esforço. Um modelo disponível ainda pode exigir toda a lógica de interação.

## Prioridade imediata

| Prioridade | Entrega | Critério de conclusão |
| --- | --- | --- |
| P0 | Gerar APK da missão do gerente | Build da cena correta concluída e identificada pelo commit |
| P0 | Validar ambiente e missão no Quest | Roteiro executado com controles reais, resultado e problemas registrados |
| P0 | Corrigir falhas encontradas | Falha reproduzida, corrigida e cenário afetado testado novamente |
| P1 | Distribuir versão de teste | Release com APK, cena/commit, instruções e limitações |

## 1. Busca pelo gerente

**Implementado no Unity:** NPC estilizado, diálogo, aceite, percurso, chegada, conclusão única e reinício. As 11 verificações de fluxo passaram.

**Falta:** gerar o APK e validar seleção, leitura, alcance, locomoção e conforto no headset.

## 2. A própria sala

**Implementado:** localização, área de chegada e conclusão integradas à primeira missão.

**Falta:** maçaneta e porta interativas. Critérios:

- Seleção da maçaneta compreensível com o controle.
- Movimento de abertura com limites definidos.
- Passagem atravessável sem prender o jogador.
- Atualização do objetivo uma única vez, na etapa correta.

Boliche e arco e flecha são atividades planejadas. Confirmar se fazem parte da entrega principal ou são extras antes de estimar.

## 3. Mural do Conhecimento

**Pendente.** Critérios:

- Selecionar o mural abre painel legível em VR.
- Conteúdo sobre espaços/serviços definido pela equipe.
- “Entendido” fecha e conclui a etapa uma única vez.
- A interface não bloqueia permanentemente a locomoção.

## 4. Café de sobrevivência

**Disponível:** copa do ambiente. **Pendente:** copo/xícara, café/cafeteira conforme o escopo e lógica.

Critérios:

- Objeto que pode ser pego claramente identificado.
- Grip permite pegar/soltar sem atravessar superfícies.
- Pegar o copo correto na etapa correta registra a conclusão.
- Existe recuperação para um copo derrubado fora de alcance.

## 5. O faz-tudo

Endireitar cadeiras é uma tarefa desta missão, não uma sexta missão.

| Tarefa | Aproveitável | Falta |
| --- | --- | --- |
| Guardar livros | Ambiente de coworking | Livros, estante utilizável, pontos de colocação e validação |
| Endireitar cadeiras | Modelos existentes | Manipulação, posição/orientação válidas e confirmação |
| Posicionar notebooks | Ambiente e salas | Notebooks, pontos nas mesas, colocação e contagem |

Critérios comuns:

- Só objetos corretos nos destinos corretos avançam a tarefa.
- Uma mesma entrega não é contada várias vezes.
- Retorno visual e possibilidade de corrigir uma colocação errada.
- Aviso final ao concluir todas as tarefas exigidas, incluindo o último notebook.
- Reinício restaura itens e contadores.

## Integração final

- Encadear as cinco etapas em uma sessão.
- Definir introdução, instruções e mensagem final.
- Testar com alguém que não conhece o projeto.
- Medir desempenho, conforto, leitura e duração no Quest.
- Registrar o conteúdo de cada versão distribuída.

## Organizar uma tarefa

Abra uma issue com objetivo, escopo, dependências e critérios de aceite. Confirme o responsável com a equipe. Implemente em uma branch e associe o PR à issue. Use o modelo de entrega disponível nas Issues.
