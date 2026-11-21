using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SebraeLab.Missions;
using Object = UnityEngine.Object;

namespace SebraeLab.Editor
{
    /// <summary>
    /// Ajusta a cena da missão para a trilha dentro do prédio: leva o gerente e a caixa de conversa para a frente de quem
    /// nasce, fecha a fachada com uma parede transparente medida a partir da malha do vidro e deixa a porta de vidro dos
    /// fundos móvel (sem marca de estático). Pode ser rodado de novo: refaz a parede e regrava as posições.
    /// </summary>
    public static class TrilhaCena
    {
        [MenuItem("Sebrae Lab/Trilha/Ajustar cena")]
        public static void AjustarCena()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Pare o Play antes de ajustar a cena.");
            var cena = EditorSceneManager.OpenScene(ManagerMissionSetup.MissionScene);
            var missao = Object.FindFirstObjectByType<ManagerMission>();
            if (!missao || !missao.manager) throw new InvalidOperationException("ManagerMission ou gerente ausente na cena.");
            missao.manager.position = PlantaDaTrilha.Gerente;
            Achar("Ponto do gerente").position = PlantaDaTrilha.Gerente + Vector3.up * .008f;
            Achar("Identificacao do gerente").position = PlantaDaTrilha.IdentificacaoDoGerente;
            Achar("Conversa com o gerente").position = PlantaDaTrilha.CaixaDeConversa;
            PosicionarMesaDoGerente();
            MontarFachada();
            var vidro = Achar(PlantaDaTrilha.NomeDaPortaDeVidro).gameObject;
            GameObjectUtility.SetStaticEditorFlags(vidro, 0); // estática, ela não desliza em Play (lote estático)
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            Debug.Log("SEBRAE_TRILHA_CENA_AJUSTADA");
        }

        public static void RodarPelaLinhaDeComando()
        {
            try { AjustarCena(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError(e); EditorApplication.Exit(1); }
        }

        /// <summary>
        /// Põe a mesa da recepção entre quem nasce e o gerente, girada 90° para ocupar menos de x, e deixa o gerente atrás
        /// dela (a oeste). Pode ser rodado de novo: se a mesa já está no lugar, não faz nada.
        /// </summary>
        static void PosicionarMesaDoGerente()
        {
            var mesa = Achar(PlantaDaTrilha.NomeDaMesaDoGerente);
            Bounds Caixa()
            {
                var renders = mesa.GetComponentsInChildren<Renderer>();
                var b = renders[0].bounds;
                foreach (var x in renders) b.Encapsulate(x.bounds);
                return b;
            }
            var alvo = PlantaDaTrilha.MesaDoGerente;
            var caixa = Caixa();
            if (new Vector2(caixa.center.x - alvo.x, caixa.center.z - alvo.z).magnitude < .05f) return;
            mesa.RotateAround(caixa.center, Vector3.up, PlantaDaTrilha.GiroDaMesaDoGerente);
            caixa = Caixa();
            mesa.position += new Vector3(alvo.x - caixa.center.x, 0f, alvo.z - caixa.center.z);
        }

        static Transform Achar(string nome)
        {
            var t = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(x => x.name == nome);
            if (!t) throw new InvalidOperationException("Objeto ausente na cena: " + nome);
            return t;
        }

        /// <summary>
        /// Raios horizontais a 1,2 m, de grau em grau, do nascimento contra uma cópia temporária do colisor do vidro.
        /// Os pontos achados, em ordem, viram trechos de colisor de 2,8 m. Onde o raio não acha vidro (o vão da porta),
        /// os pontos vizinhos se ligam direto. As duas pontas avançam 0,3 m para encostar nas paredes.
        /// </summary>
        static void MontarFachada()
        {
            var antiga = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == PlantaDaTrilha.NomeDaFachada);
            if (antiga) Object.DestroyImmediate(antiga.gameObject);
            var entrada = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(m => m.name == PlantaDaTrilha.NomeDaEntrada);
            if (!entrada) throw new InvalidOperationException("Vidro da recepção ausente: '" + PlantaDaTrilha.NomeDaEntrada + "'");
            var temporario = entrada.gameObject.AddComponent<MeshCollider>();
            temporario.sharedMesh = entrada.sharedMesh;
            Physics.SyncTransforms();
            var pontos = new List<Vector3>();
            var origem = PlantaDaTrilha.Nascimento + Vector3.up * 1.2f;
            for (int grau = -90; grau <= 270; grau++)
            {
                var direcao = Quaternion.Euler(0f, grau, 0f) * Vector3.forward;
                if (temporario.Raycast(new Ray(origem, direcao), out var hit, 15f))
                    pontos.Add(new Vector3(hit.point.x, 0f, hit.point.z));
            }
            Object.DestroyImmediate(temporario);
            if (pontos.Count < 10) throw new InvalidOperationException($"Só {pontos.Count} pontos do vidro achados; confira o objeto Entrada.");
            pontos[0] += (pontos[0] - pontos[1]).normalized * .3f;
            int ultimo = pontos.Count - 1;
            pontos[ultimo] += (pontos[ultimo] - pontos[ultimo - 1]).normalized * .3f;

            var raiz = new GameObject(PlantaDaTrilha.NomeDaFachada).transform;
            int trechos = 0;
            for (int i = 1; i < pontos.Count; i++)
            {
                var a = pontos[i - 1];
                var b = pontos[i];
                float comprimento = Vector3.Distance(a, b);
                if (comprimento < .01f) continue;
                var trecho = new GameObject("Trecho " + (++trechos)).transform;
                trecho.SetParent(raiz, false);
                trecho.SetPositionAndRotation((a + b) / 2f, Quaternion.LookRotation(b - a));
                var caixa = trecho.gameObject.AddComponent<BoxCollider>();
                caixa.center = new Vector3(0f, 1.4f, 0f);
                caixa.size = new Vector3(.1f, 2.8f, comprimento + .05f);
            }
            Debug.Log($"Fachada: {pontos.Count} pontos do vidro, {trechos} trechos de colisor");
        }
    }
}
