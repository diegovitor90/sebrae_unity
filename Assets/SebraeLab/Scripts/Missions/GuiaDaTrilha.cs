using UnityEngine;

namespace SebraeLab.Missions
{
    /// <summary>O que a sinalização precisa saber da trilha para decidir onde acender o feixe.</summary>
    public struct EstadoDaTrilha
    {
        public ManagerMissionState Gerente;
        public EtapaExpediente Etapa;
        public bool CafeNaMao;
        /// <summary>Destino do item da organização que está na mão; nulo com a mão vazia.</summary>
        public Vector3? DestinoDoItemNaMao;
        /// <summary>Próximo item da organização a pegar; nulo fora da organização.</summary>
        public Vector3? ProximoItem;
        public bool PortaDosFundosAberta;
        /// <summary>A cabeça está dentro do anexo: a trilha acabou, e o InternshipDay apaga o feixe e a seta.</summary>
        public bool NoAnexo;
    }

    /// <summary>Regras puras da sinalização: onde fica o feixe e quando a seta aparece. Sem estado; testadas no editor.</summary>
    public static class GuiaDaTrilha
    {
        /// <summary>Acima deste ângulo, no plano do chão, o feixe está fora da vista e a seta aparece.</summary>
        public const float AnguloDaSeta = 50f;
        /// <summary>Acima deste ângulo, o HUD do PC diz que a luz está atrás.</summary>
        public const float AnguloDeTras = 135f;

        public static Vector3 Destino(EstadoDaTrilha e)
        {
            switch (e.Gerente)
            {
                case ManagerMissionState.FindManager:
                case ManagerMissionState.Dialogue:
                    return PlantaDaTrilha.FeixeDoGerente;
                case ManagerMissionState.FindRoom:
                    return PlantaDaTrilha.FeixeDaPortaDaSala2;
            }
            switch (e.Etapa)
            {
                case EtapaExpediente.Sala: return PlantaDaTrilha.FeixeDaPortaDaSala2;
                case EtapaExpediente.Mural: return PlantaDaTrilha.FeixeDoMural;
                case EtapaExpediente.Cafe: return e.CafeNaMao ? PlantaDaTrilha.FeixeDoPires : PlantaDaTrilha.FeixeDaBancada;
                case EtapaExpediente.Organizacao:
                    if (e.DestinoDoItemNaMao.HasValue) return PlantaDaTrilha.ChaoDiante(e.DestinoDoItemNaMao.Value);
                    if (e.ProximoItem.HasValue) return PlantaDaTrilha.ChaoDiante(e.ProximoItem.Value);
                    return PlantaDaTrilha.FeixeDaPortaDosFundos;
                default:
                    return e.PortaDosFundosAberta ? PlantaDaTrilha.FeixeDaPortaDoAnexo : PlantaDaTrilha.FeixeDaPortaDosFundos;
            }
        }

        /// <summary>Ângulo com sinal, no plano do chão, de onde a cabeça olha até o alvo. Positivo é à direita.</summary>
        public static float AnguloHorizontal(Vector3 cabeca, Vector3 frente, Vector3 alvo)
        {
            frente.y = 0f;
            var direcao = alvo - cabeca;
            direcao.y = 0f;
            if (frente.sqrMagnitude < 1e-6f || direcao.sqrMagnitude < 1e-6f) return 0f;
            return Vector3.SignedAngle(frente, direcao, Vector3.up);
        }

        public static bool SetaVisivel(Vector3 cabeca, Vector3 frente, Vector3 alvo) =>
            Mathf.Abs(AnguloHorizontal(cabeca, frente, alvo)) > AnguloDaSeta;

        /// <summary>Seta de texto do HUD do PC; vazia quando o feixe está na vista.</summary>
        public static string SetaDoHud(Vector3 cabeca, Vector3 frente, Vector3 alvo)
        {
            float angulo = AnguloHorizontal(cabeca, frente, alvo);
            if (Mathf.Abs(angulo) <= AnguloDaSeta) return "";
            if (Mathf.Abs(angulo) > AnguloDeTras) return "↓   A luz está atrás de você";
            return angulo > 0f ? "A luz está à direita   →" : "←   A luz está à esquerda";
        }
    }
}
