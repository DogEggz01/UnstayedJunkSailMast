using System.Collections.Generic;
using UnityEngine;

namespace UnstayedJunkSailMast
{
    internal sealed class RectangleJunkWinchVisibility : MonoBehaviour
    {
        internal void SetHidden(bool shouldHide)
        {
            Restore();
            if (!shouldHide)
            {
                return;
            }

            Renderer[] renderers =
                GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null && renderer.enabled)
                {
                    hiddenRenderers.Add(renderer);
                    renderer.enabled = false;
                }
            }

            Collider[] colliders =
                GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider != null && collider.enabled)
                {
                    hiddenColliders.Add(collider);
                    collider.enabled = false;
                }
            }
        }

        private void OnDestroy()
        {
            Restore();
        }

        private void Restore()
        {
            for (int i = 0; i < hiddenRenderers.Count; i++)
            {
                if (hiddenRenderers[i] != null)
                {
                    hiddenRenderers[i].enabled = true;
                }
            }

            for (int i = 0; i < hiddenColliders.Count; i++)
            {
                if (hiddenColliders[i] != null)
                {
                    hiddenColliders[i].enabled = true;
                }
            }

            hiddenRenderers.Clear();
            hiddenColliders.Clear();
        }

        private readonly List<Renderer> hiddenRenderers =
            new List<Renderer>();
        private readonly List<Collider> hiddenColliders =
            new List<Collider>();
    }
}
