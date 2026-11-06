# Testes e validação

## Evidência já registrada

| Área | Verificação realizada | Limite |
| --- | --- | --- |
| Ambiente | Play Mode, prévias inspecionadas, materiais/scripts presentes e ponto inicial sobre piso livre | Não confirma conforto ou escala percebida |
| Sala de testes | APK ARM64 compilado e assinatura verificada | Sem validação registrada no Quest |
| Ambiente standalone | APK ARM64 compilado e assinatura verificada; 51.567.489 bytes | Não contém a missão |
| Missão do gerente | 11 verificações de fluxo em Play Mode aprovadas | Não substitui ponteiro/controles físicos |
| Percurso | Recepção-gerente e gerente-sala verificados com cápsula de raio 0,22 m | Precisa ser percorrido com os controles reais |

Logs são locais, fora do Git. Registre na issue/PR a evidência necessária para outra pessoa entender o que foi testado.

## Verificação do fluxo da missão

1. Pare o Play e preserve seu trabalho.
2. Use **Sebrae Lab → Missao 01 → 4 - Verificar fluxo da missao**.
3. A ferramenta abre a cena, entra em Play, verifica e sai.
4. Leia **Logs/manager-mission-check.json**: passed deve ser true e error vazio.

O verificador controla posições de cabeça e invoca callbacks dos botões. Confere:

1. Componente da missão carregado.
2. Conversa bloqueada à distância.
3. Entrada antes do aceite sem conclusão indevida.
4. Conversa bloqueada por parede.
5. Botão inicia diálogo.
6. Afastamento bloqueia avanço.
7. Aceite ativa objetivo e marcações.
8. Nova conversa não reinicia missão aceita.
9. Permanecer fora da sala não conclui.
10. Entrada conclui uma vez e mostra sucesso.
11. Reinício limpa progresso e marcações.

Não é um teste automatizado de ponta a ponta dos controles VR. Não há pipeline no GitHub Actions executando essa rotina.

## Teste manual no computador

- [ ] Cena correta, simulador ativo e Play iniciado.
- [ ] Cabeça e dois controles simulados respondem.
- [ ] Gerente e instruções identificáveis.
- [ ] Botão apontado e acionado manualmente.
- [ ] Texto legível e dentro dos painéis.
- [ ] Percurso não exige atravessar paredes.
- [ ] Conclusão e reinício funcionam.
- [ ] Console sem novos erros relevantes.

## Teste manual no Quest

Anote o dispositivo e a build. Os APKs antigos não contêm a missão do gerente.

- [ ] Instalação e abertura standalone.
- [ ] Rastreamento de cabeça e dois controles.
- [ ] Altura, proporções e piso percebidos como corretos.
- [ ] Teletransporte e giro compreensíveis/confortáveis.
- [ ] Paredes e móveis não permitem destinos indevidos.
- [ ] Painéis legíveis e botões alcançáveis pelo gatilho.
- [ ] Missão completa da recepção à sala, se incluída na build.
- [ ] Sem contagem duplicada ou conclusão antecipada.
- [ ] Reinício permite repetir.
- [ ] Desempenho medido, com método e resultado anotados.
- [ ] Sessão com visitante que não conhece os controles.

Uma captura do editor não comprova execução no Quest.

## Registrar resultado

~~~text
Data e responsável:
Commit/branch:
Cena e APK:
Versão do Unity:
Dispositivo ou simulador:
Passos executados:
Resultado esperado:
Resultado observado:
Erros novos:
Método e medição de desempenho, quando realizada:
Pendências:
~~~

Não marque um teste como aprovado apenas porque o modelo ou a cena existe.
