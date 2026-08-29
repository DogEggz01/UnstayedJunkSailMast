using System;
using System.Collections.Generic;
using ShipyardExpansion;
using ShipyardExpansion.Scripts;
using UnityEngine;

namespace UnstayedJunkSailMast;

internal static class RectangleJunkSails
{
	internal const int LegacyNarrowIndex = 131;

	internal const int LegacyWideIndex = 132;

	internal const int NarrowIndex = 200;

	internal const int WideIndex = 201;

	private const int ShipyardExpansionSailCapacity = 512;

	private const float WindAxisToleranceDegrees = 0.01f;

	private static readonly int[] RuntimeMaterialSourceIndices = new int[7] { 101, 27, 28, 29, 100, 102, 103 };

	private static GameObject prefabContainer;

	private static bool legacyNarrowIndexWasAvailable;

	private static bool legacyWideIndexWasAvailable;

	internal static GameObject NarrowPrefab { get; private set; }

	internal static GameObject WidePrefab { get; private set; }

	internal static bool EnsureRegistered(PrefabsDirectory directory)
	{
		if (directory == null || directory.sails == null)
		{
			return false;
		}
		NarrowPrefab = GetRegisteredRectangle(directory, 200);
		WidePrefab = GetRegisteredRectangle(directory, 201);
		if (NarrowPrefab == null || WidePrefab == null)
		{
			Build(directory);
			NarrowPrefab = GetRegisteredRectangle(directory, 200);
			WidePrefab = GetRegisteredRectangle(directory, 201);
		}
		if (NarrowPrefab != null)
		{
			EnsureShipyardExpansionComponents(NarrowPrefab);
		}
		if (WidePrefab != null)
		{
			EnsureShipyardExpansionComponents(WidePrefab);
		}
		if (NarrowPrefab != null)
		{
			return WidePrefab != null;
		}
		return false;
	}

	internal static bool CanMigrateLegacyIndex(int prefabIndex)
	{
		return prefabIndex switch
		{
			131 => legacyNarrowIndexWasAvailable, 
			132 => legacyWideIndexWasAvailable, 
			_ => false, 
		};
	}

	internal static void Build(PrefabsDirectory directory)
	{
		if (directory == null || directory.sails == null)
		{
			Plugin.LogSource?.LogError("Could not add Rectangle Junk sails: PrefabsDirectory.sails is unavailable.");
			return;
		}
		if (directory.sails.Length < 512)
		{
			legacyNarrowIndexWasAvailable = false;
			legacyWideIndexWasAvailable = false;
			Plugin.LogSource?.LogError("Could not add Rectangle Junk sails: Shipyard Expansion did not provide its 512-slot sail registry.");
			return;
		}
		legacyNarrowIndexWasAvailable = directory.sails[131] == null;
		legacyWideIndexWasAvailable = directory.sails[132] == null;
		GameObject gameObject = FindRuntimeMaterialSource(directory);
		if (gameObject == null)
		{
			Plugin.LogSource?.LogError("Could not add Rectangle Junk sails: no live Junk Square rendering materials were found.");
			return;
		}
		EnsurePrefabContainer();
		NarrowPrefab = EnsureFinalRectangle(directory, gameObject, RectangleJunkAssets.LoadNarrowPrefab(), 200, "narrow rectangle junk");
		WidePrefab = EnsureFinalRectangle(directory, gameObject, RectangleJunkAssets.LoadWidePrefab(), 201, "wide rectangle junk");
		Plugin.LogSource?.LogInfo((NarrowPrefab != null) ? ("Registered narrow rectangle junk at sail index " + 200 + ".") : "Narrow rectangle junk was not registered.");
		Plugin.LogSource?.LogInfo((WidePrefab != null) ? ("Registered wide rectangle junk at sail index " + 201 + ".") : "Wide rectangle junk was not registered.");
	}

	internal static void AddToShipyard(PrefabsDirectory directory, ref GameObject[] sailPrefabs)
	{
		NarrowPrefab = GetRegisteredRectangle(directory, 200);
		WidePrefab = GetRegisteredRectangle(directory, 201);
		if (NarrowPrefab == null || WidePrefab == null)
		{
			return;
		}
		List<GameObject> list = new List<GameObject>();
		HashSet<GameObject> hashSet = new HashSet<GameObject>();
		if (sailPrefabs != null)
		{
			for (int i = 0; i < sailPrefabs.Length; i++)
			{
				GameObject gameObject = sailPrefabs[i];
				Sail sail = ((gameObject != null) ? gameObject.GetComponent<Sail>() : null);
				if (gameObject != null && (sail == null || !IsRectangleIndex(sail.prefabIndex)) && hashSet.Add(gameObject))
				{
					list.Add(gameObject);
				}
			}
		}
		AddUnique(list, hashSet, NarrowPrefab);
		AddUnique(list, hashSet, WidePrefab);
		sailPrefabs = list.ToArray();
	}

	internal static void RemoveFromShipyard(ref GameObject[] sailPrefabs)
	{
		if (sailPrefabs == null)
		{
			return;
		}
		List<GameObject> list = new List<GameObject>(sailPrefabs.Length);
		for (int i = 0; i < sailPrefabs.Length; i++)
		{
			GameObject gameObject = sailPrefabs[i];
			Sail sail = ((gameObject != null) ? gameObject.GetComponent<Sail>() : null);
			if (sail == null || !IsRectangleIndex(sail.prefabIndex))
			{
				list.Add(gameObject);
			}
		}
		sailPrefabs = list.ToArray();
	}

	internal static void PrepareMast(Mast mast)
	{
		if (!(mast == null) && mast.sails != null)
		{
			for (int i = 0; i < mast.sails.Count; i++)
			{
				GameObject gameObject = mast.sails[i];
				((gameObject != null) ? gameObject.GetComponent<RectangleJunkSailRig>() : null)?.PrepareForMastUpdate();
			}
		}
	}

	internal static void ApplyToMast(Mast mast)
	{
		if (mast == null || mast.sails == null)
		{
			return;
		}
		for (int i = 0; i < mast.sails.Count; i++)
		{
			GameObject gameObject = mast.sails[i];
			RectangleJunkSailRig rectangleJunkSailRig = ((gameObject != null) ? gameObject.GetComponent<RectangleJunkSailRig>() : null);
			if (rectangleJunkSailRig != null && rectangleJunkSailRig.Initialize(mast))
			{
				rectangleJunkSailRig.BindAssignedWinch();
			}
		}
	}

	internal static void Reset()
	{
		NarrowPrefab = null;
		WidePrefab = null;
		legacyNarrowIndexWasAvailable = false;
		legacyWideIndexWasAvailable = false;
		if (prefabContainer != null)
		{
			UnityEngine.Object.Destroy(prefabContainer);
			prefabContainer = null;
		}
	}

	private static void EnsurePrefabContainer()
	{
		if (!(prefabContainer != null))
		{
			prefabContainer = new GameObject("UJSM Rectangle Junk Prefabs");
			UnityEngine.Object.DontDestroyOnLoad(prefabContainer);
			prefabContainer.SetActive(value: false);
		}
	}

	private static GameObject EnsureFinalRectangle(PrefabsDirectory directory, GameObject runtimeMaterialSource, GameObject authoredSource, int prefabIndex, string sailName)
	{
		GameObject gameObject = directory.sails[prefabIndex];
		if (gameObject != null)
		{
			RectangleJunkSailRig component = gameObject.GetComponent<RectangleJunkSailRig>();
			Sail component2 = gameObject.GetComponent<Sail>();
			if (component != null && component2 != null && component2.prefabIndex == prefabIndex)
			{
				EnsureShipyardExpansionComponents(gameObject);
				return gameObject;
			}
			Plugin.LogSource?.LogError("Could not register " + sailName + " at index " + prefabIndex + ": that sail slot is already occupied by " + gameObject.name + ".");
			return null;
		}
		if (authoredSource == null)
		{
			return null;
		}
		GameObject gameObject2 = null;
		try
		{
			gameObject2 = UnityEngine.Object.Instantiate(authoredSource, prefabContainer.transform);
			gameObject2.name = prefabIndex + " SAIL " + sailName;
			ConfigureFinalRectangle(gameObject2, runtimeMaterialSource, prefabIndex, sailName);
			directory.sails[prefabIndex] = gameObject2;
			return gameObject2;
		}
		catch (Exception ex)
		{
			if (gameObject2 != null)
			{
				UnityEngine.Object.DestroyImmediate(gameObject2);
			}
			Plugin.LogSource?.LogError("Could not build authored " + sailName + ": " + ex);
			return null;
		}
	}

	private static void ConfigureFinalRectangle(GameObject clone, GameObject runtimeMaterialSource, int prefabIndex, string sailName)
	{
		Sail sail = RequireComponent<Sail>(clone, "Sail");
		SailConnections connections = RequireComponent<SailConnections>(clone, "SailConnections");
		HingeJoint hinge = RequireComponent<HingeJoint>(clone, "HingeJoint");
		Animator componentInChildren = clone.GetComponentInChildren<Animator>(includeInactive: true);
		Transform transform = FindDescendant((componentInChildren != null) ? componentInChildren.transform : null, "SAIL_junk_square");
		Transform transform2 = FindDescendant(transform, "sail_rope_att__angle_mid_rectangle_");
		if (componentInChildren == null || transform == null || transform2 == null)
		{
			throw new InvalidOperationException("the authored Rectangle Junk hierarchy is incomplete");
		}
		ValidateFinalRuntimeRig(clone, sail, connections, hinge, transform2, prefabIndex, sailName);
		AssignRuntimeMaterials(clone, runtimeMaterialSource);
		clone.AddComponent<RectangleJunkSailRig>();
		EnsureShipyardExpansionComponents(clone);
	}

	private static void EnsureShipyardExpansionComponents(GameObject prefab)
	{
		if (prefab.GetComponent<SailScaler>() == null)
		{
			prefab.AddComponent<SailScaler>();
		}
		if (prefab.GetComponent<SailTextureChanger>() == null)
		{
			prefab.AddComponent<SailTextureChanger>().Setup();
		}
	}

	private static GameObject GetRegisteredRectangle(PrefabsDirectory directory, int prefabIndex)
	{
		if (directory == null || directory.sails == null || prefabIndex < 0 || prefabIndex >= directory.sails.Length)
		{
			return null;
		}
		GameObject gameObject = directory.sails[prefabIndex];
		Sail sail = ((gameObject != null) ? gameObject.GetComponent<Sail>() : null);
		RectangleJunkSailRig rectangleJunkSailRig = ((gameObject != null) ? gameObject.GetComponent<RectangleJunkSailRig>() : null);
		if (!(sail != null) || !(rectangleJunkSailRig != null) || sail.prefabIndex != prefabIndex)
		{
			return null;
		}
		return gameObject;
	}

	private static void ValidateFinalRuntimeRig(GameObject clone, Sail sail, SailConnections connections, HingeJoint hinge, Transform sheetAnchor, int prefabIndex, string sailName)
	{
		RopeControllerSailReef ropeControllerSailReef = connections.reefController as RopeControllerSailReef;
		RopeControllerSailAngle ropeControllerSailAngle = connections.angleControllerMid as RopeControllerSailAngle;
		Transform midRopeAttachment = connections.midRopeAttachment;
		RopeEffect ropeEffect = ((midRopeAttachment != null) ? midRopeAttachment.GetComponent<RopeEffect>() : null);
		RopeEffect ropeEffect2 = ((ropeControllerSailAngle != null) ? ropeControllerSailAngle.GetComponent<RopeEffect>() : null);
		if (sail.prefabIndex != prefabIndex || sail.sailName != sailName || sail.squareSail || sail.obsolete || sail.windcenter == null || Quaternion.Angle(clone.transform.rotation, sail.windcenter.rotation) > 0.01f || connections.sail != sail || ropeControllerSailReef == null || ropeControllerSailAngle == null || midRopeAttachment == null || ropeEffect == null || ropeEffect2 == null || connections.angleControllerLeft != null || connections.angleControllerRight != null || ropeControllerSailAngle.sailHinge != hinge || ropeControllerSailAngle.transform.parent != clone.transform || midRopeAttachment.parent != clone.transform || ropeEffect2.attachment != midRopeAttachment || ropeEffect.attachment != sheetAnchor || !ropeEffect.sheet || ropeControllerSailReef.sail != sail || !ropeControllerSailReef.reverseReefing || clone.GetComponent<SquareAngleMaster>() != null || clone.GetComponent<SquareTopsailAngleMirror>() != null)
		{
			throw new InvalidOperationException("the final Rectangle Junk prefab has invalid serialized vanilla Junk rig or wind-axis configuration");
		}
	}

	private static GameObject FindRuntimeMaterialSource(PrefabsDirectory directory)
	{
		for (int i = 0; i < RuntimeMaterialSourceIndices.Length; i++)
		{
			int num = RuntimeMaterialSourceIndices[i];
			if (num >= 0 && num < directory.sails.Length)
			{
				GameObject gameObject = directory.sails[num];
				if (HasRuntimeMaterials(gameObject))
				{
					return gameObject;
				}
			}
		}
		return null;
	}

	private static bool HasRuntimeMaterials(GameObject candidate)
	{
		Sail sail = ((candidate != null) ? candidate.GetComponent<Sail>() : null);
		Renderer renderer = ((sail != null && sail.cloth != null) ? sail.cloth.GetComponent<Renderer>() : null);
		ReefEffectAnimUniversal reefEffectAnimUniversal = ((candidate != null) ? candidate.GetComponentInChildren<ReefEffectAnimUniversal>(includeInactive: true) : null);
		if (renderer != null && renderer.sharedMaterials.Length != 0 && renderer.sharedMaterials[0] != null && reefEffectAnimUniversal != null && reefEffectAnimUniversal.furledSail != null && reefEffectAnimUniversal.furledSail.sharedMaterials.Length != 0)
		{
			return reefEffectAnimUniversal.furledSail.sharedMaterials[0] != null;
		}
		return false;
	}

	private static void AssignRuntimeMaterials(GameObject target, GameObject source)
	{
		Sail sail = RequireComponent<Sail>(target, "Sail");
		Sail sail2 = RequireComponent<Sail>(source, "Junk Square Sail");
		Renderer obj = ((sail.cloth != null) ? sail.cloth.GetComponent<Renderer>() : null);
		Renderer renderer = ((sail2.cloth != null) ? sail2.cloth.GetComponent<Renderer>() : null);
		ReefEffectAnimUniversal componentInChildren = target.GetComponentInChildren<ReefEffectAnimUniversal>(includeInactive: true);
		ReefEffectAnimUniversal componentInChildren2 = source.GetComponentInChildren<ReefEffectAnimUniversal>(includeInactive: true);
		if (obj == null || renderer == null || componentInChildren == null || componentInChildren.furledSail == null || componentInChildren2 == null || componentInChildren2.furledSail == null || renderer.sharedMaterials.Length == 0 || componentInChildren2.furledSail.sharedMaterials.Length == 0)
		{
			throw new InvalidOperationException("the live Junk Square rendering materials are incomplete");
		}
		obj.sharedMaterials = renderer.sharedMaterials;
		componentInChildren.furledSail.sharedMaterials = componentInChildren2.furledSail.sharedMaterials;
	}

	internal static bool IsRectangle(GameObject sailObject)
	{
		Sail sail = ((sailObject != null) ? sailObject.GetComponent<Sail>() : null);
		if (sail != null)
		{
			return IsRectangleIndex(sail.prefabIndex);
		}
		return false;
	}

	private static bool IsRectangleIndex(int prefabIndex)
	{
		if (prefabIndex != 200)
		{
			return prefabIndex == 201;
		}
		return true;
	}

	internal static void SynchronizeCollisionCheckerScale(Sail sail)
	{
		if (!(sail == null) && IsRectangle(sail.gameObject) && !(sail.cloth == null) && !(sail.cloth.transform.parent == null))
		{
			SailConnections component = sail.GetComponent<SailConnections>();
			Transform transform = ((component != null && component.colChecker != null) ? component.colChecker.transform : null);
			if (!(transform == null) && !(transform.parent == null) && transform.parent.gameObject.layer == 8)
			{
				Vector3 localScale = sail.cloth.transform.parent.localScale;
				transform.localScale = new Vector3(localScale.x, localScale.z, localScale.y);
			}
		}
	}

	internal static void FurlNewlyInstalledSail(GameObject sailObject)
	{
		if (IsRectangle(sailObject))
		{
			Sail component = sailObject.GetComponent<Sail>();
			SailConnections component2 = sailObject.GetComponent<SailConnections>();
			RopeControllerSailReef ropeControllerSailReef = ((component2 != null) ? (component2.reefController as RopeControllerSailReef) : null);
			if (ropeControllerSailReef == null)
			{
				Plugin.LogSource?.LogWarning("Could not furl newly installed Rectangle Junk: its reef controller is missing.");
				return;
			}
			component.currentUnroll = 0f;
			ropeControllerSailReef.reverseReefing = true;
			ropeControllerSailReef.currentLength = 1f;
			ropeControllerSailReef.changed = true;
		}
	}

	internal static void SynchronizeWinchVisibility(Mast mast)
	{
		if (mast == null)
		{
			return;
		}
		bool hasRectangle = false;
		if (mast.sails != null)
		{
			for (int i = 0; i < mast.sails.Count; i++)
			{
				if (IsRectangle(mast.sails[i]))
				{
					hasRectangle = true;
					break;
				}
			}
		}
		HashSet<GPButtonRopeWinch> seen = new HashSet<GPButtonRopeWinch>();
		SynchronizeWinchArray(mast.reefWinch, hasRectangle, seen);
		SynchronizeWinchArray(mast.midAngleWinch, hasRectangle, seen);
		SynchronizeWinchArray(mast.leftAngleWinch, hasRectangle, seen);
		SynchronizeWinchArray(mast.rightAngleWinch, hasRectangle, seen);
	}

	private static void SynchronizeWinchArray(GPButtonRopeWinch[] winches, bool hasRectangle, HashSet<GPButtonRopeWinch> seen)
	{
		if (winches == null)
		{
			return;
		}
		foreach (GPButtonRopeWinch gPButtonRopeWinch in winches)
		{
			if (!(gPButtonRopeWinch == null) && seen.Add(gPButtonRopeWinch))
			{
				RectangleJunkWinchVisibility rectangleJunkWinchVisibility = gPButtonRopeWinch.GetComponent<RectangleJunkWinchVisibility>();
				if (((rectangleJunkWinchVisibility == null) & hasRectangle) && gPButtonRopeWinch.rope == null)
				{
					rectangleJunkWinchVisibility = gPButtonRopeWinch.gameObject.AddComponent<RectangleJunkWinchVisibility>();
				}
				rectangleJunkWinchVisibility?.SetHidden(hasRectangle && gPButtonRopeWinch.rope == null);
			}
		}
	}

	private static T RequireComponent<T>(GameObject target, string componentName) where T : Component
	{
		T component = target.GetComponent<T>();
		if (component == null)
		{
			throw new InvalidOperationException(componentName + " is missing");
		}
		return component;
	}

	private static Transform FindDescendant(Transform root, string name)
	{
		if (root == null)
		{
			return null;
		}
		Transform[] componentsInChildren = root.GetComponentsInChildren<Transform>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			if (componentsInChildren[i].name == name)
			{
				return componentsInChildren[i];
			}
		}
		return null;
	}

	private static void AddUnique(List<GameObject> result, HashSet<GameObject> seen, GameObject prefab)
	{
		if (prefab != null && seen.Add(prefab))
		{
			result.Add(prefab);
		}
	}
}
