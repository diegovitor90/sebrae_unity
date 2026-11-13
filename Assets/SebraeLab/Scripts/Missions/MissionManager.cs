using System;
using UnityEngine;

namespace SebraeLab.Missions
{
    /// <summary>Etapas do expediente, na ordem em que o estagiário passa por elas.</summary>
    public enum EtapaExpediente { Sala, Mural, Cafe, Organizacao, Descompressao }

    /// <summary>
    /// Fonte única da etapa atual do expediente. Botões e objetos perguntam a este componente
    /// se podem agir, em vez de cada um guardar um número próprio. Uma ação fora de ordem gera
    /// um aviso na placa de status, em vez de ser ignorada em silêncio.
    /// </summary>
    /// <remarks>
    /// Se ninguém colocar este componente na cena, o <see cref="InternshipDay"/> cria um com os
    /// textos padrão. Colocar pelo Inspector serve para editar objetivos e avisos sem mexer em código.
    /// </remarks>
    [DisallowMultipleComponent]
    public class MissionManager : MonoBehaviour
    {
        [Serializable]
        public struct TextosEtapa
        {
            public EtapaExpediente etapa;
            [Tooltip("Mostrado na placa de status enquanto esta é a etapa atual.")]
            [TextArea(1, 3)] public string objetivo;
            [Tooltip("Mostrado quando o jogador tenta algo desta etapa antes da hora.")]
            [TextArea(1, 2)] public string bloqueio;
        }

        [Tooltip("Um item por etapa. Etapa sem item usa texto vazio.")]
        public TextosEtapa[] textos = Padrao();

        [Tooltip("Segundos que um aviso fica na placa antes de voltar ao objetivo.")]
        [Min(.5f)] public float duracaoAviso = 3f;

        public EtapaExpediente Atual { get; private set; }

        /// <summary>Texto que a placa de status deve mostrar agora.</summary>
        public string Texto { get; private set; }

        /// <summary>Disparado depois que a etapa muda.</summary>
        public event Action<EtapaExpediente> EtapaMudou;

        /// <summary>Disparado quando o texto da placa muda (objetivo, progresso ou aviso).</summary>
        public event Action<string> TextoMudou;

        string progresso;
        float avisoAte;

        void Awake()
        {
            Atual = EtapaExpediente.Sala;
            Publicar();
        }

        void Update()
        {
            if (avisoAte > 0 && Time.time >= avisoAte)
            {
                avisoAte = 0;
                Publicar();
            }
        }

        public bool Em(EtapaExpediente etapa) => Atual == etapa;

        /// <summary>Verdadeiro se a etapa é a atual ou já passou.</summary>
        public bool Alcancou(EtapaExpediente etapa) => Atual >= etapa;

        /// <summary>Verdadeiro enquanto um aviso está na placa.</summary>
        public bool AvisoAtivo => avisoAte > 0;

        /// <summary>Mostra por que a etapa ainda não está liberada.</summary>
        public void AvisarBloqueio(EtapaExpediente etapa) => Avisar(Textos(etapa).bloqueio);

        /// <summary>Verdadeiro se a etapa é a atual. Se não for, mostra o motivo e devolve falso.</summary>
        public bool Exigir(EtapaExpediente etapa)
        {
            if (Atual == etapa) return true;
            Avisar(Atual < etapa ? Textos(etapa).bloqueio : "Esta etapa já foi concluída.");
            return false;
        }

        /// <summary>
        /// Conclui a etapa e avança para a seguinte. Só funciona se ela for a atual, então chamar
        /// duas vezes, ou fora de ordem, não pula etapas nem conta em dobro.
        /// </summary>
        public bool Concluir(EtapaExpediente etapa)
        {
            if (!Exigir(etapa)) return false;
            if (etapa < EtapaExpediente.Descompressao) Definir(etapa + 1);
            return true;
        }

        /// <summary>Substitui o objetivo por um texto de progresso até a etapa mudar.</summary>
        public void Progresso(string texto)
        {
            progresso = texto;
            Publicar();
        }

        /// <summary>Mostra um aviso temporário. Depois de <see cref="duracaoAviso"/> a placa volta ao normal.</summary>
        public void Avisar(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return;
            avisoAte = Time.time + duracaoAviso;
            Mostrar(texto);
            Debug.Log("SEBRAE_AVISO " + texto.Replace('\n', ' '));
        }

        public void Reiniciar() => Definir(EtapaExpediente.Sala);

        void Definir(EtapaExpediente etapa)
        {
            Atual = etapa;
            progresso = null;
            avisoAte = 0;
            Publicar();
            Debug.Log("SEBRAE_ETAPA " + etapa);
            EtapaMudou?.Invoke(etapa);
        }

        void Publicar()
        {
            if (avisoAte > 0) return;
            Mostrar(progresso ?? Textos(Atual).objetivo);
        }

        void Mostrar(string texto)
        {
            if (texto == Texto) return;
            Texto = texto;
            TextoMudou?.Invoke(texto);
        }

        TextosEtapa Textos(EtapaExpediente etapa)
        {
            if (textos != null)
                foreach (var item in textos)
                    if (item.etapa == etapa) return item;
            return default;
        }

        static TextosEtapa[] Padrao() => new[]
        {
            new TextosEtapa { etapa = EtapaExpediente.Sala,
                objetivo = "Converse com o gestor e encontre sua sala.",
                bloqueio = "Primeiro converse com o gestor do espaço." },
            new TextosEtapa { etapa = EtapaExpediente.Mural,
                objetivo = "Visite o Mural do Conhecimento.",
                bloqueio = "Encontre o gerente e entre na Sala 2 antes de ler o mural." },
            new TextosEtapa { etapa = EtapaExpediente.Cafe,
                objetivo = "Pegue o café na copa e entregue no pires\nda mesa do escritório.",
                bloqueio = "Leia o Mural do Conhecimento e aperte ENTENDIDO." },
            new TextosEtapa { etapa = EtapaExpediente.Organizacao,
                objetivo = "Coworking • guarde os livros nas estantes numeradas.",
                bloqueio = "Entregue o café no pires do escritório antes de organizar o coworking." },
            new TextosEtapa { etapa = EtapaExpediente.Descompressao,
                objetivo = "EXPEDIENTE CONCLUÍDO\nEntre na sala de descompressão.",
                bloqueio = "A descompressão abre quando os seis itens estiverem no lugar." },
        };

        void OnValidate()
        {
            if (textos == null || textos.Length == 0) textos = Padrao();
        }
    }
}
