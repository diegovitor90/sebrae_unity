using System;
using UnityEngine;

namespace SebraeLab.Missions
{
    /// <summary>
    /// Flecha em voo. Integra a gravidade real e varre o trecho de cada passo com um raio saindo da ponta:
    /// a 40 m/s a flecha anda quase 1 m por quadro e o alvo tem 15 cm de espessura, então a colisão por evento
    /// poderia ser perdida. O voo roda em Update, em passos de no máximo 1/60 s, para ficar fluido no headset.
    /// </summary>
    public class ArcheryArrow : MonoBehaviour
    {
        /// <summary>Posição da ponta no espaço local da flecha de luz (a estrela termina em z 0,53).</summary>
        public static readonly Vector3 PontaLocal = new Vector3(0f, 0f, .53f);
        const float PassoMaximo = 1f / 60f;

        public bool Voando { get; private set; }
        public bool AcertouAlvo { get; private set; }
        public float VelocidadeInicial { get; private set; }
        public Vector3 Velocidade { get; private set; }
        public Vector3 Ponta { get; private set; }
        public float DistanciaDoCentro { get; private set; } = -1f;
        public float TempoNoAr { get; private set; }

        Transform alvo, arco;
        float tempoMaximo, cravadaPor;
        Action<float> acertou;
        Action errou;

        public void Lancar(Vector3 velocidade, Transform alvoDoTiro, Transform arcoDoTiro, float tempoMaximoNoAr, float cravadaPorSegundos,
            Action<float> aoAcertarAlvo, Action aoErrar)
        {
            alvo = alvoDoTiro;
            arco = arcoDoTiro;
            tempoMaximo = tempoMaximoNoAr;
            cravadaPor = cravadaPorSegundos;
            acertou = aoAcertarAlvo;
            errou = aoErrar;
            Velocidade = velocidade;
            VelocidadeInicial = velocidade.magnitude;
            Ponta = transform.TransformPoint(PontaLocal);
            Voando = true;
        }

        void Update()
        {
            if (!Voando) return;
            float restante = Mathf.Min(Time.deltaTime, .1f);
            while (Voando && restante > 0f)
            {
                float passo = Mathf.Min(restante, PassoMaximo);
                restante -= passo;
                Avancar(passo);
            }
        }

        void Avancar(float passo)
        {
            TempoNoAr += passo;
            Velocidade += Physics.gravity * passo;
            var deslocamento = Velocidade * passo;
            float comprimento = deslocamento.magnitude;
            if (comprimento > 1e-5f)
            {
                var acertos = Physics.RaycastAll(Ponta, deslocamento / comprimento, comprimento, ~0, QueryTriggerInteraction.Ignore);
                Array.Sort(acertos, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var acerto in acertos)
                {
                    if (Ignorado(acerto.collider)) continue;
                    Parar(acerto.point, acerto.collider);
                    return;
                }
            }
            Ponta += deslocamento;
            var rotacao = Quaternion.LookRotation(Velocidade, Vector3.up);
            transform.SetPositionAndRotation(Ponta - rotacao * PontaLocal, rotacao);
            if (TempoNoAr > tempoMaximo || Ponta.y < -5f) Parar(Ponta, null);
        }

        bool Ignorado(Collider colisor) =>
            colisor is CharacterController || colisor.transform.IsChildOf(transform) || (arco && colisor.transform.IsChildOf(arco));

        void Parar(Vector3 ponto, Collider colisor)
        {
            Voando = false;
            Ponta = ponto;
            transform.position = ponto - transform.rotation * PontaLocal;
            bool noAlvo = colisor && alvo && (colisor.transform == alvo || colisor.transform.IsChildOf(alvo));
            if (noAlvo)
            {
                AcertouAlvo = true;
                DistanciaDoCentro = Vector3.ProjectOnPlane(ponto - alvo.position, alvo.forward).magnitude;
                acertou?.Invoke(DistanciaDoCentro);
            }
            else errou?.Invoke();
            Destroy(gameObject, cravadaPor);
        }
    }
}
