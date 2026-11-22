using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SebraeLab.Missions
{
    /// <summary>
    /// Feixe de luz dourado no chão, onde a pessoa deve parar. Não é luz de verdade, que é cara no Quest 2: é um
    /// cilindro sem tampa e um disco, com o shader aditivo SebraeLab/Feixe, forte no piso e transparente no alto.
    /// Pulsa devagar e esmaece quando a cabeça chega perto, para não ofuscar no óculos.
    /// </summary>
    [DisallowMultipleComponent]
    public class Feixe : MonoBehaviour
    {
        public const float Diametro = 1.2f, Altura = 2.2f, Ciclo = 2f, DistanciaDeChegada = 1f;
        public const float BrilhoMinimoDoPulso = .6f, BrilhoDePerto = .15f, VelocidadeDoEsmaecer = 3f;
        const int Lados = 32;
        static readonly int IdDoBrilho = Shader.PropertyToID("_Brilho");
        static Mesh malha;

        /// <summary>Cabeça do jogador; o feixe esmaece quando ela chega perto.</summary>
        public Transform cabeca;
        /// <summary>Brilho atual, de 0 a 1, com o pulso e o esmaecimento.</summary>
        public float Brilho { get; private set; } = 1f;

        float esmaecer = 1f;
        MaterialPropertyBlock bloco;
        MeshRenderer pintor;

        public static Feixe Criar(Transform pai, Transform cabeca)
        {
            var material = Resources.Load<Material>("Acabamentos/Feixe");
            if (!material) throw new System.InvalidOperationException("Material ausente: Acabamentos/Feixe");
            var objeto = new GameObject("Feixe de luz", typeof(MeshFilter), typeof(MeshRenderer));
            objeto.transform.SetParent(pai, false);
            objeto.GetComponent<MeshFilter>().sharedMesh = Malha();
            var renderer = objeto.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            var feixe = objeto.AddComponent<Feixe>();
            feixe.cabeca = cabeca;
            return feixe;
        }

        public void Mover(Vector3 chao)
        {
            if (transform.position != chao) transform.position = chao;
        }

        void Awake()
        {
            pintor = GetComponent<MeshRenderer>();
            bloco = new MaterialPropertyBlock();
        }

        void LateUpdate()
        {
            float alvo = 1f;
            if (cabeca)
            {
                var distancia = cabeca.position - transform.position;
                distancia.y = 0f;
                if (distancia.magnitude < DistanciaDeChegada) alvo = BrilhoDePerto;
            }
            esmaecer = Mathf.MoveTowards(esmaecer, alvo, VelocidadeDoEsmaecer * Time.deltaTime);
            float pulso = Mathf.Lerp(BrilhoMinimoDoPulso, 1f, .5f + .5f * Mathf.Sin(Time.time * 2f * Mathf.PI / Ciclo));
            Brilho = pulso * esmaecer;
            bloco.SetFloat(IdDoBrilho, Brilho);
            pintor.SetPropertyBlock(bloco);
        }

        /// <summary>Cilindro sem tampa (uv.y = altura relativa) e disco no piso (uv.y = 0), numa malha só.</summary>
        static Mesh Malha()
        {
            if (malha) return malha;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangulos = new List<int>();
            float raio = Diametro / 2f;
            for (int i = 0; i <= Lados; i++)
            {
                float a = i * 2f * Mathf.PI / Lados;
                var p = new Vector3(Mathf.Cos(a) * raio, 0f, Mathf.Sin(a) * raio);
                vertices.Add(p); uvs.Add(new Vector2(i / (float)Lados, 0f));
                vertices.Add(p + Vector3.up * Altura); uvs.Add(new Vector2(i / (float)Lados, 1f));
            }
            for (int i = 0; i < Lados; i++)
            {
                int b = i * 2;
                triangulos.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 });
            }
            int centro = vertices.Count;
            vertices.Add(Vector3.up * .01f); uvs.Add(new Vector2(.5f, 0f)); // 1 cm acima do piso, sem brigar com ele
            for (int i = 0; i <= Lados; i++)
            {
                float a = i * 2f * Mathf.PI / Lados;
                vertices.Add(new Vector3(Mathf.Cos(a) * raio, .01f, Mathf.Sin(a) * raio));
                uvs.Add(new Vector2(i / (float)Lados, 0f));
            }
            for (int i = 0; i < Lados; i++) triangulos.AddRange(new[] { centro, centro + 2 + i, centro + 1 + i });
            malha = new Mesh { name = "Feixe" };
            malha.SetVertices(vertices);
            malha.SetUVs(0, uvs);
            malha.SetTriangles(triangulos, 0);
            malha.RecalculateBounds();
            return malha;
        }
    }
}
