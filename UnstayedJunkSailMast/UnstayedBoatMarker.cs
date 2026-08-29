using UnityEngine;

namespace UnstayedJunkSailMast;

internal sealed class UnstayedBoatMarker : MonoBehaviour
{
	private void OnDestroy()
	{
		BoatCustomParts component = GetComponent<BoatCustomParts>();
		if (component != null)
		{
			UnstayedBoatRegistry.Unregister(component);
		}
	}
}
