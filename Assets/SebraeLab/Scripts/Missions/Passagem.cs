using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace SebraeLab.Missions
{
    /// <summary>
    /// Leva a pessoa de um lado de uma porta ao outro sem movimento visível: escurece em <see cref="Tempo"/>, move e gira,
    /// e clareia no mesmo tempo. O escuro é uma esfera presa à cabeça, desligada quando a tela está clara.
    /// </summary>
    public sealed class Passagem : MonoBehaviour
    {
        public const float Tempo = .25f;
        static readonly int Alfa = Shader.PropertyToID("_Alfa");

        Action<Vector3, float> mover;
        MeshRenderer pintor;
        MaterialPropertyBlock bloco;

        public bool EmAndamento { get; private set; }
        public float AlfaAtual { get; private set; }
        public GameObject Escuro { get; private set; }

        /// <param name="mover">Põe a cabeça na chegada (no chão) olhando para a guinada dada.</param>
        public static Passagem Criar(Transform pai, Transform cabeca, Action<Vector3, float> mover)
        {
            var material = Resources.Load<Material>("Acabamentos/Escuro");
            if (!material) throw new InvalidOperationException("Material ausente: Acabamentos/Escuro");
            var objeto = new GameObject("Passagem escura");
            objeto.transform.SetParent(pai, false);
            var passagem = objeto.AddComponent<Passagem>();
            passagem.mover = mover;
            var esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            esfera.name = "Escuro";
            UnityEngine.Object.Destroy(esfera.GetComponent<Collider>());
            esfera.transform.SetParent(cabeca, false);
            esfera.transform.localPosition = Vector3.zero;
            esfera.transform.localScale = Vector3.one * .6f;
            var pintor = esfera.GetComponent<MeshRenderer>();
            pintor.sharedMaterial = material;
            pintor.shadowCastingMode = ShadowCastingMode.Off;
            pintor.receiveShadows = false;
            pintor.lightProbeUsage = LightProbeUsage.Off;
            passagem.Escuro = esfera;
            passagem.pintor = pintor;
            passagem.bloco = new MaterialPropertyBlock();
            passagem.Pintar(0f);
            esfera.SetActive(false);
            return passagem;
        }

        /// <summary>Começa a passagem. Devolve false se já há uma em andamento.</summary>
        public bool Atravessar(Vector3 chegada, float guinada)
        {
            if (EmAndamento) return false;
            EmAndamento = true;
            StartCoroutine(Atravessa(chegada, guinada));
            return true;
        }

        IEnumerator Atravessa(Vector3 chegada, float guinada)
        {
            Escuro.SetActive(true);
            for (float t = 0f; t < Tempo; t += Time.deltaTime) { Pintar(t / Tempo); yield return null; }
            Pintar(1f);
            mover(chegada, guinada);
            Physics.SyncTransforms();
            yield return null;
            for (float t = 0f; t < Tempo; t += Time.deltaTime) { Pintar(1f - t / Tempo); yield return null; }
            Pintar(0f);
            Escuro.SetActive(false);
            EmAndamento = false;
        }

        void Pintar(float alfa)
        {
            AlfaAtual = alfa;
            bloco.SetFloat(Alfa, alfa);
            pintor.SetPropertyBlock(bloco);
        }
    }
}
