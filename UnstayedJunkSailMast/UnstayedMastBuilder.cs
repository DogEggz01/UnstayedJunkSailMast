using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Logging;
using UnityEngine;

namespace UnstayedJunkSailMast;

internal static class UnstayedMastBuilder
{
	private sealed class HierarchyTransformSnapshot
	{
		internal Transform Target;

		internal Transform Parent;

		internal Vector3 LocalPosition;

		internal Quaternion LocalRotation;

		internal Vector3 LocalScale;
	}

	private sealed class SurfaceFittingPlacement
	{
		internal Transform Target;

		internal Vector2 RootRadial;

		internal float SourceSurfaceRadius;

		internal Vector2 SourceSurfaceRootPoint;

		internal Vector3 OriginalWorldPosition;
	}

	private static readonly string[] SmallJunkAnchorPaths = new string[3] { "junk small/structure/mast", "junk small/structure/mast_center", "junk small/structure/mast_001" };

	private static readonly string[] MediumJunkAnchorPaths = new string[5] { "junk medium (actual)/structure/mast_mid_0", "junk medium (actual)/structure/mast_mid_1", "junk medium (actual)/structure/mast_mizzen_0", "junk medium (actual)/structure/mast_mizzen_1", "junk medium (actual)/structure/mast_front_" };

	private static readonly string[] LargeJunkAnchorPaths = new string[4] { "junk large (3)/junk large (3)/structure/masts_structure/mast_main_1", "junk large (3)/junk large (3)/structure/masts_structure/mast_main_2", "junk large (3)/junk large (3)/structure/masts_structure/mast_back", "junk large (3)/junk large (3)/structure/masts_structure/mast_front" };

	private const float DiameterScale = 1.41f;

	private const float MaxSurfaceFittingOffset = 1f;

	private static readonly HashSet<string> SmallJunkRemovedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "mast_holder", "mast_holder_001", "mast_holder_002" };

	private const float MaximumSurfaceCorrection = 1f;

	private const float MinimumMastSectionBand = 0.01f;

	private const float MastSectionBandFraction = 0.0025f;

	internal static void TryBuild(SaveableBoatCustomization customization, BoatCustomParts parts, BoatRefs refs)
	{
		if (customization == null || parts == null || refs == null || customization.GetComponent<UnstayedBoatMarker>() != null)
		{
			return;
		}
		SaveableObject component = customization.GetComponent<SaveableObject>();
		if (component == null || !IsSupportedBoat(component.sceneIndex) || !HasShipyardExpansionMastCapacity(refs))
		{
			return;
		}
		List<BoatPart> list = FindTargetMastParts(customization.transform, parts, component.sceneIndex);
		if (list.Count == 0)
		{
			Plugin.LogSource?.LogError("No eligible mast part groups were found on " + customization.name + ".");
			return;
		}
		Dictionary<BoatPart, List<BoatPartOption>> dictionary = new Dictionary<BoatPart, List<BoatPartOption>>();
		for (int i = 0; i < list.Count; i++)
		{
			dictionary[list[i]] = GetEligibleSources(list[i]);
		}
		List<RestrictedPartSelection> selections = FindRestrictedSelections(customization.transform, parts, list, dictionary);
		List<UnstayedMastProfile> list2 = new List<UnstayedMastProfile>();
		UnstayedMastIndexAllocator unstayedMastIndexAllocator = new UnstayedMastIndexAllocator(customization.transform, component.sceneIndex);
		for (int j = 0; j < list.Count; j++)
		{
			BoatPart boatPart = list[j];
			List<BoatPartOption> list3 = dictionary[boatPart];
			if (list3.Count == 0)
			{
				continue;
			}
			List<RestrictedPartSelection> restrictedSelectionsForMast = GetRestrictedSelectionsForMast(selections, boatPart);
			for (int k = 0; k < list3.Count; k++)
			{
				BoatPartOption boatPartOption = list3[k];
				UnstayedMastSourceIdentity identity = UnstayedMastSourceIdentity.Create(boatPartOption, boatPart, customization.transform, component.sceneIndex);
				if (unstayedMastIndexAllocator.TryClaim(boatPartOption, identity, out var mastIndex, out var usesFixedVanillaIndex))
				{
					BoatPartOption boatPartOption2 = CloneUnstayedMast(boatPartOption, component.sceneIndex, mastIndex, identity, restrictedSelectionsForMast);
					if (!(boatPartOption2 == null))
					{
						boatPart.partOptions.Add(boatPartOption2);
						AddMutualRestrictions(boatPartOption2, restrictedSelectionsForMast);
						list2.Add(new UnstayedMastProfile
						{
							MastPart = boatPart,
							UnstayedOption = boatPartOption2,
							Marker = boatPartOption2.GetComponent<UnstayedMastMarker>(),
							UsesFixedVanillaIndex = usesFixedVanillaIndex,
							RestrictedSelections = new List<RestrictedPartSelection>(restrictedSelectionsForMast)
						});
					}
				}
			}
		}
		unstayedMastIndexAllocator.Commit();
		if (list2.Count == 0)
		{
			Plugin.LogSource?.LogError("No unstayed mast options were created on " + customization.name + ".");
			return;
		}
		RestrictNoShroudsToUnstayedMasts(selections);
		customization.gameObject.AddComponent<UnstayedBoatMarker>();
		UnstayedBoatRegistry.Register(new UnstayedBoatProfile
		{
			Parts = parts,
			Refs = refs,
			SceneIndex = component.sceneIndex,
			Masts = list2,
			RetiredMastIndices = unstayedMastIndexAllocator.GetRetiredIndices()
		});
		int num = 0;
		for (int l = 0; l < list2.Count; l++)
		{
			if (list2[l].UsesFixedVanillaIndex)
			{
				num++;
			}
		}
		Plugin.LogSource?.LogInfo("Added " + list2.Count + " unstayed mast option(s) to " + customization.name + " (scene " + component.sceneIndex + "): " + num + " fixed vanilla, " + (list2.Count - num) + " extended.");
	}

	private static bool IsSupportedBoat(int sceneIndex)
	{
		if (sceneIndex != 90 && sceneIndex != 80)
		{
			return sceneIndex == 70;
		}
		return true;
	}

	private static bool HasShipyardExpansionMastCapacity(BoatRefs refs)
	{
		if (refs.masts == null || refs.masts.Length < 128)
		{
			Plugin.LogSource?.LogError("Could not add unstayed masts: Shipyard Expansion did not provide its 128-slot mast registry.");
			return false;
		}
		return true;
	}

	private static List<BoatPart> FindTargetMastParts(Transform boat, BoatCustomParts parts, int sceneIndex)
	{
		List<BoatPart> list = new List<BoatPart>();
		string[] array = sceneIndex switch
		{
			80 => MediumJunkAnchorPaths, 
			90 => SmallJunkAnchorPaths, 
			_ => LargeJunkAnchorPaths, 
		};
		for (int i = 0; i < array.Length; i++)
		{
			Transform transform = boat.Find(array[i]);
			if (transform == null)
			{
				transform = FindUniqueMastByName(boat, GetLastPathSegment(array[i]));
			}
			BoatPartOption option = ((transform != null) ? transform.GetComponent<BoatPartOption>() : null);
			BoatPart value = FindContainingPart(parts, option);
			AddUnique(list, value);
		}
		for (int j = 0; j < parts.availableParts.Count; j++)
		{
			BoatPart boatPart = parts.availableParts[j];
			if (boatPart != null && boatPart.category == 0 && ContainsEligibleMast(boatPart))
			{
				AddUnique(list, boatPart);
			}
		}
		list.Sort((BoatPart left, BoatPart right) => parts.availableParts.IndexOf(left).CompareTo(parts.availableParts.IndexOf(right)));
		return list;
	}

	private static Transform FindUniqueMastByName(Transform root, string name)
	{
		Mast[] componentsInChildren = root.GetComponentsInChildren<Mast>(includeInactive: true);
		Transform transform = null;
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			if (string.Equals(componentsInChildren[i].name, name, StringComparison.OrdinalIgnoreCase) && !(componentsInChildren[i].GetComponent<BoatPartOption>() == null))
			{
				if (transform != null)
				{
					return null;
				}
				transform = componentsInChildren[i].transform;
			}
		}
		return transform;
	}

	private static string GetLastPathSegment(string path)
	{
		int num = path.LastIndexOf('/');
		if (num < 0)
		{
			return path;
		}
		return path.Substring(num + 1);
	}

	private static BoatPart FindContainingPart(BoatCustomParts parts, BoatPartOption option)
	{
		if (option == null)
		{
			return null;
		}
		for (int i = 0; i < parts.availableParts.Count; i++)
		{
			BoatPart boatPart = parts.availableParts[i];
			if (boatPart != null && boatPart.partOptions != null && boatPart.partOptions.Contains(option))
			{
				return boatPart;
			}
		}
		return null;
	}

	private static bool ContainsEligibleMast(BoatPart part)
	{
		if (part.partOptions == null)
		{
			return false;
		}
		for (int i = 0; i < part.partOptions.Count; i++)
		{
			if (ShouldCloneSource(part.partOptions[i]))
			{
				return true;
			}
		}
		return false;
	}

	private static List<BoatPartOption> GetEligibleSources(BoatPart part)
	{
		List<BoatPartOption> list = new List<BoatPartOption>();
		BoatPartOption[] array = part.partOptions.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			if (ShouldCloneSource(array[i]))
			{
				list.Add(array[i]);
			}
		}
		return list;
	}

	private static bool ShouldCloneSource(BoatPartOption option)
	{
		return IsEligibleSource(option);
	}

	private static bool IsEligibleSource(BoatPartOption option)
	{
		if (option == null || option.GetComponent<Mast>() == null || option.GetComponent<UnstayedMastMarker>() != null)
		{
			return false;
		}
		Mast component = option.GetComponent<Mast>();
		string text = UnstayedNameRules.Normalize(option.optionName + " " + option.name + " " + GetHierarchyPath(option.transform));
		if (text.Contains("bermuda") || text.Contains("bowsprit") || text.Contains("forestay") || text.Contains("midstay") || text.Contains("front stay") || text.Contains("back stay") || text.Contains("mast stay") || text.Contains("sprit mast") || text.StartsWith("unstayed ", StringComparison.Ordinal) || component.onlyStaysails)
		{
			return false;
		}
		return true;
	}

	private static string GetHierarchyPath(Transform transform, Transform stopAt = null)
	{
		if (transform == null)
		{
			return string.Empty;
		}
		List<string> list = new List<string>();
		Transform transform2 = transform;
		while (transform2 != null && transform2 != stopAt)
		{
			list.Add(transform2.name);
			transform2 = transform2.parent;
		}
		list.Reverse();
		return string.Join("/", list.ToArray());
	}

	private static string GetRelativePath(Transform transform, Transform root)
	{
		if (transform == root)
		{
			return string.Empty;
		}
		List<string> list = new List<string>();
		Transform transform2 = transform;
		while (transform2 != null && transform2 != root)
		{
			list.Add(transform2.name);
			transform2 = transform2.parent;
		}
		if (transform2 != root)
		{
			return null;
		}
		list.Reverse();
		return string.Join("/", list.ToArray());
	}

	private static bool IsWithin(Transform transform, Transform root)
	{
		if (transform == null || root == null)
		{
			return false;
		}
		if (!(transform == root))
		{
			return transform.IsChildOf(root);
		}
		return true;
	}

	private static bool Contains(List<BoatPartOption> options, BoatPartOption value)
	{
		return options?.Contains(value) ?? false;
	}

	private static void AddUnique<T>(List<T> list, T value) where T : class
	{
		if (value != null && !list.Contains(value))
		{
			list.Add(value);
		}
	}

	private static BoatPartOption CloneUnstayedMast(BoatPartOption sourceOption, int sceneIndex, int mastIndex, UnstayedMastSourceIdentity identity, List<RestrictedPartSelection> restrictedSelections)
	{
		Mast component = sourceOption.GetComponent<Mast>();
		if (component == null || component.walkColMast == null)
		{
			Plugin.LogSource?.LogError("Skipped " + sourceOption.optionName + ": source mast or walk collider is missing.");
			return null;
		}
		string text = (string.IsNullOrWhiteSpace(sourceOption.optionName) ? sourceOption.name : sourceOption.optionName);
		Transform transform = null;
		Transform transform2 = null;
		try
		{
			bool activeSelf = sourceOption.gameObject.activeSelf;
			try
			{
				sourceOption.gameObject.SetActive(value: false);
				transform = UnityEngine.Object.Instantiate(sourceOption.transform, sourceOption.transform.parent);
				transform.gameObject.SetActive(value: false);
				transform2 = UnityEngine.Object.Instantiate(component.walkColMast, component.walkColMast.parent);
				transform2.gameObject.SetActive(value: false);
			}
			finally
			{
				sourceOption.gameObject.SetActive(activeSelf);
			}
			InitializeCloneTransforms(sourceOption, component, transform, transform2);
			Mast component2 = transform.GetComponent<Mast>();
			BoatPartOption component3 = transform.GetComponent<BoatPartOption>();
			if (component2 == null || component3 == null)
			{
				throw new InvalidOperationException("cloned components are missing");
			}
			InitializeClonedMast(component2, transform2, sourceOption, mastIndex);
			InitializeClonedOption(component3, transform2, sourceOption, text, sceneIndex);
			DisableClonedRiggingVisuals(sourceOption, component.walkColMast, component3, transform2, restrictedSelections);
			PruneHierarchy(transform, sceneIndex);
			PruneHierarchy(transform2, sceneIndex);
			Dictionary<Collider, Collider> visualColliderMap = ExtractScaledRootBodyAndPlaceFittings(transform, "UJSM_scaled_mast_body", forceTriggerColliders: true, isWalkBody: false);
			Dictionary<Collider, Collider> walkColliderMap = ExtractScaledRootBodyAndPlaceFittings(transform2, "UJSM_scaled_walk_body", forceTriggerColliders: false, isWalkBody: true);
			component2.mastCols = CloneMastColliders(component, component2, sceneIndex, visualColliderMap, walkColliderMap);
			component2.walkColMast = transform2;
			component3.walkColObject = transform2.gameObject;
			UnstayedMastMarker unstayedMastMarker = transform.gameObject.AddComponent<UnstayedMastMarker>();
			unstayedMastMarker.Identity = identity;
			unstayedMastMarker.DiameterScale = 1.41f;
			return component3;
		}
		catch (Exception ex)
		{
			if (transform != null)
			{
				UnityEngine.Object.DestroyImmediate(transform.gameObject);
			}
			if (transform2 != null)
			{
				UnityEngine.Object.DestroyImmediate(transform2.gameObject);
			}
			Plugin.LogSource?.LogError("Skipped " + text + ": " + ex);
			return null;
		}
	}

	private static void InitializeCloneTransforms(BoatPartOption sourceOption, Mast sourceMast, Transform cloneTransform, Transform cloneWalk)
	{
		cloneTransform.name = "Unstayed_" + sourceOption.name;
		cloneWalk.name = "Unstayed_" + sourceMast.walkColMast.name;
		cloneTransform.localPosition = sourceOption.transform.localPosition;
		cloneTransform.localEulerAngles = sourceOption.transform.localEulerAngles;
		cloneTransform.localScale = sourceOption.transform.localScale;
		cloneWalk.localPosition = sourceMast.walkColMast.localPosition;
		cloneWalk.localEulerAngles = sourceMast.walkColMast.localEulerAngles;
		cloneWalk.localScale = sourceMast.walkColMast.localScale;
	}

	private static void InitializeClonedMast(Mast cloneMast, Transform cloneWalk, BoatPartOption sourceOption, int mastIndex)
	{
		cloneMast.orderIndex = mastIndex;
		cloneMast.walkColMast = cloneWalk;
		cloneMast.shipRigidbody = sourceOption.GetComponentInParent<Rigidbody>();
		cloneMast.startSailPrefab = null;
		cloneMast.startSailPrefabs = new GameObject[0];
		cloneMast.startSailsHeightOffsets = new float[0];
	}

	private static void InitializeClonedOption(BoatPartOption cloneOption, Transform cloneWalk, BoatPartOption sourceOption, string sourceDisplayName, int sceneIndex)
	{
		cloneOption.optionName = "Unstayed " + sourceDisplayName;
		cloneOption.mass = sourceOption.mass * 2;
		cloneOption.walkColObject = cloneWalk.gameObject;
		cloneOption.requires = cloneOption.requires ?? new List<BoatPartOption>();
		cloneOption.requiresDisabled = cloneOption.requiresDisabled ?? new List<BoatPartOption>();
		cloneOption.childOptions = FilterChildOptions(cloneOption.childOptions, sceneIndex);
	}

	private static void DisableClonedRiggingVisuals(BoatPartOption sourceOption, Transform sourceWalk, BoatPartOption cloneOption, Transform cloneWalk, List<RestrictedPartSelection> selections)
	{
		HashSet<GameObject> hashSet = new HashSet<GameObject>();
		for (int i = 0; i < selections.Count; i++)
		{
			RestrictedPartSelection restrictedPartSelection = selections[i];
			if (restrictedPartSelection.Kind != RestrictedPartKind.Rigging && restrictedPartSelection.Kind != RestrictedPartKind.RiggingAccessory)
			{
				continue;
			}
			for (int j = 0; j < restrictedPartSelection.NonEmptyOptions.Count; j++)
			{
				BoatPartOption boatPartOption = restrictedPartSelection.NonEmptyOptions[j];
				if (boatPartOption == null || boatPartOption.childOptions == null)
				{
					continue;
				}
				for (int k = 0; k < boatPartOption.childOptions.Length; k++)
				{
					GameObject gameObject = boatPartOption.childOptions[k];
					if (!(gameObject == null))
					{
						AddMappedVisual(hashSet, gameObject.transform, sourceOption.transform, cloneOption.transform);
						AddMappedVisual(hashSet, gameObject.transform, sourceWalk, cloneWalk);
					}
				}
			}
		}
		AddOrphanedTelltales(hashSet, cloneOption.transform);
		AddOrphanedTelltales(hashSet, cloneWalk);
		if (hashSet.Count == 0)
		{
			return;
		}
		foreach (GameObject item in hashSet)
		{
			if (item != null)
			{
				item.SetActive(value: false);
			}
		}
		List<GameObject> list = new List<GameObject>();
		if (cloneOption.childOptions != null)
		{
			for (int l = 0; l < cloneOption.childOptions.Length; l++)
			{
				GameObject gameObject2 = cloneOption.childOptions[l];
				if (gameObject2 != null && !hashSet.Contains(gameObject2))
				{
					list.Add(gameObject2);
				}
			}
		}
		cloneOption.childOptions = list.ToArray();
	}

	private static void AddMappedVisual(HashSet<GameObject> result, Transform sourceVisual, Transform sourceRoot, Transform cloneRoot)
	{
		Transform transform = MapToClone(sourceVisual, sourceRoot, cloneRoot);
		if (transform != null && transform != cloneRoot)
		{
			result.Add(transform.gameObject);
		}
	}

	private static void AddOrphanedTelltales(HashSet<GameObject> result, Transform mastRoot)
	{
		if (mastRoot == null)
		{
			return;
		}
		Transform[] componentsInChildren = mastRoot.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren)
		{
			if (!(transform == mastRoot))
			{
				string text = UnstayedNameRules.Normalize(transform.name);
				bool flag = text.Contains("telltale");
				bool flag2 = text == "wind flag" && transform.parent == mastRoot;
				if (flag | flag2)
				{
					result.Add(transform.gameObject);
				}
			}
		}
	}

	private static Dictionary<Collider, Collider> ExtractScaledRootBodyAndPlaceFittings(Transform root, string bodyName, bool forceTriggerColliders, bool isWalkBody)
	{
		if (root == null)
		{
			throw new ArgumentNullException("root");
		}
		Vector3 localScale = root.localScale;
		List<HierarchyTransformSnapshot> hierarchy = CaptureHierarchy(root);
		List<SurfaceFittingPlacement> list = CaptureSurfaceFittings(root);
		GameObject gameObject = new GameObject(bodyName);
		gameObject.layer = root.gameObject.layer;
		gameObject.tag = root.gameObject.tag;
		gameObject.isStatic = root.gameObject.isStatic;
		UnstayedScaledBodyMarker unstayedScaledBodyMarker = gameObject.AddComponent<UnstayedScaledBodyMarker>();
		unstayedScaledBodyMarker.LogicalRoot = root;
		unstayedScaledBodyMarker.IsWalkBody = isWalkBody;
		Transform transform = gameObject.transform;
		transform.SetParent(root, worldPositionStays: false);
		transform.localPosition = Vector3.zero;
		transform.localRotation = Quaternion.identity;
		transform.localScale = new Vector3(1.41f, 1.41f, 1f);
		bool num = CopyRootRenderer(root, gameObject);
		Dictionary<Collider, Collider> dictionary = CopyRootColliders(root, gameObject, forceTriggerColliders);
		if (!num && dictionary.Count == 0)
		{
			UnityEngine.Object.DestroyImmediate(gameObject);
			throw new InvalidOperationException("root mast body has no renderer or collider components");
		}
		if (list.Count > 0)
		{
			PlaceSurfaceFittings(root, list);
		}
		ValidateExtractedRootBody(root, transform, localScale, hierarchy, list);
		return dictionary;
	}

	private static List<HierarchyTransformSnapshot> CaptureHierarchy(Transform root)
	{
		List<HierarchyTransformSnapshot> list = new List<HierarchyTransformSnapshot>();
		Transform[] componentsInChildren = root.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren)
		{
			if (!(transform == root))
			{
				list.Add(new HierarchyTransformSnapshot
				{
					Target = transform,
					Parent = transform.parent,
					LocalPosition = transform.localPosition,
					LocalRotation = transform.localRotation,
					LocalScale = transform.localScale
				});
			}
		}
		return list;
	}

	private static void ValidateExtractedRootBody(Transform root, Transform body, Vector3 sourceRootScale, List<HierarchyTransformSnapshot> hierarchy, List<SurfaceFittingPlacement> surfaceFittings)
	{
		if ((root.localScale - sourceRootScale).sqrMagnitude > 1E-08f || body.parent != root || body.localPosition.sqrMagnitude > 1E-08f || Quaternion.Angle(body.localRotation, Quaternion.identity) > 0.001f || (body.localScale - new Vector3(1.41f, 1.41f, 1f)).sqrMagnitude > 1E-08f)
		{
			throw new InvalidOperationException("scaled mast body hierarchy is invalid");
		}
		HashSet<Transform> hashSet = new HashSet<Transform>();
		for (int i = 0; i < surfaceFittings.Count; i++)
		{
			hashSet.Add(surfaceFittings[i].Target);
		}
		for (int j = 0; j < hierarchy.Count; j++)
		{
			HierarchyTransformSnapshot hierarchyTransformSnapshot = hierarchy[j];
			Transform target = hierarchyTransformSnapshot.Target;
			if (target == null || target.parent != hierarchyTransformSnapshot.Parent || Quaternion.Angle(target.localRotation, hierarchyTransformSnapshot.LocalRotation) > 0.001f || (target.localScale - hierarchyTransformSnapshot.LocalScale).sqrMagnitude > 1E-08f || (!hashSet.Contains(target) && (target.localPosition - hierarchyTransformSnapshot.LocalPosition).sqrMagnitude > 1E-08f))
			{
				throw new InvalidOperationException("mast child hierarchy changed unexpectedly at " + ((target != null) ? target.name : "destroyed child"));
			}
		}
	}

	private static bool CopyRootRenderer(Transform sourceRoot, GameObject target)
	{
		MeshRenderer component = sourceRoot.GetComponent<MeshRenderer>();
		MeshFilter component2 = sourceRoot.GetComponent<MeshFilter>();
		Renderer component3 = sourceRoot.GetComponent<Renderer>();
		if (component == null)
		{
			if (component3 != null)
			{
				throw new InvalidOperationException("unsupported root renderer type " + component3.GetType().Name);
			}
			return false;
		}
		if (component2 == null || component2.sharedMesh == null)
		{
			throw new InvalidOperationException("root MeshRenderer has no source mesh");
		}
		target.AddComponent<MeshFilter>().sharedMesh = component2.sharedMesh;
		MeshRenderer meshRenderer = target.AddComponent<MeshRenderer>();
		meshRenderer.sharedMaterials = component.sharedMaterials;
		meshRenderer.shadowCastingMode = component.shadowCastingMode;
		meshRenderer.receiveShadows = component.receiveShadows;
		meshRenderer.lightProbeUsage = component.lightProbeUsage;
		meshRenderer.reflectionProbeUsage = component.reflectionProbeUsage;
		meshRenderer.probeAnchor = component.probeAnchor;
		meshRenderer.motionVectorGenerationMode = component.motionVectorGenerationMode;
		meshRenderer.allowOcclusionWhenDynamic = component.allowOcclusionWhenDynamic;
		meshRenderer.sortingLayerID = component.sortingLayerID;
		meshRenderer.sortingOrder = component.sortingOrder;
		meshRenderer.enabled = component.enabled;
		component.enabled = false;
		return true;
	}

	private static Dictionary<Collider, Collider> CopyRootColliders(Transform sourceRoot, GameObject target, bool forceTrigger)
	{
		Dictionary<Collider, Collider> dictionary = new Dictionary<Collider, Collider>();
		Collider[] components = sourceRoot.GetComponents<Collider>();
		foreach (Collider collider in components)
		{
			Collider collider2 = CopyCollider(collider, target);
			collider2.sharedMaterial = collider.sharedMaterial;
			collider2.isTrigger = forceTrigger || collider.isTrigger;
			collider2.contactOffset = collider.contactOffset;
			collider2.enabled = collider.enabled;
			collider.enabled = false;
			dictionary[collider] = collider2;
		}
		return dictionary;
	}

	private static Collider CopyCollider(Collider source, GameObject target)
	{
		CapsuleCollider capsuleCollider = source as CapsuleCollider;
		if (capsuleCollider != null)
		{
			CapsuleCollider capsuleCollider2 = target.AddComponent<CapsuleCollider>();
			capsuleCollider2.center = capsuleCollider.center;
			capsuleCollider2.radius = capsuleCollider.radius;
			capsuleCollider2.height = capsuleCollider.height;
			capsuleCollider2.direction = capsuleCollider.direction;
			return capsuleCollider2;
		}
		MeshCollider meshCollider = source as MeshCollider;
		if (meshCollider != null)
		{
			MeshCollider meshCollider2 = target.AddComponent<MeshCollider>();
			meshCollider2.sharedMesh = meshCollider.sharedMesh;
			meshCollider2.convex = meshCollider.convex;
			meshCollider2.cookingOptions = meshCollider.cookingOptions;
			return meshCollider2;
		}
		BoxCollider boxCollider = source as BoxCollider;
		if (boxCollider != null)
		{
			BoxCollider boxCollider2 = target.AddComponent<BoxCollider>();
			boxCollider2.center = boxCollider.center;
			boxCollider2.size = boxCollider.size;
			return boxCollider2;
		}
		SphereCollider sphereCollider = source as SphereCollider;
		if (sphereCollider != null)
		{
			SphereCollider sphereCollider2 = target.AddComponent<SphereCollider>();
			sphereCollider2.center = sphereCollider.center;
			sphereCollider2.radius = sphereCollider.radius;
			return sphereCollider2;
		}
		throw new InvalidOperationException("unsupported root collider type " + source.GetType().Name);
	}

	private static GameObject[] FilterChildOptions(GameObject[] childOptions, int sceneIndex)
	{
		if (childOptions == null || childOptions.Length == 0)
		{
			return new GameObject[0];
		}
		List<GameObject> list = new List<GameObject>();
		foreach (GameObject gameObject in childOptions)
		{
			if (gameObject != null && !ShouldRemove(gameObject.transform, sceneIndex))
			{
				list.Add(gameObject);
			}
		}
		return list.ToArray();
	}

	private static void PruneHierarchy(Transform root, int sceneIndex)
	{
		Transform[] componentsInChildren = root.GetComponentsInChildren<Transform>(includeInactive: true);
		for (int num = componentsInChildren.Length - 1; num >= 0; num--)
		{
			Transform transform = componentsInChildren[num];
			if (transform != root && ShouldRemove(transform, sceneIndex))
			{
				UnityEngine.Object.DestroyImmediate(transform.gameObject);
			}
		}
	}

	private static bool ShouldRemove(Transform transform, int sceneIndex)
	{
		if (transform == null)
		{
			return false;
		}
		if (sceneIndex == 90 && SmallJunkRemovedNames.Contains(transform.name))
		{
			return true;
		}
		string text = UnstayedNameRules.Normalize(transform.name + " " + GetHierarchyPath(transform));
		if (!text.Contains("static rig") && !text.Contains("static rope atts") && !text.Contains("static rope attachment") && !text.Contains("shroud") && !text.Contains("crowsnest") && !text.Contains("crows nest") && !text.Contains("crownest"))
		{
			return text.Contains("crow nest");
		}
		return true;
	}

	private static CapsuleCollider[] CloneMastColliders(Mast source, Mast clone, int sceneIndex, Dictionary<Collider, Collider> visualColliderMap, Dictionary<Collider, Collider> walkColliderMap)
	{
		if (source.mastCols == null)
		{
			return new CapsuleCollider[0];
		}
		List<CapsuleCollider> list = new List<CapsuleCollider>();
		for (int i = 0; i < source.mastCols.Length; i++)
		{
			CapsuleCollider capsuleCollider = source.mastCols[i];
			if (!(capsuleCollider == null) && !ShouldRemove(capsuleCollider.transform, sceneIndex))
			{
				CapsuleCollider capsuleCollider2 = MapCollider(capsuleCollider, source.transform, clone.transform);
				if (capsuleCollider2 == null && source.walkColMast != null && clone.walkColMast != null)
				{
					capsuleCollider2 = MapCollider(capsuleCollider, source.walkColMast, clone.walkColMast);
				}
				capsuleCollider2 = ResolveScaledCapsule(capsuleCollider2, visualColliderMap, walkColliderMap);
				if (capsuleCollider2 != null)
				{
					AddUnique(list, capsuleCollider2);
				}
				else if (!IsWithin(capsuleCollider.transform, source.transform) && !IsWithin(capsuleCollider.transform, source.walkColMast))
				{
					AddUnique(list, capsuleCollider);
				}
			}
		}
		return list.ToArray();
	}

	private static CapsuleCollider ResolveScaledCapsule(CapsuleCollider mapped, Dictionary<Collider, Collider> visualColliderMap, Dictionary<Collider, Collider> walkColliderMap)
	{
		if (mapped == null)
		{
			return null;
		}
		if (visualColliderMap != null && visualColliderMap.TryGetValue(mapped, out var value))
		{
			return value as CapsuleCollider;
		}
		if (walkColliderMap != null && walkColliderMap.TryGetValue(mapped, out value))
		{
			return value as CapsuleCollider;
		}
		return mapped;
	}

	private static CapsuleCollider MapCollider(CapsuleCollider sourceCollider, Transform sourceRoot, Transform cloneRoot)
	{
		Transform transform = MapToClone(sourceCollider.transform, sourceRoot, cloneRoot);
		if (!(transform != null))
		{
			return null;
		}
		return transform.GetComponent<CapsuleCollider>();
	}

	private static Transform MapToClone(Transform sourceTransform, Transform sourceRoot, Transform cloneRoot)
	{
		if (!IsWithin(sourceTransform, sourceRoot))
		{
			return null;
		}
		string relativePath = GetRelativePath(sourceTransform, sourceRoot);
		if (!string.IsNullOrEmpty(relativePath))
		{
			return cloneRoot.Find(relativePath);
		}
		return cloneRoot;
	}

	private static List<SurfaceFittingPlacement> CaptureSurfaceFittings(Transform mastRoot)
	{
		List<SurfaceFittingPlacement> result = new List<SurfaceFittingPlacement>();
		HashSet<Transform> hashSet = new HashSet<Transform>();
		List<Transform> list = new List<Transform>();
		Transform[] componentsInChildren = mastRoot.GetComponentsInChildren<Transform>(includeInactive: true);
		CapsuleCollider component = mastRoot.GetComponent<CapsuleCollider>();
		MeshFilter component2 = mastRoot.GetComponent<MeshFilter>();
		Mesh mesh = ((component2 != null) ? component2.sharedMesh : null);
		Vector3[] mastVertices = ((mesh != null && mesh.isReadable) ? mesh.vertices : null);
		Bounds mastBounds = ((mesh != null) ? mesh.bounds : default(Bounds));
		foreach (Transform transform in componentsInChildren)
		{
			if (!(transform == mastRoot) && (transform.GetComponent<GPButtonRopeWinch>() != null || (IsNamedSurfaceFitting(transform) && HasOwnVisibleGeometry(transform))))
			{
				list.Add(transform);
			}
		}
		list.Sort((Transform first, Transform second) => GetHierarchyDepth(first).CompareTo(GetHierarchyDepth(second)));
		for (int num = 0; num < list.Count; num++)
		{
			Transform candidate = list[num];
			if (!HasSelectedAncestor(candidate, hashSet))
			{
				AddSurfaceFitting(result, hashSet, candidate, mastRoot, component, mastVertices, mastBounds);
			}
		}
		return result;
	}

	private static void AddSurfaceFitting(List<SurfaceFittingPlacement> result, HashSet<Transform> added, Transform candidate, Transform mastRoot, CapsuleCollider mastCapsule, Vector3[] mastVertices, Bounds mastBounds)
	{
		if (!TryGetVisibleCenterInRoot(candidate, mastRoot, out var center))
		{
			center = mastRoot.InverseTransformPoint(candidate.position);
		}
		if (TryResolveMastSection(center, mastCapsule, mastVertices, mastBounds, out var center2, out var radial, out var radius))
		{
			added.Add(candidate);
			result.Add(new SurfaceFittingPlacement
			{
				Target = candidate,
				RootRadial = radial,
				SourceSurfaceRadius = radius,
				SourceSurfaceRootPoint = new Vector2(center2.x + radial.x * radius, center2.y + radial.y * radius),
				OriginalWorldPosition = candidate.position
			});
		}
	}

	private static bool TryResolveMastSection(Vector3 fittingCenter, CapsuleCollider mastCapsule, Vector3[] mastVertices, Bounds mastBounds, out Vector2 center, out Vector2 radial, out float radius)
	{
		if (mastCapsule != null && mastCapsule.direction == 2 && mastCapsule.radius > 0f)
		{
			center = new Vector2(mastCapsule.center.x, mastCapsule.center.y);
			radial = new Vector2(fittingCenter.x - center.x, fittingCenter.y - center.y);
			if (!NormalizeAndValidateFittingRadial(ref radial))
			{
				radius = 0f;
				return false;
			}
			radius = mastCapsule.radius;
			return true;
		}
		if (mastVertices == null || mastVertices.Length == 0)
		{
			center = Vector2.zero;
			radial = Vector2.zero;
			radius = 0f;
			return false;
		}
		float num = float.PositiveInfinity;
		for (int i = 0; i < mastVertices.Length; i++)
		{
			num = Mathf.Min(num, Mathf.Abs(mastVertices[i].z - fittingCenter.z));
		}
		float num2 = Mathf.Max(0.01f, mastBounds.size.z * 0.0025f);
		float num3 = num + num2;
		float num4 = float.PositiveInfinity;
		float num5 = float.NegativeInfinity;
		float num6 = float.PositiveInfinity;
		float num7 = float.NegativeInfinity;
		int num8 = 0;
		for (int j = 0; j < mastVertices.Length; j++)
		{
			Vector3 vector = mastVertices[j];
			if (!(Mathf.Abs(vector.z - fittingCenter.z) > num3))
			{
				num4 = Mathf.Min(num4, vector.x);
				num5 = Mathf.Max(num5, vector.x);
				num6 = Mathf.Min(num6, vector.y);
				num7 = Mathf.Max(num7, vector.y);
				num8++;
			}
		}
		if (num8 < 3)
		{
			center = Vector2.zero;
			radial = Vector2.zero;
			radius = 0f;
			return false;
		}
		center = new Vector2((num4 + num5) * 0.5f, (num6 + num7) * 0.5f);
		radial = new Vector2(fittingCenter.x - center.x, fittingCenter.y - center.y);
		if (!NormalizeAndValidateFittingRadial(ref radial))
		{
			radius = 0f;
			return false;
		}
		radius = 0f;
		for (int k = 0; k < mastVertices.Length; k++)
		{
			Vector3 vector2 = mastVertices[k];
			if (!(Mathf.Abs(vector2.z - fittingCenter.z) > num3))
			{
				float b = Vector2.Dot(new Vector2(vector2.x, vector2.y) - center, radial);
				radius = Mathf.Max(radius, b);
			}
		}
		return radius > 0.001f;
	}

	internal static bool TryGetSailMountOffset(Mast mast, Sail sail, float diameterScale, out Vector3 localOffset)
	{
		localOffset = Vector3.zero;
		if (mast == null || sail == null || sail.windcenter == null || diameterScale <= 1f)
		{
			return false;
		}
		Transform transform = mast.transform;
		Vector3 vector = transform.InverseTransformPoint(sail.windcenter.position);
		Vector3 vector2 = transform.InverseTransformPoint(sail.transform.position);
		Vector2 vector3 = new Vector2(vector.x - vector2.x, vector.y - vector2.y);
		if (vector3.sqrMagnitude <= 1E-06f)
		{
			return false;
		}
		vector3.Normalize();
		CapsuleCollider component = transform.GetComponent<CapsuleCollider>();
		MeshFilter component2 = transform.GetComponent<MeshFilter>();
		Mesh mesh = ((component2 != null) ? component2.sharedMesh : null);
		Vector3[] mastVertices = ((mesh != null && mesh.isReadable) ? mesh.vertices : null);
		Bounds mastBounds = ((mesh != null) ? mesh.bounds : default(Bounds));
		if (!TryResolveMastSurfaceAtHeight(sail.transform.localPosition.z, vector3, component, mastVertices, mastBounds, out var center, out var radius))
		{
			return false;
		}
		Vector2 sourceSurfaceRootPoint = center + vector3 * radius;
		if (!TryGetSurfaceCorrectionWorld(transform, sourceSurfaceRootPoint, diameterScale, out var correctionWorld))
		{
			return false;
		}
		Vector3 vector4 = transform.InverseTransformVector(correctionWorld);
		localOffset = new Vector3(vector4.x, vector4.y, 0f);
		return localOffset.sqrMagnitude > 1E-06f;
	}

	private static bool TryResolveMastSurfaceAtHeight(float height, Vector2 radial, CapsuleCollider mastCapsule, Vector3[] mastVertices, Bounds mastBounds, out Vector2 center, out float radius)
	{
		if (mastCapsule != null && mastCapsule.direction == 2 && mastCapsule.radius > 0f)
		{
			center = new Vector2(mastCapsule.center.x, mastCapsule.center.y);
			radius = mastCapsule.radius;
			return true;
		}
		if (mastVertices == null || mastVertices.Length == 0)
		{
			center = Vector2.zero;
			radius = 0f;
			return false;
		}
		float num = float.PositiveInfinity;
		for (int i = 0; i < mastVertices.Length; i++)
		{
			num = Mathf.Min(num, Mathf.Abs(mastVertices[i].z - height));
		}
		float num2 = Mathf.Max(0.01f, mastBounds.size.z * 0.0025f);
		float num3 = num + num2;
		float num4 = float.PositiveInfinity;
		float num5 = float.NegativeInfinity;
		float num6 = float.PositiveInfinity;
		float num7 = float.NegativeInfinity;
		int num8 = 0;
		for (int j = 0; j < mastVertices.Length; j++)
		{
			Vector3 vector = mastVertices[j];
			if (!(Mathf.Abs(vector.z - height) > num3))
			{
				num4 = Mathf.Min(num4, vector.x);
				num5 = Mathf.Max(num5, vector.x);
				num6 = Mathf.Min(num6, vector.y);
				num7 = Mathf.Max(num7, vector.y);
				num8++;
			}
		}
		if (num8 < 3)
		{
			center = Vector2.zero;
			radius = 0f;
			return false;
		}
		center = new Vector2((num4 + num5) * 0.5f, (num6 + num7) * 0.5f);
		radius = 0f;
		for (int k = 0; k < mastVertices.Length; k++)
		{
			Vector3 vector2 = mastVertices[k];
			if (!(Mathf.Abs(vector2.z - height) > num3))
			{
				float b = Vector2.Dot(new Vector2(vector2.x, vector2.y) - center, radial);
				radius = Mathf.Max(radius, b);
			}
		}
		return radius > 0.001f;
	}

	private static bool NormalizeAndValidateFittingRadial(ref Vector2 radial)
	{
		float magnitude = radial.magnitude;
		if (magnitude <= 0.001f || magnitude > 1f)
		{
			return false;
		}
		radial /= magnitude;
		return true;
	}

	private static bool IsNamedSurfaceFitting(Transform candidate)
	{
		string text = UnstayedNameRules.Normalize((candidate != null) ? candidate.name : null);
		if (!text.Contains("rope holder") && !text.Contains("rope att") && !text.Contains("reef att") && !text.Contains("winch") && !text.Contains("windcloth"))
		{
			return text.Contains("flag");
		}
		return true;
	}

	private static bool HasOwnVisibleGeometry(Transform candidate)
	{
		if (!(candidate.GetComponent<MeshFilter>() != null) && !(candidate.GetComponent<SkinnedMeshRenderer>() != null))
		{
			return candidate.GetComponent<Collider>() != null;
		}
		return true;
	}

	private static bool HasSelectedAncestor(Transform candidate, HashSet<Transform> selected)
	{
		Transform parent = candidate.parent;
		while (parent != null)
		{
			if (selected.Contains(parent))
			{
				return true;
			}
			parent = parent.parent;
		}
		return false;
	}

	private static int GetHierarchyDepth(Transform candidate)
	{
		int num = 0;
		Transform transform = candidate;
		while (transform != null)
		{
			num++;
			transform = transform.parent;
		}
		return num;
	}

	private static void PlaceSurfaceFittings(Transform mastRoot, List<SurfaceFittingPlacement> fittings)
	{
		for (int i = 0; i < fittings.Count; i++)
		{
			SurfaceFittingPlacement surfaceFittingPlacement = fittings[i];
			if (surfaceFittingPlacement.Target == null)
			{
				continue;
			}
			if (!TryGetSurfaceCorrectionWorld(mastRoot, surfaceFittingPlacement.SourceSurfaceRootPoint, 1.41f, out var correctionWorld))
			{
				ManualLogSource logSource = Plugin.LogSource;
				if (logSource != null)
				{
					string[] obj = new string[11]
					{
						"Skipped implausible mast-fitting correction for ",
						surfaceFittingPlacement.Target.name,
						" on ",
						mastRoot.name,
						": sourceRadius=",
						surfaceFittingPlacement.SourceSurfaceRadius.ToString("0.###"),
						", sourcePoint=",
						null,
						null,
						null,
						null
					};
					Vector2 sourceSurfaceRootPoint = surfaceFittingPlacement.SourceSurfaceRootPoint;
					obj[7] = sourceSurfaceRootPoint.ToString();
					obj[8] = ", radial=";
					sourceSurfaceRootPoint = surfaceFittingPlacement.RootRadial;
					obj[9] = sourceSurfaceRootPoint.ToString();
					obj[10] = ".";
					logSource.LogWarning(string.Concat(obj));
				}
			}
			else
			{
				surfaceFittingPlacement.Target.position = surfaceFittingPlacement.OriginalWorldPosition + correctionWorld;
				Plugin.LogSource?.LogDebug("Placed mast fitting " + surfaceFittingPlacement.Target.name + " on the enlarged surface of " + mastRoot.name + " (correction " + correctionWorld.magnitude.ToString("0.###") + ").");
			}
		}
	}

	private static bool TryGetSurfaceCorrectionWorld(Transform mastRoot, Vector2 sourceSurfaceRootPoint, float diameterScale, out Vector3 correctionWorld)
	{
		correctionWorld = Vector3.zero;
		if (mastRoot == null || diameterScale <= 1f || sourceSurfaceRootPoint.sqrMagnitude <= 1E-06f)
		{
			return false;
		}
		Vector3 vector = new Vector3(sourceSurfaceRootPoint.x * (diameterScale - 1f), sourceSurfaceRootPoint.y * (diameterScale - 1f), 0f);
		correctionWorld = mastRoot.TransformVector(vector);
		if (correctionWorld.magnitude > 1f)
		{
			correctionWorld = Vector3.zero;
			return false;
		}
		return true;
	}

	private static bool TryGetVisibleCenterInRoot(Transform fitting, Transform mastRoot, out Vector3 center)
	{
		Vector3 zero = Vector3.zero;
		int num = 0;
		MeshFilter[] componentsInChildren = fitting.GetComponentsInChildren<MeshFilter>(includeInactive: true);
		foreach (MeshFilter meshFilter in componentsInChildren)
		{
			if (!(meshFilter.sharedMesh == null))
			{
				zero += mastRoot.InverseTransformPoint(meshFilter.transform.TransformPoint(meshFilter.sharedMesh.bounds.center));
				num++;
			}
		}
		SkinnedMeshRenderer[] componentsInChildren2 = fitting.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
		for (int j = 0; j < componentsInChildren2.Length; j++)
		{
			zero += mastRoot.InverseTransformPoint(componentsInChildren2[j].transform.TransformPoint(componentsInChildren2[j].localBounds.center));
			num++;
		}
		if (num == 0)
		{
			center = Vector3.zero;
			return false;
		}
		center = zero / num;
		return true;
	}

	private static List<RestrictedPartSelection> FindRestrictedSelections(Transform boat, BoatCustomParts parts, List<BoatPart> mastParts, Dictionary<BoatPart, List<BoatPartOption>> sourcesByMastPart)
	{
		List<RestrictedPartSelection> list = new List<RestrictedPartSelection>();
		for (int i = 0; i < parts.availableParts.Count; i++)
		{
			BoatPart boatPart = parts.availableParts[i];
			if (boatPart == null || mastParts.Contains(boatPart) || boatPart.partOptions == null || boatPart.partOptions.Count == 0 || !TryGetRestrictedKind(boatPart, boat, out var kind))
			{
				continue;
			}
			List<BoatPart> list2 = FindOwningMastParts(boatPart, mastParts, sourcesByMastPart);
			if (list2.Count != 0)
			{
				RestrictedPartSelection restrictedPartSelection = CreateRestrictedSelection(boatPart, list2, kind, boat);
				if (restrictedPartSelection != null)
				{
					list.Add(restrictedPartSelection);
				}
			}
		}
		return list;
	}

	private static List<RestrictedPartSelection> GetRestrictedSelectionsForMast(List<RestrictedPartSelection> selections, BoatPart mastPart)
	{
		List<RestrictedPartSelection> list = new List<RestrictedPartSelection>();
		for (int i = 0; i < selections.Count; i++)
		{
			if (selections[i].OwningMastParts.Contains(mastPart))
			{
				list.Add(selections[i]);
			}
		}
		return list;
	}

	private static bool TryGetRestrictedKind(BoatPart part, Transform boat, out RestrictedPartKind kind)
	{
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		for (int i = 0; i < part.partOptions.Count; i++)
		{
			string directOptionSignalText = GetDirectOptionSignalText(part.partOptions[i], boat);
			flag |= HasRiggingText(directOptionSignalText);
			flag2 |= HasRiggingAccessoryText(directOptionSignalText);
			flag3 |= HasCrowsNestText(directOptionSignalText);
		}
		if (flag && !flag3)
		{
			kind = RestrictedPartKind.Rigging;
			return true;
		}
		if (flag3 && !flag)
		{
			kind = RestrictedPartKind.CrowsNest;
			return true;
		}
		if (flag2 && !flag && !flag3)
		{
			kind = RestrictedPartKind.RiggingAccessory;
			return true;
		}
		bool flag4 = false;
		bool flag5 = false;
		bool flag6 = false;
		for (int j = 0; j < part.partOptions.Count; j++)
		{
			string optionSignalText = GetOptionSignalText(part.partOptions[j], boat);
			flag4 |= HasRiggingText(optionSignalText);
			flag5 |= HasRiggingAccessoryText(optionSignalText);
			flag6 |= HasCrowsNestText(optionSignalText);
		}
		if (flag4 && !flag6)
		{
			kind = RestrictedPartKind.Rigging;
			return true;
		}
		if (flag6 && !flag4)
		{
			kind = RestrictedPartKind.CrowsNest;
			return true;
		}
		if (flag5 && !flag4 && !flag6)
		{
			kind = RestrictedPartKind.RiggingAccessory;
			return true;
		}
		kind = RestrictedPartKind.Rigging;
		return false;
	}

	private static List<BoatPart> FindOwningMastParts(BoatPart candidatePart, List<BoatPart> mastParts, Dictionary<BoatPart, List<BoatPartOption>> sourcesByMastPart)
	{
		List<BoatPart> list = new List<BoatPart>();
		int num = 0;
		for (int i = 0; i < mastParts.Count; i++)
		{
			BoatPart boatPart = mastParts[i];
			int partRelationScore = GetPartRelationScore(candidatePart, sourcesByMastPart[boatPart]);
			if (partRelationScore > num)
			{
				num = partRelationScore;
				list.Clear();
				list.Add(boatPart);
			}
			else if (partRelationScore > 0 && partRelationScore == num)
			{
				list.Add(boatPart);
			}
		}
		if (num == 0)
		{
			list.Clear();
		}
		else if (list.Count > 1 && num < 80)
		{
			Plugin.LogSource?.LogWarning("Skipped a weak multi-mast Shipyard relationship (" + GetPartLabel(candidatePart) + ").");
			list.Clear();
		}
		return list;
	}

	private static int GetPartRelationScore(BoatPart candidatePart, List<BoatPartOption> sources)
	{
		int num = 0;
		for (int i = 0; i < candidatePart.partOptions.Count; i++)
		{
			BoatPartOption candidate = candidatePart.partOptions[i];
			for (int j = 0; j < sources.Count; j++)
			{
				num = Math.Max(num, GetOptionRelationScore(candidate, sources[j]));
			}
		}
		return num;
	}

	private static int GetOptionRelationScore(BoatPartOption candidate, BoatPartOption mastOption)
	{
		if (candidate == null || mastOption == null)
		{
			return 0;
		}
		if (Contains(candidate.requires, mastOption) || Contains(mastOption.requires, candidate) || candidate.childMast == mastOption.GetComponent<Mast>())
		{
			return 100;
		}
		if (Contains(candidate.requiresDisabled, mastOption) || Contains(mastOption.requiresDisabled, candidate))
		{
			return 90;
		}
		if (AnyChildWithin(candidate.childOptions, mastOption.transform, (mastOption.walkColObject != null) ? mastOption.walkColObject.transform : null))
		{
			return 80;
		}
		if (AnyChildWithin(mastOption.childOptions, candidate.transform, (candidate.walkColObject != null) ? candidate.walkColObject.transform : null))
		{
			return 70;
		}
		return 0;
	}

	private static bool AnyChildWithin(GameObject[] children, Transform firstRoot, Transform secondRoot)
	{
		if (children == null)
		{
			return false;
		}
		for (int i = 0; i < children.Length; i++)
		{
			Transform transform = ((children[i] != null) ? children[i].transform : null);
			if (IsWithin(transform, firstRoot) || IsWithin(transform, secondRoot))
			{
				return true;
			}
		}
		return false;
	}

	private static string GetPartLabel(BoatPart part)
	{
		if (part == null || part.partOptions == null || part.partOptions.Count == 0 || part.partOptions[0] == null)
		{
			return "unnamed";
		}
		BoatPartOption boatPartOption = part.partOptions[0];
		if (!string.IsNullOrWhiteSpace(boatPartOption.optionName))
		{
			return boatPartOption.optionName;
		}
		return boatPartOption.name;
	}

	private static RestrictedPartSelection CreateRestrictedSelection(BoatPart part, List<BoatPart> owningMastParts, RestrictedPartKind kind, Transform boat)
	{
		BoatPartOption boatPartOption = null;
		List<BoatPartOption> list = new List<BoatPartOption>();
		for (int i = 0; i < part.partOptions.Count; i++)
		{
			BoatPartOption boatPartOption2 = part.partOptions[i];
			if (boatPartOption2 == null)
			{
				continue;
			}
			if (IsEmptyOption(boatPartOption2, kind))
			{
				if (boatPartOption == null)
				{
					boatPartOption = boatPartOption2;
				}
			}
			else
			{
				list.Add(boatPartOption2);
			}
		}
		if (list.Count == 0)
		{
			return null;
		}
		if (boatPartOption == null && kind == RestrictedPartKind.Rigging)
		{
			boatPartOption = CreateNoShroudsOption(part, boat);
		}
		return new RestrictedPartSelection
		{
			Part = part,
			OwningMastParts = new List<BoatPart>(owningMastParts),
			EmptyOption = boatPartOption,
			NonEmptyOptions = list,
			Kind = kind
		};
	}

	private static bool IsEmptyOption(BoatPartOption option, RestrictedPartKind kind)
	{
		if (!(option.GetComponent<EmptyRestrictedPartMarker>() != null))
		{
			return IsExplicitEmptyName(option, kind);
		}
		return true;
	}

	private static bool IsExplicitEmptyName(BoatPartOption option, RestrictedPartKind kind)
	{
		string text = UnstayedNameRules.Normalize(option.optionName);
		string text2 = UnstayedNameRules.Normalize(option.name);
		string text3 = text + " " + text2;
		if (text == "none" || text == "empty")
		{
			return true;
		}
		switch (kind)
		{
		case RestrictedPartKind.CrowsNest:
			if (HasCrowsNestText(text3))
			{
				return UnstayedNameRules.HasAbsenceToken(text3);
			}
			return false;
		case RestrictedPartKind.RiggingAccessory:
			if (HasRiggingAccessoryText(text3))
			{
				return UnstayedNameRules.HasAbsenceToken(text3);
			}
			return false;
		default:
			if (HasRiggingText(text3) || UnstayedNameRules.ContainsWord(text3, "rig"))
			{
				return UnstayedNameRules.HasAbsenceToken(text3);
			}
			return false;
		}
	}

	private static BoatPartOption CreateNoShroudsOption(BoatPart part, Transform boat)
	{
		GameObject gameObject = new GameObject("UJSM_empty_shrouds");
		gameObject.SetActive(value: false);
		gameObject.transform.SetParent(boat, worldPositionStays: false);
		BoatPartOption boatPartOption = gameObject.AddComponent<BoatPartOption>();
		GameObject gameObject2 = new GameObject("UJSM_empty_shrouds_walk");
		gameObject2.SetActive(value: false);
		gameObject2.transform.SetParent(boat, worldPositionStays: false);
		boatPartOption.optionName = "(no shrouds)";
		boatPartOption.basePrice = 0;
		boatPartOption.installCost = 0;
		boatPartOption.mass = 0;
		boatPartOption.requires = new List<BoatPartOption>();
		boatPartOption.requiresDisabled = new List<BoatPartOption>();
		boatPartOption.walkColObject = gameObject2;
		boatPartOption.canInstall = true;
		boatPartOption.childOptions = new GameObject[0];
		boatPartOption.childMast = null;
		gameObject.AddComponent<EmptyRestrictedPartMarker>();
		part.partOptions.Add(boatPartOption);
		Plugin.LogSource?.LogInfo("Added missing (no shrouds) option to a rigging Shipyard group.");
		return boatPartOption;
	}

	private static void AddMutualRestrictions(BoatPartOption unstayed, List<RestrictedPartSelection> selections)
	{
		for (int i = 0; i < selections.Count; i++)
		{
			List<BoatPartOption> nonEmptyOptions = selections[i].NonEmptyOptions;
			for (int j = 0; j < nonEmptyOptions.Count; j++)
			{
				AddMutualExclusion(unstayed, nonEmptyOptions[j]);
			}
		}
	}

	private static void RestrictNoShroudsToUnstayedMasts(List<RestrictedPartSelection> selections)
	{
		for (int i = 0; i < selections.Count; i++)
		{
			RestrictedPartSelection restrictedPartSelection = selections[i];
			if (restrictedPartSelection.Kind != RestrictedPartKind.Rigging || restrictedPartSelection.EmptyOption == null)
			{
				continue;
			}
			for (int j = 0; j < restrictedPartSelection.OwningMastParts.Count; j++)
			{
				BoatPart boatPart = restrictedPartSelection.OwningMastParts[j];
				for (int k = 0; k < boatPart.partOptions.Count; k++)
				{
					BoatPartOption boatPartOption = boatPart.partOptions[k];
					if (!(boatPartOption == null) && !(boatPartOption.GetComponent<Mast>() == null) && !(boatPartOption.GetComponent<UnstayedMastMarker>() != null))
					{
						AddMutualExclusion(restrictedPartSelection.EmptyOption, boatPartOption);
					}
				}
			}
		}
	}

	private static void AddMutualExclusion(BoatPartOption first, BoatPartOption second)
	{
		if (!(first == null) && !(second == null))
		{
			first.requiresDisabled = first.requiresDisabled ?? new List<BoatPartOption>();
			second.requiresDisabled = second.requiresDisabled ?? new List<BoatPartOption>();
			AddUnique(first.requiresDisabled, second);
			AddUnique(second.requiresDisabled, first);
		}
	}

	private static bool HasRiggingText(string text)
	{
		if (!text.Contains("shroud") && !text.Contains("static rig") && !text.Contains("static rope atts"))
		{
			return text.Contains("static rope attachment");
		}
		return true;
	}

	private static bool HasRiggingAccessoryText(string text)
	{
		if (!text.Contains("telltale"))
		{
			return text.Contains("wind flag");
		}
		return true;
	}

	private static bool HasCrowsNestText(string text)
	{
		if (!text.Contains("crowsnest") && !text.Contains("crows nest") && !text.Contains("crownest"))
		{
			return text.Contains("crow nest");
		}
		return true;
	}

	private static string GetDirectOptionSignalText(BoatPartOption option, Transform boat)
	{
		if (option == null)
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(option.optionName).Append(' ').Append(option.name)
			.Append(' ')
			.Append(GetHierarchyPath(option.transform, boat));
		if (option.walkColObject != null)
		{
			stringBuilder.Append(' ').Append(GetHierarchyPath(option.walkColObject.transform, boat));
		}
		return UnstayedNameRules.Normalize(stringBuilder.ToString());
	}

	private static string GetOptionSignalText(BoatPartOption option, Transform boat)
	{
		if (option == null)
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(option.optionName).Append(' ').Append(option.name)
			.Append(' ')
			.Append(GetHierarchyPath(option.transform, boat));
		if (option.walkColObject != null)
		{
			stringBuilder.Append(' ').Append(GetHierarchyPath(option.walkColObject.transform, boat));
		}
		if (option.childOptions != null)
		{
			for (int i = 0; i < option.childOptions.Length; i++)
			{
				if (option.childOptions[i] != null)
				{
					stringBuilder.Append(' ').Append(GetHierarchyPath(option.childOptions[i].transform, boat));
				}
			}
		}
		return UnstayedNameRules.Normalize(stringBuilder.ToString());
	}
}
