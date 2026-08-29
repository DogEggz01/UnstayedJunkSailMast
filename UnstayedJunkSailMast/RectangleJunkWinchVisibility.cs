using System.Collections.Generic;
using UnityEngine;

namespace UnstayedJunkSailMast;

internal sealed class RectangleJunkWinchVisibility : MonoBehaviour
{
	private readonly List<Renderer> hiddenRenderers = new List<Renderer>();

	private readonly List<Collider> hiddenColliders = new List<Collider>();

	internal void SetHidden(bool shouldHide)
	{
		Restore();
		if (!shouldHide)
		{
			return;
		}
		Renderer[] componentsInChildren = GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer in componentsInChildren)
		{
			if (renderer != null && renderer.enabled)
			{
				hiddenRenderers.Add(renderer);
				renderer.enabled = false;
			}
		}
		Collider[] componentsInChildren2 = GetComponentsInChildren<Collider>(includeInactive: true);
		foreach (Collider collider in componentsInChildren2)
		{
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
		for (int j = 0; j < hiddenColliders.Count; j++)
		{
			if (hiddenColliders[j] != null)
			{
				hiddenColliders[j].enabled = true;
			}
		}
		hiddenRenderers.Clear();
		hiddenColliders.Clear();
	}
}
