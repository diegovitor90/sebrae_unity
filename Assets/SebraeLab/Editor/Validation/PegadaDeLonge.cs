using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace SebraeLab.Editor
{
    /// <summary>
    /// Repete a decisão do NearFarInteractor (OnSelectEntering) quando um objeto é pego pelo raio: o objeto fica
    /// na ponta do raio ou vem para a mão? Os testes que disparam eventos do XRI por Invoke não passam por essa
    /// decisão; esta consulta lê a configuração real do rig da cena (no editor o rig fica desligado, mas existe).
    /// </summary>
    static class PegadaDeLonge
    {
        public static List<NearFarInteractor> MaosDoRig() =>
            Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();

        /// <summary>Verdadeiro se, pego de longe por esta mão, o objeto vem para a mão em vez de ficar na ponta do raio.</summary>
        public static bool VemParaAMao(XRGrabInteractable objeto, NearFarInteractor mao)
        {
            // Padrão da mão, depois a preferência do objeto, como no NearFarInteractor.
            bool naPontaDoRaio = mao.farAttachMode == InteractorFarAttachMode.Far;
            var provedor = (IFarAttachProvider)objeto;
            if (provedor.farAttachMode != InteractableFarAttachMode.DeferToInteractor)
                naPontaDoRaio = provedor.farAttachMode == InteractableFarAttachMode.Far;
            return !naPontaDoRaio;
        }
    }
}
