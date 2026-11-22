using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using SebraeLab.Missions;

namespace SebraeLab.Editor
{
    /// <summary>
    /// Teste em EditMode das regras da trilha no prédio: onde o feixe acende em cada estado, quando a seta aparece e
    /// se cada peça da planta fica na sala certa. Resultado em Logs/trilha-regras-check.txt.
    /// </summary>
    public static class TrilhaRegrasCheck
    {
        const string Report = "Logs/trilha-regras-check.txt";

        [MenuItem("Sebrae Lab/Verificação/Regras da trilha")]
        static void Menu() => Debug.Log("TrilhaRegrasCheck: " + (Run() == 0 ? "OK" : "FALHA") + ". Detalhes em " + Report);

        /// <summary>Para -executeMethod: fecha o Unity com 0 (ok) ou 1 (falha).</summary>
        public static void RunFromCommandLine() => EditorApplication.Exit(Run() == 0 ? 0 : 1);

        public static int Run()
        {
            var texto = new StringBuilder();
            int falhas = 0;
            void Check(bool ok, string mensagem)
            {
                if (!ok) falhas++;
                texto.AppendLine((ok ? "OK    " : "FALHA ") + mensagem);
            }
            bool Igual(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .001f;
            EstadoDaTrilha Estado(ManagerMissionState gerente, EtapaExpediente etapa) => new EstadoDaTrilha { Gerente = gerente, Etapa = etapa };
            const ManagerMissionState Concluido = ManagerMissionState.Completed;

            texto.AppendLine("== Feixe");
            Check(Igual(GuiaDaTrilha.Destino(Estado(ManagerMissionState.FindManager, EtapaExpediente.Sala)), PlantaDaTrilha.FeixeDoGerente),
                "procurando o gerente: feixe diante do gerente");
            Check(Igual(GuiaDaTrilha.Destino(Estado(ManagerMissionState.Dialogue, EtapaExpediente.Sala)), PlantaDaTrilha.FeixeDoGerente),
                "na conversa: feixe continua diante do gerente");
            Check(Igual(GuiaDaTrilha.Destino(Estado(ManagerMissionState.FindRoom, EtapaExpediente.Sala)), PlantaDaTrilha.FeixeDaPortaDaSala2),
                "procurando a sala: feixe na porta da Sala 2");
            Check(Igual(GuiaDaTrilha.Destino(Estado(Concluido, EtapaExpediente.Sala)), PlantaDaTrilha.FeixeDaPortaDaSala2),
                "sala concluída, etapa ainda Sala: feixe fica na porta");
            Check(Igual(GuiaDaTrilha.Destino(Estado(Concluido, EtapaExpediente.Mural)), PlantaDaTrilha.FeixeDoMural), "mural: feixe diante do mural");
            Check(Igual(GuiaDaTrilha.Destino(Estado(Concluido, EtapaExpediente.Cafe)), PlantaDaTrilha.FeixeDaBancada), "café ainda na bancada: feixe na bancada");
            var cafe = Estado(Concluido, EtapaExpediente.Cafe);
            cafe.CafeNaMao = true;
            Check(Igual(GuiaDaTrilha.Destino(cafe), PlantaDaTrilha.FeixeDoPires), "café na mão: feixe diante do pires");

            var livro = Estado(Concluido, EtapaExpediente.Organizacao);
            livro.ProximoItem = PlantaDaTrilha.InicioDosLivros[0];
            Check(Igual(GuiaDaTrilha.Destino(livro), new Vector3(-3.7f, 0f, -6.85f)),
                "organização, mão vazia: feixe no corredor interno, na altura do livro 1");
            livro.DestinoDoItemNaMao = PlantaDaTrilha.DestinoDosLivros[0];
            Check(Igual(GuiaDaTrilha.Destino(livro), new Vector3(-4.5f, 0f, -6.85f)),
                "livro na mão: feixe diante da estante, sem encostar na parede");
            var cadeira = Estado(Concluido, EtapaExpediente.Organizacao);
            cadeira.ProximoItem = PlantaDaTrilha.InicioDasCadeiras[0];
            Check(Igual(GuiaDaTrilha.Destino(cadeira), new Vector3(-.7f, 0f, -6.85f)),
                "cadeira 1 a pegar: feixe no corredor interno, na altura dela");
            Check(Igual(GuiaDaTrilha.Destino(Estado(Concluido, EtapaExpediente.Organizacao)), PlantaDaTrilha.FeixeDaPortaDosFundos),
                "organização sem item pendente: feixe na porta dos fundos");
            Check(Igual(GuiaDaTrilha.Destino(Estado(Concluido, EtapaExpediente.Descompressao)), PlantaDaTrilha.FeixeDaPortaDosFundos),
                "expediente concluído, porta fechando: feixe na porta dos fundos");
            var aberta = Estado(Concluido, EtapaExpediente.Descompressao);
            aberta.PortaDosFundosAberta = true;
            Check(Igual(GuiaDaTrilha.Destino(aberta), PlantaDaTrilha.FeixeDoBotaoDaDescompressao),
                "porta aberta: feixe diante do botão da descompressão");

            var destinos = new[]
            {
                PlantaDaTrilha.FeixeDoGerente, PlantaDaTrilha.FeixeDaPortaDaSala2, PlantaDaTrilha.FeixeDoMural, PlantaDaTrilha.FeixeDaBancada,
                PlantaDaTrilha.FeixeDoPires, PlantaDaTrilha.FeixeDaPortaDosFundos, PlantaDaTrilha.FeixeDoBotaoDaDescompressao
            };
            foreach (var d in destinos)
                Check(d.y == 0f && PlantaDaTrilha.Predio.Contains(d + Vector3.up), $"feixe em {d} fica no chão e dentro do prédio");

            texto.AppendLine().AppendLine("== Seta");
            var cabeca = Vector3.zero;
            var frente = Vector3.forward;
            Check(!GuiaDaTrilha.SetaVisivel(cabeca, frente, Quaternion.Euler(0f, 49f, 0f) * Vector3.forward * 5f), "a 49° do centro, a seta fica escondida");
            Check(GuiaDaTrilha.SetaVisivel(cabeca, frente, Quaternion.Euler(0f, 51f, 0f) * Vector3.forward * 5f), "a 51° do centro, a seta aparece");
            Check(GuiaDaTrilha.SetaVisivel(cabeca, frente, Vector3.back * 5f), "com o feixe atrás, a seta aparece");
            Check(!GuiaDaTrilha.SetaVisivel(cabeca, frente + Vector3.down * 3f, Vector3.forward * 5f + Vector3.up * 2f),
                "só o plano do chão conta: olhar para baixo não esconde o feixe da frente");
            Check(GuiaDaTrilha.AnguloHorizontal(cabeca, frente, Vector3.right * 5f) > 0f, "ângulo positivo é à direita");
            Check(GuiaDaTrilha.SetaDoHud(cabeca, frente, Vector3.right * 5f).EndsWith("→"), "HUD do PC: luz à direita");
            Check(GuiaDaTrilha.SetaDoHud(cabeca, frente, Vector3.left * 5f).StartsWith("←"), "HUD do PC: luz à esquerda");
            Check(GuiaDaTrilha.SetaDoHud(cabeca, frente, Vector3.back * 5f).StartsWith("↓"), "HUD do PC: luz atrás");
            Check(GuiaDaTrilha.SetaDoHud(cabeca, frente, Vector3.forward * 5f) == "", "HUD do PC: luz na frente, sem seta");

            texto.AppendLine().AppendLine("== Planta");
            Check(PlantaDaTrilha.Sala2.Contains(PlantaDaTrilha.MesaDoEscritorio) && PlantaDaTrilha.Sala2.Contains(PlantaDaTrilha.Pires),
                "mesa e pires dentro da Sala 2 (Sala A)");
            Check(PlantaDaTrilha.Sala2.Contains(PlantaDaTrilha.AreaDeConclusao + Vector3.up), "área de conclusão dentro da Sala 2");
            Check(PlantaDaTrilha.Copa.Contains(PlantaDaTrilha.CopoDeCafe), "copo de café na copa");
            foreach (var p in PlantaDaTrilha.InicioDosLivros) Check(PlantaDaTrilha.SalaDeAula.Contains(p), $"livro começa na sala de aula ({p})");
            foreach (var p in PlantaDaTrilha.InicioDosNotebooks) Check(PlantaDaTrilha.SalaDeAula.Contains(p), $"notebook começa na sala de aula ({p})");
            foreach (var p in PlantaDaTrilha.InicioDasCadeiras) Check(PlantaDaTrilha.SalaDeAula.Contains(p), $"cadeira começa na sala de aula ({p})");
            foreach (var p in PlantaDaTrilha.DestinoDosLivros) Check(PlantaDaTrilha.SalaDeAula.Contains(p), $"destino de livro na sala de aula ({p})");
            foreach (var p in PlantaDaTrilha.DestinoDasCadeiras) Check(PlantaDaTrilha.SalaDeAula.Contains(p), $"destino de cadeira na sala de aula ({p})");
            foreach (var p in PlantaDaTrilha.DestinoDosNotebooks) Check(PlantaDaTrilha.SalaDeAula.Contains(p), $"destino de notebook na sala de aula ({p})");
            Check(PlantaDaTrilha.Fundos.Contains(PlantaDaTrilha.ChegadaDosFundos) && PlantaDaTrilha.Fundos.Contains(PlantaDaTrilha.BotaoDaDescompressao),
                "chegada e botão da descompressão nos fundos");
            Check(Igual(PlantaDaTrilha.NaDescompressao(new Vector3(48f, 0f, 0f)), new Vector3(0f, 0f, -26f)),
                "descompressão deslocada para trás da parede sul");
            Check(!PlantaDaTrilha.Predio.Intersects(new Bounds(PlantaDaTrilha.NaDescompressao(new Vector3(48f, 1.5f, 0f)), new Vector3(14f, 3f, 16f))),
                "a descompressão provisória fica toda fora do prédio");

            texto.AppendLine().AppendLine("== Textos");
            var caminhoAntigo = new[] { @"\bpraça\b", @"\bgaleria\b", @"\bala\b", @"marcas azuis", @"marcações azuis", @"\bcoworking\b" };
            foreach (var arquivo in new[]
            {
                "Assets/SebraeLab/Scripts/Missions/InternshipDay.cs",
                "Assets/SebraeLab/Scripts/Missions/ManagerMission.cs",
                "Assets/SebraeLab/Scripts/Missions/MissionManager.cs",
            })
            {
                var literais = Regex.Matches(File.ReadAllText(arquivo), "\"((?:[^\"\\\\]|\\\\.)*)\"").Cast<Match>().Select(m => m.Groups[1].Value);
                var achados = literais.Where(l => caminhoAntigo.Any(p => Regex.IsMatch(l, p, RegexOptions.IgnoreCase))).ToList();
                Check(achados.Count == 0, $"{Path.GetFileName(arquivo)}: nenhum texto fala do caminho antigo ({string.Join(" | ", achados.Take(3))})");
            }

            texto.AppendLine().AppendLine(falhas == 0 ? "RESULTADO: OK" : $"RESULTADO: FALHA ({falhas})");
            Directory.CreateDirectory("Logs");
            File.WriteAllText(Report, texto.ToString());
            return falhas;
        }
    }
}
