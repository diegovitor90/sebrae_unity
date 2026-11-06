# Missão 01 — A busca pelo gerente

[Documentação](README.md) · [Roteiro de testes](TESTES.md) · [Roadmap](ROADMAP.md)

**Estado:** implementada e verificada no Unity; APK desta missão e teste no Quest ainda pendentes.

## Abrir e testar
No Unity, pare o Play e escolha **Sebrae Lab > Missao 01 > 2 - Abrir missao do gerente**.
Cena: `Assets/SebraeLab/Scenes/SebraeLab_MissaoGerente.unity`.
Para teclado/mouse, ative o simulador pelo menu existente antes do Play.

1. Aproxime-se do gerente e aponte para CONVERSAR no painel; aperte o gatilho.
2. Avance a conversa e escolha ACEITAR MISSAO.
3. Siga as marcacoes azuis pelo corredor ate a sala sinalizada.
4. Entre na area indicada: aparece MISSAO CONCLUIDA.
5. O botao REINICIAR MISSAO permite repetir a experiencia.

O NPC e um modelo estilizado simples, salvo como prefab reutilizavel em `Assets/SebraeLab/Missions/Manager/Prefabs/Gerente.prefab`.
A cena de exploracao original foi preservada. As portas e moveis da base continuam estaticos.

## Regras
- Conversa permitida a ate 2,2 m do gerente, com linha de visao livre.
- Sair de perto pausa o avanco do dialogo; a conversa pode continuar ao se aproximar novamente.
- Marcacoes de percurso so aparecem depois do aceite.
- A conclusao verifica a posicao da cabeca do jogador dentro da sala; nao depende de encostar um controle no destino.
- Entrar na sala antes de aceitar nao conclui a missao.
- A conclusao e registrada uma unica vez por tentativa.
- O reinicio limpa progresso, marcacoes e painel de sucesso.

## Verificacao feita
O menu **Sebrae Lab > Missao 01 > 4 - Verificar fluxo da missao** executa verificacoes do fluxo em Play Mode e grava `Logs/manager-mission-check.json`.
As 11 verificacoes passaram, incluindo proximidade, bloqueio por parede, callbacks dos botoes, aceite, conclusao unica e reinicio.
O percurso recepcao-gerente e gerente-sala foi verificado na geometria com capsula de raio 0,22 m.
Isso verifica a logica e a montagem, sem substituir um teste de ponteiro/controles e locomocao no Quest real.

## APK e distribuicao
Esta entrega adiciona a missao ao projeto Unity. Os APKs existentes `SebraeLabVR.apk` e `SebraeLab-Ambiente.apk` foram gerados antes da missao e nao a incluem.
Para gerar um APK desta cena, use **Sebrae Lab > Missao 01 > 3 - Gerar APK da missao**.
Saida esperada: `Builds/Quest/SebraeLab-MissaoGerente.apk`.
A compilacao deste novo APK e a validacao no headset ainda estao pendentes nesta entrega.
O APK usa o mesmo identificador do aplicativo anterior; sua instalacao substitui a versao anterior no Quest.

## Escopo
Inclui apenas encontrar o gerente, conversar, receber o objetivo e chegar a sala.
Cafe, livros, cadeiras, notebooks, voz e animacoes de fala ficam para entregas futuras.
