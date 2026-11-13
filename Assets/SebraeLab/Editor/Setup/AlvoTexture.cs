using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using SebraeLab.Missions;

namespace SebraeLab.Editor
{
    /// <summary>
    /// Regenera Alvo.png com os 5 anéis nos raios de ArcheryScore.Raios, para a pintura e a pontuação saírem
    /// da mesma tabela. A borda da imagem vale o raio do último anel (0,70 m). Relatório em Logs/alvo-texture.txt.
    /// </summary>
    public static class AlvoTexture
    {
        const string Caminho = "Assets/SebraeLab/Art/Textures/Acabamentos/Alvo.png";
        const string Relatorio = "Logs/alvo-texture.txt";
        const int Lado = 512;

        static readonly Color Fundo = new Color32(0x2A, 0x4F, 0x54, 255);
        // Do centro para fora, na ordem dos anéis de ArcheryScore (10, 8, 6, 4 e 2 pontos).
        static readonly Color[] Aneis =
        {
            new Color32(0xEF, 0xB8, 0x5E, 255), new Color32(0xB4, 0x50, 0x35, 255), new Color32(0xEB, 0xDF, 0xBE, 255),
            new Color32(0x27, 0x6C, 0x74, 255), new Color32(0xEB, 0xDF, 0xBE, 255),
        };

        [MenuItem("Sebrae Lab/Arte/Gerar textura do alvo")]
        static void Menu() => Debug.Log("AlvoTexture: " + (Gerar() ? "OK" : "FALHA") + ". Detalhes em " + Relatorio);

        /// <summary>Para -executeMethod: fecha o Unity com 0 (ok) ou 1 (falha).</summary>
        public static void GerarPelaLinhaDeComando() => EditorApplication.Exit(Gerar() ? 0 : 1);

        public static bool Gerar()
        {
            var texto = new StringBuilder();
            int falhas = 0;
            void Check(bool ok, string mensagem)
            {
                if (!ok) falhas++;
                texto.AppendLine((ok ? "OK    " : "FALHA ") + mensagem);
            }

            try
            {
                float raioMaximo = ArcheryScore.Raios[ArcheryScore.Raios.Length - 1];
                var pixels = new Color[Lado * Lado];
                for (int y = 0; y < Lado; y++)
                    for (int x = 0; x < Lado; x++)
                    {
                        var soma = Color.clear;
                        foreach (float sy in new[] { .25f, .75f })
                            foreach (float sx in new[] { .25f, .75f })
                            {
                                float dx = (x + sx) / Lado * 2f - 1f, dy = (y + sy) / Lado * 2f - 1f;
                                soma += CorEm(Mathf.Sqrt(dx * dx + dy * dy) * raioMaximo);
                            }
                        pixels[y * Lado + x] = soma / 4f;
                    }
                var textura = new Texture2D(Lado, Lado, TextureFormat.RGBA32, false);
                textura.SetPixels(pixels);
                var bytes = textura.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(textura);
                File.WriteAllBytes(Caminho, bytes);
                AssetDatabase.ImportAsset(Caminho, ImportAssetOptions.ForceUpdate);
                texto.AppendLine($"PNG gravado em {Caminho} ({bytes.Length} bytes)");

                // Confere lendo o arquivo de volta: a cor no meio de cada faixa, de cada lado de uma borda e no canto.
                var lida = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Check(lida.LoadImage(File.ReadAllBytes(Caminho)) && lida.width == Lado && lida.height == Lado, $"PNG lido de volta com {lida.width} x {lida.height}");
                Color Amostra(float raio) => lida.GetPixel(Lado / 2 + Mathf.RoundToInt(raio / raioMaximo * (Lado / 2f)), Lado / 2);
                bool Igual(Color a, Color b) => Mathf.Abs(a.r - b.r) < .02f && Mathf.Abs(a.g - b.g) < .02f && Mathf.Abs(a.b - b.b) < .02f;
                float interno = 0f;
                for (int i = 0; i < ArcheryScore.Raios.Length; i++)
                {
                    float meio = (interno + ArcheryScore.Raios[i]) / 2f;
                    Check(Igual(Amostra(meio), Aneis[i]), $"anel de {ArcheryScore.PontosDosAneis[i]} pontos tem a cor esperada em {meio:0.###} m");
                    interno = ArcheryScore.Raios[i];
                }
                Check(!Igual(Amostra(.13f), Amostra(.15f)), "a cor muda na borda de 0,14 m");
                Check(Igual(lida.GetPixel(2, 2), Fundo), "o canto da imagem tem a cor do fundo");
                UnityEngine.Object.DestroyImmediate(lida);
            }
            catch (Exception e)
            {
                falhas++;
                texto.AppendLine("FALHA exceção: " + e);
            }

            texto.Insert(0, "Textura do alvo • " + DateTime.Now + "\n");
            texto.AppendLine(falhas == 0 ? "RESULTADO: OK" : $"RESULTADO: FALHA ({falhas})");
            Directory.CreateDirectory("Logs");
            File.WriteAllText(Relatorio, texto.ToString());
            Debug.Log(texto.ToString());
            return falhas == 0;
        }

        static Color CorEm(float raio)
        {
            for (int i = 0; i < ArcheryScore.Raios.Length; i++) if (raio <= ArcheryScore.Raios[i]) return Aneis[i];
            return Fundo;
        }
    }
}
