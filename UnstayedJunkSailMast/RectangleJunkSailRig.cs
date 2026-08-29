using System;
using UnityEngine;

namespace UnstayedJunkSailMast;

internal sealed class RectangleJunkSailRig : MonoBehaviour
{
	private Mast mast;

	private Sail sail;

	private RopeControllerSailReef reefController;

	private ReefEffectAnimUniversal reefEffect;

	private GPButtonRopeWinch boundWinch;

	private bool originalWinchReverseResistance;

	private bool initialized;

	private bool refreshClothPending;

	private bool warningLogged;

	internal bool Initialize(Mast owningMast)
	{
		mast = owningMast;
		if (initialized)
		{
			return true;
		}
		sail = GetComponent<Sail>();
		SailConnections component = GetComponent<SailConnections>();
		reefController = ((component != null) ? (component.reefController as RopeControllerSailReef) : null);
		reefEffect = GetComponentInChildren<ReefEffectAnimUniversal>(includeInactive: true);
		if (sail == null || component == null || reefController == null || reefEffect == null)
		{
			return Fail("its reefing components are incomplete");
		}
		ApplyControllerDirection();
		initialized = true;
		refreshClothPending = true;
		return true;
	}

	internal float GetTransformedSailArea(float fallback)
	{
		if (sail == null)
		{
			sail = GetComponent<Sail>();
		}
		SkinnedMeshRenderer skinnedMeshRenderer = ((sail != null && sail.cloth != null) ? sail.cloth.GetComponent<SkinnedMeshRenderer>() : null);
		Mesh mesh = ((skinnedMeshRenderer != null) ? skinnedMeshRenderer.sharedMesh : null);
		if (mesh == null)
		{
			return fallback;
		}
		int[] triangles = mesh.triangles;
		Vector3[] vertices = mesh.vertices;
		float num = 0f;
		for (int i = 0; i + 2 < triangles.Length; i += 3)
		{
			Vector3 vector = skinnedMeshRenderer.transform.TransformVector(vertices[triangles[i]]);
			Vector3 vector2 = skinnedMeshRenderer.transform.TransformVector(vertices[triangles[i + 1]]);
			Vector3 vector3 = skinnedMeshRenderer.transform.TransformVector(vertices[triangles[i + 2]]);
			num += Vector3.Cross(vector2 - vector, vector3 - vector).magnitude * 0.5f;
		}
		if (!(num > 0f))
		{
			return fallback;
		}
		return num;
	}

	internal void PrepareForMastUpdate()
	{
		RestoreWinch();
	}

	internal void BindAssignedWinch()
	{
		if (!initialized || mast == null)
		{
			return;
		}
		GPButtonRopeWinch[] componentsInChildren = ((mast.shipRigidbody != null) ? mast.shipRigidbody.transform : mast.transform).GetComponentsInChildren<GPButtonRopeWinch>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			if (componentsInChildren[i] != null && componentsInChildren[i].rope == reefController)
			{
				BindWinch(componentsInChildren[i]);
				return;
			}
		}
		RestoreWinch();
	}

	private void LateUpdate()
	{
		if (initialized && refreshClothPending)
		{
			refreshClothPending = false;
			RefreshCloth();
			base.enabled = false;
		}
	}

	private void OnDestroy()
	{
		RestoreWinch();
	}

	private void ApplyControllerDirection()
	{
		float num = Mathf.Clamp01(sail.currentUnroll);
		reefController.reverseReefing = true;
		reefController.currentLength = 1f - num;
		reefController.changed = true;
	}

	private void BindWinch(GPButtonRopeWinch winch)
	{
		if (boundWinch == winch)
		{
			winch.reverseWindResistance = true;
			return;
		}
		RestoreWinch();
		boundWinch = winch;
		originalWinchReverseResistance = winch.reverseWindResistance;
		winch.reverseWindResistance = true;
	}

	private void RestoreWinch()
	{
		if (boundWinch != null)
		{
			boundWinch.reverseWindResistance = originalWinchReverseResistance;
		}
		boundWinch = null;
	}

	private void RefreshCloth()
	{
		if (reefEffect == null || !reefEffect.isActiveAndEnabled)
		{
			return;
		}
		try
		{
			reefEffect.RefreshCloth();
		}
		catch (Exception ex)
		{
			Plugin.LogSource?.LogWarning("Could not refresh Rectangle Junk cloth for " + base.name + ": " + ex.Message);
		}
	}

	private bool Fail(string reason)
	{
		if (!warningLogged)
		{
			warningLogged = true;
			Plugin.LogSource?.LogWarning("Could not initialize Rectangle Junk rig for " + base.name + ": " + reason + ".");
		}
		return false;
	}
}
